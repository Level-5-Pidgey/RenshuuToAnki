using Console.Models;
using Console.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Console;

public class RenshuuCommand : AsyncCommand<CommandSettings>
{
	protected override async Task<int> ExecuteAsync(CommandContext context, CommandSettings settings,
		CancellationToken cancellationToken)
	{
		AnsiConsole.MarkupLine("[bold blue]Renshuu Mnemonic Extractor[/]");
		AnsiConsole.MarkupLine($"AnkiConnect: {Markup.Escape(settings.AnkiConnectUrl)}");
		AnsiConsole.MarkupLine($"Query: {Markup.Escape(settings.Query)}");
		AnsiConsole.MarkupLine($"Read-only: {settings.ReadOnly}");
		AnsiConsole.WriteLine();

		var httpClient = new HttpClient();
		var ankiConnector = new AnkiConnector(httpClient, settings.AnkiConnectUrl);
		var scraper = new RenshuuScraper(
			httpClient,
			"https://www.renshuu.org",
			"/index.php?page=misc/lookup_kanji");
		var rateLimiter = new RateLimiter(settings.RequestsPerMinute);
		var htmlCleaner = new MnemonicHtmlCleaner(settings.MnemonicKanjiClass);

		// Step 1: Discover cards
		AnsiConsole.MarkupLine("[bold]Discovering cards...[/]");
		NoteInfo[] allNotes;
		try
		{
			allNotes = await ankiConnector.NotesInfoAsync(settings.Query, cancellationToken);
		}
		catch (Exception ex)
		{
			AnsiConsole.MarkupLine(
				$"[bold red]Error:[/] Failed to connect to AnkiConnect: {Markup.Escape(ex.Message)}");
			return -1;
		}

		AnsiConsole.MarkupLine($"[green]Found {allNotes.Length} notes.[/]");

		if (allNotes.Length == 0)
		{
			AnsiConsole.MarkupLine("[yellow]No notes found. Exiting.[/]");
			return 0;
		}

		// Filter: non-empty Kanji field, empty Mnemonic field
		var keyComparer = StringComparer.OrdinalIgnoreCase;
		var cardsNeedingMnemonics = allNotes
			.Where(n =>
			{
				var kanjiField = n.Fields.FirstOrDefault(kvp => keyComparer.Equals(kvp.Key, settings.KanjiField)).Value;
				var mnField = n.Fields.FirstOrDefault(kvp => keyComparer.Equals(kvp.Key, settings.MnemonicField)).Value;
				return kanjiField != null
				       && !string.IsNullOrWhiteSpace(kanjiField.Value)
				       && (settings.Overwrite || mnField == null || string.IsNullOrWhiteSpace(mnField.Value));
			})
			.ToList();

		AnsiConsole.MarkupLine($"[green]{cardsNeedingMnemonics.Count} cards need mnemonics.[/]");

		if (cardsNeedingMnemonics.Count == 0)
		{
			AnsiConsole.MarkupLine("[yellow]All cards already have mnemonics. Exiting.[/]");

			return 0;
		}

		// Group by kanji to avoid fetching the same mnemonic twice
		var kanjiToNotes = cardsNeedingMnemonics
			.Select(n =>
			{
				var kanjiField = n.Fields.FirstOrDefault(kvp => keyComparer.Equals(kvp.Key, settings.KanjiField)).Value;
				return (Note: n, Kanji: kanjiField?.Value ?? "");
			})
			.Where(x => !string.IsNullOrWhiteSpace(x.Kanji))
			.GroupBy(x => x.Kanji)
			.ToDictionary(g => g.Key, g => g.Select(x => x.Note).ToList());

		// Step 2: Fetch mnemonics from Renshuu
		AnsiConsole.MarkupLine("[bold]Fetching mnemonics from Renshuu...[/]");

		var mnemonicMap = new Dictionary<string, MnemonicResult>();

		await AnsiConsole.Status()
			.Spinner(Spinner.Known.Star)
			.StartAsync("Fetching mnemonics...", async ctx =>
			{
				var kanjiList = kanjiToNotes.Keys.ToList();
				for (var i = 0; i < kanjiList.Count; i++)
				{
					var kanji = kanjiList[i];
					ctx.Status($"Fetching {kanji} ({i + 1}/{kanjiList.Count})");

					await rateLimiter.WaitAsync(cancellationToken);
					var mnemonic = await scraper.ScrapeAsync(kanji, cancellationToken);

					if (mnemonic != null)
					{
						var cleanedText = htmlCleaner.Clean(mnemonic.Text);
						mnemonicMap[kanji] = mnemonic with { Kanji = kanji, Text = cleanedText };
						AnsiConsole.MarkupLine(
							$"[green]Found[/]: {kanji} mnemonic by {Markup.Escape(mnemonic.Author)}");
					}
					else
					{
						AnsiConsole.MarkupLine($"[yellow]Warning[/]: {kanji}: No mnemonics found");
					}
				}
			});

		if (settings.ReadOnly)
		{
			AnsiConsole.MarkupLine("[bold yellow]Read-only mode — no cards will be updated.[/]");
			var table = new Table();
			table.AddColumn("Kanji");
			table.AddColumn("Text");
			table.AddColumn("Author");
			foreach (var kanji in kanjiToNotes.Keys)
			{
				if (mnemonicMap.TryGetValue(kanji, out var m))
					table.AddRow(kanji, m.Text, m.Author);
				else
					table.AddRow(Markup.Escape(kanji), "—", "[yellow]No mnemonic[/]");
			}

			AnsiConsole.Write(table);

			return 0;
		}

		// Step 3: Update Anki cards
		AnsiConsole.MarkupLine("[bold]Updating Anki cards...[/]");
		var updated = 0;
		var failed = 0;

		foreach (var (kanji, notes) in kanjiToNotes)
		{
			if (!mnemonicMap.TryGetValue(kanji, out var mnemonic)) continue;

			foreach (var note in notes)
			{
				var success =
					await ankiConnector.UpdateNoteAsync(note.NoteId, mnemonic.FormattedMnemonic, cancellationToken);
				if (success)
				{
					AnsiConsole.MarkupLine($"[green]Success[/]: Card {note.NoteId} ({Markup.Escape(kanji)}) updated");
					updated++;
				}
				else
				{
					AnsiConsole.MarkupLine($"[red]Error[/]: Card {note.NoteId} ({Markup.Escape(kanji)}) failed");
					failed++;
				}
			}
		}

		AnsiConsole.WriteLine();
		AnsiConsole.MarkupLine($"[bold]Done![/] Updated: {updated}, Failed: {failed}");

		return 0;
	}
}