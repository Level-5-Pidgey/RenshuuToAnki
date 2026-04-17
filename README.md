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

| Option                   | Description                                                                                            | Default                 |
|--------------------------|--------------------------------------------------------------------------------------------------------|-------------------------|
| `--query`                | Anki search query for cards to update                                                                  | (none)                  |
| `--anki-url`             | AnkiConnect HTTP URL                                                                                   | `http://localhost:8765` |
| `--rpm`                  | Max requests per minute to Renshuu                                                                     | `120`                   |
| `--field`                | Source→destination field mapping                                                                       | (none)                  |
| `--mode`                 | Operation mode: `readonly` (preview only), `replace` (update all), `addempty` (only fill empty fields) | `addempty`              |
| `--mnemonic-kanji-class` | CSS class template for kanji spans in mnemonics (must contain `{index}` placeholder)                   | (sequential colors)     |

### `--mnemonic-kanji-class` Formatting

When extracting mnemonics, each kanji span is individually tagged so you can style them differently (Renshuu uses distinct colors for each part). By default, sequential colors are applied: `#fc3199`, `#f5c10f`, `#aa1aff`, `#31a0f6`.

To use CSS classes instead, provide a template string with an `{index}` placeholder:

```bash
# Results in: <span class="kanji-0">, <span class="kanji-1">, <span class="kanji-2">...
--mnemonic-kanji-class "kanji-{index}"

# Index-first format: <span class="0-kanji">, <span class="1-kanji">...
--mnemonic-kanji-class "{index}-kanji"
```

The placeholder `{index}` is replaced with an incrementing number (0, 1, 2, ...) for each span.

### `--field` Mapping

The `--field` option maps Renshuu data sources to Anki note fields. Specify multiple times for different fields.

**Supported sources:** `kanji`, `kunyomi`, `onyomi`, `radical`, `meaning`, `strokes`, `mnemonic`, `jlpt`, `kentei`

**Example — Updating multiple fields:**  

This is my personal workflow.

```bash
dotnet run -- \
    --query="tag:Languages::Japanese::Writing::Kanji" \
    --mode replace \
    --field kanji=Kanji \
    --field mnemonic=Mnemonic \
    --field onyomi=Reading \
    --field meaning=Meaning
```

**Example — Meaning-only update (read-only preview):**
```bash
dotnet run -- \
    --query "deck:Kanji" \
    --field kanji=Character \
    --field meaning=Meaning \
    --mode readonly
```
