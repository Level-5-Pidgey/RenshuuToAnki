using RenshuuMnemonicExtractor;
using RenshuuMnemonicExtractor.Models;
using RenshuuMnemonicExtractor.Services;
using Spectre.Console;
using Spectre.Console.Cli;

var app = new CommandApp();
app.SetDefaultCommand<RenshuuCommand>();
return app.Run(args);

public class RenshuuCommand : Command<Settings>
{
    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("[bold blue]Renshuu Mnemonic Extractor[/]");
        AnsiConsole.MarkupLine($"AnkiConnect: {settings.AnkiConnectUrl}");
        AnsiConsole.MarkupLine($"Query: {settings.Query}");
        AnsiConsole.MarkupLine($"Read-only: {settings.ReadOnly}");
        AnsiConsole.WriteLine();

        var httpClient = new HttpClient();
        var ankiConnector = new AnkiConnector(httpClient, settings.AnkiConnectUrl);
        var scraper = new RenshuuScraper(
            httpClient,
            "https://www.renshuu.org",
            "/index.php?page=misc/lookup_kanji");
        var rateLimiter = new RateLimiter(settings.RequestsPerMinute);

        // Run async logic synchronously since Command<T>.Execute returns int
        ExecuteAsync(settings, ankiConnector, scraper, rateLimiter).GetAwaiter().GetResult();
        return 0;
    }

    private async Task ExecuteAsync(Settings settings, AnkiConnector ankiConnector, RenshuuScraper scraper, RateLimiter rateLimiter)
    {
        // Step 1: Discover cards
        AnsiConsole.MarkupLine("[bold]Step 1: Discovering cards...[/]");
        NoteInfo[] allNotes;
        try
        {
            allNotes = await ankiConnector.NotesInfoAsync(settings.Query);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[bold red]Error:[/] Failed to connect to AnkiConnect: {ex.Message}");
            return;
        }

        AnsiConsole.MarkupLine($"[green]Found {allNotes.Length} notes.[/]");

        if (allNotes.Length == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No notes found. Exiting.[/]");
            return;
        }

        // Filter: non-empty Kanji field, empty Mnemonic field
        // Adjust field names ("Kanji", "Mnemonic") to match your note type
        var cardsNeedingMnemonics = allNotes
            .Where(n => n.Fields.TryGetValue("Kanji", out var kanjiField)
                        && !string.IsNullOrWhiteSpace(kanjiField.Value)
                        && (!n.Fields.TryGetValue("Mnemonic", out var mnField)
                            || string.IsNullOrWhiteSpace(mnField?.Value)))
            .ToList();

        AnsiConsole.MarkupLine($"[green]{cardsNeedingMnemonics.Count} cards need mnemonics.[/]");

        if (cardsNeedingMnemonics.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]All cards already have mnemonics. Exiting.[/]");
            return;
        }

        // Group by kanji to avoid fetching the same mnemonic twice
        var kanjiToNotes = cardsNeedingMnemonics
            .GroupBy(n => n.Fields["Kanji"].Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Step 2: Fetch mnemonics from Renshuu
        AnsiConsole.MarkupLine("[bold]Step 2: Fetching mnemonics from Renshuu...[/]");

        var mnemonicMap = new Dictionary<string, MnemonicResult>();

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Star)
            .StartAsync("Fetching mnemonics...", async ctx =>
            {
                var kanjiList = kanjiToNotes.Keys.ToList();
                for (int i = 0; i < kanjiList.Count; i++)
                {
                    var kanji = kanjiList[i];
                    ctx.Status($"Fetching {kanji} ({i + 1}/{kanjiList.Count})");

                    await rateLimiter.WaitAsync();
                    var mnemonic = await scraper.ScrapeAsync(kanji);

                    if (mnemonic != null)
                    {
                        mnemonicMap[kanji] = mnemonic with { Kanji = kanji };
                        AnsiConsole.MarkupLine($"[green]✓[/] {kanji}: {mnemonic.HeartCount} hearts by {mnemonic.Author}");
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"[yellow]⚠[/] {kanji}: No mnemonics found");
                    }
                }
            });

        if (settings.ReadOnly)
        {
            AnsiConsole.MarkupLine("[bold yellow]Read-only mode — no cards will be updated.[/]");
            var table = new Table();
            table.AddColumn("Kanji");
            table.AddColumn("Hearts");
            table.AddColumn("Author");
            foreach (var kanji in kanjiToNotes.Keys)
            {
                if (mnemonicMap.TryGetValue(kanji, out var m))
                    table.AddRow(kanji, m.HeartCount.ToString(), m.Author);
                else
                    table.AddRow(kanji, "—", "[yellow]No mnemonic[/]");
            }
            AnsiConsole.Write(table);
            return;
        }

        // Step 3: Update Anki cards
        AnsiConsole.MarkupLine("[bold]Step 3: Updating Anki cards...[/]");
        var updated = 0;
        var failed = 0;

        foreach (var (kanji, notes) in kanjiToNotes)
        {
            if (!mnemonicMap.TryGetValue(kanji, out var mnemonic)) continue;

            foreach (var note in notes)
            {
                var success = await ankiConnector.UpdateNoteAsync(note.NoteId, "Mnemonic", mnemonic.FormattedMnemonic);
                if (success)
                {
                    AnsiConsole.MarkupLine($"[green]✓[/] Card {note.NoteId} ({kanji}) updated");
                    updated++;
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]✗[/] Card {note.NoteId} ({kanji}) failed");
                    failed++;
                }
            }
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold]Done![/] Updated: {updated}, Failed: {failed}");
    }
}