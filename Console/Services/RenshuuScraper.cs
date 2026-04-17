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
}