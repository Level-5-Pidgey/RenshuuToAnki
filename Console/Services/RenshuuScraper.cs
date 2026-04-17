using System.Net;
using AngleSharp;
using AngleSharp.Dom;
using Console.Models;

namespace RenshuuMnemonicExtractor.Services;

public class RenshuuScraper
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _lookupPath;
    private readonly IBrowsingContext _context;

    public RenshuuScraper(HttpClient httpClient, string baseUrl, string lookupPath)
    {
        _httpClient = httpClient;
        _baseUrl = baseUrl;
        _lookupPath = lookupPath;
        _context = BrowsingContext.New();
    }

    public async Task<MnemonicResult?> ScrapeAsync(string kanji, CancellationToken ct = default)
    {
        var html = await FetchPageAsync(kanji, ct);
        if (string.IsNullOrEmpty(html)) return null;

        var document = await _context.OpenAsync(req => req.Content(html), ct);
        var boxes = document.QuerySelectorAll("div.mnemonic_box");

        var mnemonics = boxes
            .Select(box => ParseMnemonicBox(box, kanji))
            .Where(m => m != null)
            .Cast<MnemonicResult>()
            .ToList();

        return mnemonics.Count == 0 ? null : mnemonics.OrderByDescending(m => m.HeartCount).First();
    }

    private async Task<string?> FetchPageAsync(string kanji, CancellationToken ct)
    {
        var url = $"{_baseUrl}{_lookupPath}";
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
                var response = await _httpClient.PostAsync(url, content, ct);
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
        var heartSpan = box.QuerySelector("span[id^=\"suki_mn\"]");
        if (heartSpan == null) return null;

        var heartText = heartSpan.TextContent?.Trim() ?? "0";
        if (!int.TryParse(heartText, out var heartCount)) return null;

        var imgEl = box.QuerySelector("div[id^=\"mnimg_\"] img");
        var imageUrl = imgEl?.GetAttribute("src") ?? "";

        var textEl = box.QuerySelector("div[id^=\"mnemonic_\"]");
        var text = textEl?.InnerHtml ?? "";

        var authorEl = box.QuerySelector("div.indent a[href^=\"/me/\"]");
        var author = authorEl?.TextContent ?? "";

        return new MnemonicResult(kanji, imageUrl, text, author, heartCount);
    }
}