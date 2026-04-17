# Renshuu To Anki

Fetches fields from Kanji from [Renshuu.org](https://www.renshuu.org) and places them into your Anki cards via AnkiConnect.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Anki](https://apps.ankiweb.net) with the [AnkiConnect addon](https://ankiweb.net/shared/info/2055492159) installed and running

## Setup

1. **Configure your Anki note type** and ensure there's unique fields for each part of the Renshuu Kanji dictionary. Pass fields via CLI arguments (see mapping below).
2. **Build the solution** using `dotnet build`

## Run

```bash
cd Console
dotnet run
```

## CLI Options

| Option        | Description                                                                                                                                                 | Default                 |
|---------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------|-------------------------|
| `--query`     | Anki search query for cards to update                                                                                                                       | (none)                  |
| `--anki-url`  | AnkiConnect HTTP URL                                                                                                                                        | `http://localhost:8765` |
| `--rpm`       | Max requests per minute to Renshuu                                                                                                                          | `120`                   |
| `--field`     | Source→destination field mapping                                                                                                                            | (none)                  |
| `--mode`      | Operation mode: `readonly` (preview only), `replace` (update all), `addempty` (only fill empty fields)                 | `addempty`              |

### `--field` Mapping

The `--field` option maps Renshuu data sources to Anki note fields. Specify multiple times for different fields.

**Supported sources:** `kanji`, `kunyomi`, `onyomi`, `radical`, `meaning`, `strokes`, `mnemonic`, `jlpt`, `kentei`

**Example — Full multi-field update:**
```bash
dotnet run -- \
    --query "deck:Kanji" \
    --field kanji=Character \
    --field meaning=Meaning \
    --field kunyomi=Kunyomi \
    --field onyomi=Onyomi \
    --field radical=Radical \
    --field strokes=Strokes \
    --field mnemonic=Mnemonic
```

**Example — Meaning-only update (read-only preview):**
```bash
dotnet run -- \
    --query "tag:new" \
    --field kanji=Character \
    --field meaning=Meaning \
    --mode readonly
```
