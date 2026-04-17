using System.Net;
using System.Text.Json;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Console.Models;

namespace Console.Services;

public class RenshuuScraper
{
	private readonly HttpClient HttpClient;
	private readonly string BaseUrl;
	private readonly string LookupPath;

	public RenshuuScraper(HttpClient httpClient, string baseUrl, string lookupPath)
	{
		HttpClient = httpClient;
		BaseUrl = baseUrl;
		LookupPath = lookupPath;
	}

	public async Task<KanjiResult?> ScrapeFullAsync(string kanji, CancellationToken ct = default)
	{
		var json = await FetchPageAsync(kanji, ct);
		if (string.IsNullOrEmpty(json)) return null;

		using var doc = JsonDocument.Parse(json);
		var htmlElement = doc.RootElement.GetProperty("stext_kanji");
		var rawHtml = htmlElement.GetString() ?? "";
		if (string.IsNullOrEmpty(rawHtml)) return null;

		var cleanHtml = rawHtml
			.Replace("\\\"", "\"")
			.Replace("\\n", "\n")
			.Replace("\\t", "\t");

		var parser = new HtmlParser();
		var document = await parser.ParseDocumentAsync(cleanHtml, ct);

		// Meaning: div[style*='font-size:165%']
		var meaningEl = document.QuerySelector("div[style*='165%']");
		var meaning = meaningEl?.TextContent.Trim() ?? "";

		var kunyomi = ParseReadings("Kunyomi", cleanHtml);
		var onyomi = ParseReadings("Onyomi", cleanHtml);

		var strokes = ParseStrokes(cleanHtml);
		var radical = ParseRadical(cleanHtml);

		// Mnemonic: first mnemonic_box only
		var boxes = document.QuerySelectorAll("div.mnemonic_box");
		MnemonicInfo? mnemonic = null;
		if (boxes.Length > 0)
		{
			var firstBox = boxes[0];
			var existing = ParseMnemonicBox(firstBox, kanji);
			if (existing != null)
			{
				mnemonic = new MnemonicInfo(existing.Text, existing.ImageUrl, existing.Author);
			}
		}

		return new KanjiResult(
			Kanji: kanji,
			ImageUrl: "",
			Meaning: meaning,
			Kunyomi: kunyomi,
			Onyomi: onyomi,
			Radical: radical,
			Strokes: strokes,
			Mnemonic: mnemonic,
			Jlpt: null,
			Kentei: null);
	}

	public async Task<MnemonicResult?> ScrapeAsync(string kanji, CancellationToken ct = default)
	{
		var json = await FetchPageAsync(kanji, ct);
		if (string.IsNullOrEmpty(json)) return null;

		// Parse JSON to extract HTML from stext_kanji field
		using var doc = JsonDocument.Parse(json);
		var htmlElement = doc.RootElement.GetProperty("stext_kanji");
		var rawHtml = htmlElement.GetString() ?? "";
		if (string.IsNullOrEmpty(rawHtml)) return null;

		// Unescape JSON-encoded quotes and other escape sequences
		var cleanHtml = rawHtml
			.Replace("\\\"", "\"")
			.Replace("\\n", "\n")
			.Replace("\\t", "\t");

		var parser = new HtmlParser();
		var document = await parser.ParseDocumentAsync(cleanHtml, ct);

		var boxes = document.QuerySelectorAll("div.mnemonic_box");

		return boxes
			.Select(box => ParseMnemonicBox(box, kanji))
			.Where(m => m != null)
			.Cast<MnemonicResult>()
			.FirstOrDefault();
	}

	private async Task<string?> FetchPageAsync(string kanji, CancellationToken ct)
	{
		var url = $"{BaseUrl}{LookupPath}";
		var content = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["kanji_japanese"] = kanji,
			["kword_filter"] = "n1",
			["br_dark"] = "1"
		});

		for (int attempt = 0; attempt < 2; attempt++)
		{
			try
			{
				var response = await HttpClient.PostAsync(url, content, ct);
				if (response.StatusCode == HttpStatusCode.NotFound) return null;
				if (!response.IsSuccessStatusCode)
				{
					if (attempt == 0) await Task.Delay(1000, ct);
					continue;
				}

				return await response.Content.ReadAsStringAsync(ct);
			}
			catch (HttpRequestException) when (attempt == 0)
			{
				await Task.Delay(1000, ct);
			}
		}

		return null;
	}

	private MnemonicResult? ParseMnemonicBox(IElement box, string kanji)
	{
		var imgEl = box.QuerySelector("div[id^=\"mnimg_\"] img");
		var imageUrl = imgEl?.GetAttribute("src") ?? "";

		var textEl = box.QuerySelector("div[id^=\"mnemonic_\"]");
		var text = textEl?.InnerHtml ?? "";

		// The author link is nested deeper: div.grow > div.indent > span.little > a[href$="/me/N"]
		var authorEl = box.QuerySelector("a[href*=\"/me/\"]");
		var author = authorEl?.TextContent ?? "";

		return new MnemonicResult(kanji, imageUrl, text, author);
	}

	private List<Reading> ParseReadings(string sectionId, string rawHtml)
	{
		var results = new List<Reading>();

		// Use regex to extract readings based on section
		// The HTML structure is: SectionName: <span class="lnk">reading</span><sub class='little'>level</sub><br/>...
		// We need to find the section and extract all readings until the next section or end

		// Find where this section starts
		var sectionPattern = sectionId + @":\s*";
		var sectionMatch = System.Text.RegularExpressions.Regex.Match(rawHtml, sectionPattern);
		if (!sectionMatch.Success) return results;

		// Find where the next section starts (Onyomi, Strokes, or Radical) - search AFTER current section
		var searchStart = sectionMatch.Index + sectionMatch.Length;
		var nextSectionPattern = @"(?:Onyomi|Strokes|Radical):\s*";
		var remainingHtml = rawHtml.Substring(searchStart);
		var nextSectionMatch = System.Text.RegularExpressions.Regex.Match(remainingHtml, nextSectionPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);

		// Extract the content for this section
		var sectionEnd = nextSectionMatch.Success ? searchStart + nextSectionMatch.Index : rawHtml.Length;
		if (sectionEnd <= searchStart) return results; // Safety check
		var sectionContent = rawHtml.Substring(searchStart, sectionEnd - searchStart);

		// Split by <br/> to get individual readings
		var lines = sectionContent.Split(new[] { "<br/>", "<br>" }, StringSplitOptions.RemoveEmptyEntries);

		foreach (var line in lines)
		{
			// Match ALL readings in this line
			var lnkMatches = System.Text.RegularExpressions.Regex.Matches(line, @"class=""lnk""[^>]*>([^<]+)</span>");
			var lvlMatches = System.Text.RegularExpressions.Regex.Matches(line, @"<sub class='little'>([^<]+)</sub>");

			for (int i = 0; i < lnkMatches.Count; i++)
			{
				var text = lnkMatches[i].Groups[1].Value;
				var level = i < lvlMatches.Count ? lvlMatches[i].Groups[1].Value : "";
				results.Add(new Reading(text, level));
			}
		}

		return results;
	}

	private int ParseStrokes(string rawHtml)
	{
		var strokesMatch = System.Text.RegularExpressions.Regex.Match(rawHtml, @"Strokes:\s*(\d+)");
		return strokesMatch.Success ? int.Parse(strokesMatch.Groups[1].Value) : 0;
	}

	private RadicalInfo? ParseRadical(string rawHtml)
	{
		var radicalMatch = System.Text.RegularExpressions.Regex.Match(
			rawHtml,
			@"Radical:\s*<span class=""noto""><span data-klook>([^<]+)</span></span>\s*<span class='little'>\(([^)]+)\)</span>");
		if (!radicalMatch.Success) return null;
		var names = radicalMatch.Groups[2].Value.Split(',').Select(n => n.Trim()).ToList();
		return new RadicalInfo(radicalMatch.Groups[1].Value, names);
	}
}