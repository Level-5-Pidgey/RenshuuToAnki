using System.Text;
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
		AnsiConsole.MarkupLine("[bold blue]Renshuu Kanji Dictionary Extractor[/]");
		AnsiConsole.MarkupLine($"AnkiConnect: {Markup.Escape(settings.AnkiConnectUrl)}");
		AnsiConsole.MarkupLine($"Query: {Markup.Escape(settings.Query)}");
		AnsiConsole.MarkupLine($"Mode: {settings.Mode}");
		AnsiConsole.WriteLine();

		var keyComparer = StringComparer.OrdinalIgnoreCase;

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

		if (allNotes.Length == 0)
		{
			AnsiConsole.MarkupLine("[yellow]No notes found. Exiting.[/]");
			return 0;
		}
		AnsiConsole.MarkupLine($"[green]Found {allNotes.Length} notes.[/]");

		var sourceToDest = new Dictionary<string, string>(settings.FieldMap!, StringComparer.OrdinalIgnoreCase);
		if (!sourceToDest.TryGetValue("kanji", out var kanjiField))
		{
			AnsiConsole.MarkupLine(
				"[bold red]Error:[/] 'kanji' source must be mapped (required to look up the kanji).");
			AnsiConsole.MarkupLine("[bold yellow]Aborting.[/]");
			return -1;
		}
		
		var existingFields = allNotes.SelectMany(n => n.Fields.Keys)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
		var missing = sourceToDest.Values
			.Where(v => !existingFields.Contains(v))
			.ToList();
		if (missing.Count > 0)
		{
			AnsiConsole.MarkupLine(
				$"[bold red]Error:[/] Missing fields in note type: {string.Join(", ", missing)}.");
			AnsiConsole.MarkupLine("[bold yellow]Aborting.[/]");
			return -1;
		}

		// Step 2: Filter notes needing updates
		var cardsNeedingUpdate = allNotes
			.Where(n =>
			{
				var kanjiVal = n.Fields.FirstOrDefault(kvp => keyComparer.Equals(kvp.Key, kanjiField)).Value;
				var hasKanji = kanjiVal != null && !string.IsNullOrWhiteSpace(kanjiVal.Value);
				if (!hasKanji)
				{
					return false;
				}

				return settings.Mode switch
				{
					CommandSettings.UpdateMode.Replace => true,
					CommandSettings.UpdateMode.ReadOnly => true,
					CommandSettings.UpdateMode.AddEmpty => sourceToDest.Values.Any(dest =>
					{
						var fieldVal = n.Fields.FirstOrDefault(kvp => keyComparer.Equals(kvp.Key, dest)).Value;
						return fieldVal == null || string.IsNullOrWhiteSpace(fieldVal.Value);
					}),
					_ => throw new ArgumentOutOfRangeException()
				};
			})
			.ToList();

		AnsiConsole.MarkupLine($"[green]{cardsNeedingUpdate.Count} cards need updates.[/]");

		if (cardsNeedingUpdate.Count == 0)
		{
			AnsiConsole.MarkupLine("[yellow]All cards are up to date. Exiting.[/]");
			return 0;
		}

		// Step 3: Group by kanji
		var kanjiToNotes = cardsNeedingUpdate
			.Select(n =>
			{
				var kanjiVal = n.Fields.FirstOrDefault(kvp => keyComparer.Equals(kvp.Key, kanjiField)).Value;
				return (Note: n, Kanji: kanjiVal?.Value ?? "");
			})
			.Where(x => !string.IsNullOrWhiteSpace(x.Kanji))
			.GroupBy(x => x.Kanji)
			.ToDictionary(g => g.Key, g => g.Select(x => x.Note).ToList());

		// Step 4: Fetch KanjiResult for each unique kanji
		AnsiConsole.MarkupLine("[bold]Fetching kanji data from Renshuu...[/]");

		var kanjiResultMap = new Dictionary<string, KanjiResult>();

		await AnsiConsole.Status()
			.Spinner(Spinner.Known.Star)
			.StartAsync("Fetching kanji...", async ctx =>
			{
				var kanjiList = kanjiToNotes.Keys.ToList();
				for (var i = 0; i < kanjiList.Count; i++)
				{
					var kanji = kanjiList[i];
					ctx.Status($"Fetching {kanji} ({i + 1}/{kanjiList.Count})");

					await rateLimiter.WaitAsync(cancellationToken);
					var result = await scraper.ScrapeFullAsync(kanji, cancellationToken);

					if (result != null)
					{
						// Clean mnemonic text if present
						if (result.Mnemonic != null)
						{
							var cleaned = htmlCleaner.Clean(result.Mnemonic.Text);
							result = result with
							{
								Mnemonic = result.Mnemonic with { Text = cleaned }
							};
						}

						kanjiResultMap[kanji] = result;

						var fieldInfos = new List<string>();
						foreach (var (source, dest) in sourceToDest)
						{
							if (source.Equals("kanji", StringComparison.OrdinalIgnoreCase))
								continue;

							var value = source.ToLowerInvariant() switch
							{
								"meaning" => result.Meaning,
								"kunyomi" => string.Join(", ", result.Kunyomi.Select(r => r.Text)),
								"onyomi" => string.Join(", ", result.Onyomi.Select(r => r.Text)),
								"radical" => result.Radical?.Character,
								"strokes" => result.Strokes.ToString(),
								"mnemonic" => result.Mnemonic?.Text != null
									? System.Text.RegularExpressions.Regex.Replace(result.Mnemonic.Text, "<[^>]*>", "")
									: null,
								"jlpt" => result.Jlpt,
								"kentei" => result.Kentei,
								_ => null
							};

							if (!string.IsNullOrEmpty(value))
							{
								var valLen = Math.Min(value.Length, 40);
								var truncated = new StringBuilder(value[..Math.Min(value.Length, 40)]);
								if (valLen < value.Length)
								{
									truncated.Append('…');
								}
								
								fieldInfos.Add($"{dest}: {truncated.ToString()}");
							}
						}

						var info = fieldInfos.Count > 0 ? $" ({string.Join(", ", fieldInfos)})" : "";
						AnsiConsole.MarkupLine($"[green]Found[/]: {kanji}{info}");
					}
					else
					{
						AnsiConsole.MarkupLine($"[yellow]Warning[/]: {kanji}: No data found");
					}
				}
			});

		if (settings.Mode == CommandSettings.UpdateMode.ReadOnly)
		{
			AnsiConsole.MarkupLine("[bold yellow]Read-only mode — no cards will be updated.[/]");
			var table = new Table();
			table.AddColumn("Kanji");
			foreach (var dest in sourceToDest.Values.Distinct())
				table.AddColumn(dest);

			foreach (var kanji in kanjiToNotes.Keys)
			{
				var row = new List<string> { kanji };
				if (kanjiResultMap.TryGetValue(kanji, out var kr))
				{
					foreach (var src in sourceToDest.Keys)
						row.Add(MapSourceToValue(src, kr));
				}
				else
				{
					row.AddRange(Enumerable.Repeat("[yellow]—[/]", sourceToDest.Count));
				}

				table.AddRow(row.ToArray());
			}

			AnsiConsole.Write(table);
			return 0;
		}

		// Step 5: Update Anki cards
		AnsiConsole.MarkupLine("[bold]Updating Anki cards...[/]");
		var updated = 0;
		var failed = 0;

		foreach (var (kanji, notes) in kanjiToNotes)
		{
			if (!kanjiResultMap.TryGetValue(kanji, out var kr)) continue;

			foreach (var note in notes)
			{
				var fields = new Dictionary<string, string>();
				foreach (var (src, dest) in sourceToDest)
				{
					var value = MapSourceToValue(src, kr);
					if (!string.IsNullOrEmpty(value))
						fields[dest] = value;
				}

				if (fields.Count == 0) continue;

				var success = await ankiConnector.UpdateNoteFieldsAsync(note.NoteId, fields, cancellationToken);
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

	private static string MapSourceToValue(string source, KanjiResult kr)
	{
		return source switch
		{
			"kanji" => kr.Kanji,
			"meaning" => kr.Meaning,
			"kunyomi" => string.Join(", ",
				kr.Kunyomi.Select(r => string.IsNullOrEmpty(r.SchoolLevel) ? r.Text : $"{r.Text} ({r.SchoolLevel})")),
			"onyomi" => string.Join(", ",
				kr.Onyomi.Select(r => string.IsNullOrEmpty(r.SchoolLevel) ? r.Text : $"{r.Text} ({r.SchoolLevel})")),
			"radical" => kr.Radical != null ? $"{kr.Radical.Character}: {string.Join(", ", kr.Radical.Names)}" : "",
			"strokes" => kr.Strokes > 0 ? kr.Strokes.ToString() : "",
			"mnemonic" => kr.FormattedMnemonic,
			"jlpt" => kr.Jlpt ?? "",
			"kentei" => kr.Kentei ?? "",
			_ => ""
		};
	}
}