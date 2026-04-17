# Renshuu Mnemonic Extractor

Fetches kanji mnemonics from [Renshuu.org](https://www.renshuu.org) and fills them into your Anki cards via AnkiConnect.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Anki](https://apps.ankiweb.net) with the [AnkiConnect addon](https://ankiweb.net/shared/info/2055492159) installed and running

## Setup

1. **Configure your Anki note type** to have a field for the kanji character (e.g., `Character`) and a field for mnemonics (e.g., `Mnemonic`). Pass the field names via `--kanji-field` and `--mnemonic-field` CLI arguments.
2. **Adjust the default query** in `Console/Settings.cs` if needed, or pass it via CLI.

## Run

```bash
cd Console
dotnet run
```

## CLI Options

| Option             | Description                                     | Default                 |
|--------------------|-------------------------------------------------|-------------------------|
| `--query`          | Anki search query for cards to update           | (none)                  |
| `--anki-url`       | AnkiConnect HTTP URL                            | `http://localhost:8765` |
| `--rpm`            | Max requests per minute to Renshuu              | `120`                   |
| `--read-only`      | Preview changes without writing to Anki         | `false`                 |
| `--kanji-field`    | Field containing the kanji character to look up | `"Kanji"`               |
| `--mnemonic-field` | Field to write fetched mnemonics into           | `"Mnemonic"`            |
| `--overwrite`      | Overwrite existing mnemonic values              | `false`                 |

Example — run against a specific deck and actually update cards:

```bash
dotnet run -- --query "deck:Kanji"
```

Example — using custom field names for a different note type:

```bash
dotnet run -- --kanji-field "Character" --mnemonic-field "Hint" --query "deck:Japanese"
```
