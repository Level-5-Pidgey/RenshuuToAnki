using System.Text.RegularExpressions;

namespace Console.Services;

public partial class MnemonicHtmlCleaner(string? kanjiClass = null)
{
	private static readonly string[] SpanColors = [
		"#fc3199", 
		"#f5c10f", 
		"#aa1aff", 
		"#31a0f6",
		"#27c200",
		"#0ff54e"
	];

	public static MnemonicHtmlCleaner Create(string? kanjiClassWithIndex)
	{
		ArgumentNullException.ThrowIfNull(kanjiClassWithIndex);

		return kanjiClassWithIndex.Contains("{index}") ? 
			new MnemonicHtmlCleaner(kanjiClassWithIndex) : 
			throw new ArgumentException("kanjiClass must contain '{index}' placeholder for the incrementing index (e.g., \"kanji-{index}\" or \"{index}-kanji\").", nameof(kanjiClassWithIndex));
	}

	[GeneratedRegex(@"<span data-klook=""[^""]*"">([^<]*)</span>", RegexOptions.IgnoreCase, "en-AU")]
	private static partial Regex SpanReplacementRegex();

	[GeneratedRegex(@"<div[^>]*\bmn_dpiece\b[^>]*\bdata-tip=""([^""]*)""[^>]*>([^<]*)</div>", RegexOptions.IgnoreCase, "en-AU")]
	private static partial Regex MnDpieceTipRegex();

	[GeneratedRegex(@"<div[^>]*\bmn_spiece\b[^>]*>(\s*<svg[^>]*>.*?</svg>\s*)</div>", RegexOptions.IgnoreCase | RegexOptions.Singleline, "en-AU")]
	private static partial Regex MnSpieceSvgRegex();

	[GeneratedRegex(@"<div[^>]*\bmn_dpiece\b[^>]*>((?:<span[^>]*>[^<]*</span>)?)</div>", RegexOptions.IgnoreCase, "en-AU")]
	private static partial Regex KanjiSpanRegex();

	[GeneratedRegex(@"<div[^>]*\bmn_dpiece\b[^>]*>([^<]{1,3})</div>", RegexOptions.IgnoreCase, "en-AU")]
	private static partial Regex MnDpieceTextFallbackRegex();

	[GeneratedRegex(@"(?:stroke[=:])""?#000""?", RegexOptions.IgnoreCase, "en-AU")]
	private static partial Regex SvgStrokeColorRegex();

	[GeneratedRegex(@"<svg[^>]*>", RegexOptions.IgnoreCase, "en-AU")]
	private static partial Regex SvgTagRegex();

	[GeneratedRegex(@"<span data-kanji-svg="""">(.*?)</span>", RegexOptions.IgnoreCase | RegexOptions.Singleline, "en-AU")]
	private static partial Regex DataKanjiSvgRegex();

	[GeneratedRegex(@"<span data-kanji="""">([^<]*)</span>", RegexOptions.IgnoreCase, "en-AU")]
	private static partial Regex DataKanjiRegex();

	[GeneratedRegex(@"<div[^>]*\bhunderline_\d+\b[^>]*>([^<]*)</div>", RegexOptions.IgnoreCase, "en-AU")]
	private static partial Regex UnderlineRegex();
    
	[GeneratedRegex(@"\b(?:ib|flbox_flat|flbox_\S+)\b", RegexOptions.IgnoreCase, "en-AU")]
	private static partial Regex FlexboxRegex();
    
	[GeneratedRegex(@"class=""\s*""", RegexOptions.IgnoreCase, "en-AU")]
	private static partial Regex ClassAttributeRegex();
    
	[GeneratedRegex(@"\s{2,}")]
	private static partial Regex DoubleSpaceRegex();
    
	[GeneratedRegex(@"(<(?:div|span)[^>]*)class=""""([^>]*>)", RegexOptions.IgnoreCase, "en-AU")]
	private static partial Regex EmptyClassAttrRegex();
    
	[GeneratedRegex(@"<div>([^<]*)</div>", RegexOptions.IgnoreCase, "en-AU")]
	private static partial Regex UnwrapEmptyDivRegex();

	public string Clean(string html)
	{
		if (string.IsNullOrEmpty(html))
		{
			return html;
		}

		var result = html;

		// Phase 1: Convert all kanji-bearing elements to neutral placeholders with data attributes.
		// This ensures processing happens in document order regardless of element type.

		// Replace spans with data-klook - mark as regular kanji
		result = SpanReplacementRegex().Replace(result, match =>
		{
			var charContent = match.Groups[1].Value;
			return $"<span data-kanji=\"\">{charContent}</span>";
		});

		// Handle mn_dpiece divs with data-tip containing direct text (not wrapped in span)
		result = MnDpieceTipRegex().Replace(result, match =>
		{
			var innerContent = match.Groups[2].Value;
			if (string.IsNullOrWhiteSpace(innerContent) || innerContent == "\u00A0" || innerContent == "&nbsp;")
			{
				return match.Value;
			}
			return $"<span data-kanji=\"\">{innerContent}</span>";
		});

		// Handle mn_spiece divs containing inline SVGs - mark as SVG kanji
		result = MnSpieceSvgRegex().Replace(result, match =>
		{
			var svgContent = match.Groups[1].Value;
			return $"<span data-kanji-svg=\"\">{svgContent}</span>";
		});

		// Handle mn_dpiece divs with direct text content (like ⺍) - mark as regular kanji
		result = MnDpieceTextFallbackRegex().Replace(result, match =>
		{
			var innerContent = match.Groups[1].Value;
			return $"<span data-kanji=\"\">{innerContent}</span>";
		});

		// Handle mn_dpiece divs that contain only the kanji span - unwrap them
		result = KanjiSpanRegex().Replace(result, "$1");

		// Remove hunderline_* classes and their wrapper divs
		result = UnderlineRegex().Replace(result, "$1");

		// Remove ib and flbox_* classes
		result = FlexboxRegex().Replace(result, "");
		result = ClassAttributeRegex().Replace(result, "");
		result = DoubleSpaceRegex().Replace(result, " ");

		// Remove empty class attributes
		result = EmptyClassAttrRegex().Replace(result, "$1$2");

		// Unwrap empty divs
		result = UnwrapEmptyDivRegex().Replace(result, "$1");

		// Clean up double spaces
		result = DoubleSpaceRegex().Replace(result, " ");

		// Phase 2: Collect all kanji elements in document order and apply styling with consistent indexing

		var spanIndex = 0;
		var contentToIndex = new Dictionary<string, int>();

		int GetOrCreateIndex(string content)
		{
			if (!contentToIndex.TryGetValue(content, out var idx))
			{
				idx = spanIndex++;
				contentToIndex[content] = idx;
			}
			return idx;
		}

		// Process SVG spans first (they need stroke color replacement and default sizing)
		result = DataKanjiSvgRegex().Replace(result, match =>
		{
			var svgContent = match.Groups[1].Value;
			var idx = GetOrCreateIndex(svgContent);

			// Add default styling attributes to the SVG tag if not already present
			var styledSvg = SvgStrokeColorRegex().Replace(svgContent, $"stroke:{SpanColors[idx % SpanColors.Length]}");
			styledSvg = AddDefaultSvgAttributes(styledSvg);

			if (!string.IsNullOrEmpty(kanjiClass))
			{
				var className = kanjiClass.Replace("{index}", (idx + 1).ToString());
				return $"<span class=\"{className}\">{styledSvg}</span>";
			}
			var color = SpanColors[idx % SpanColors.Length];
			return $"<span style=\"color: {color}\">{styledSvg}</span>";
		});

		// Process regular kanji spans
		result = DataKanjiRegex().Replace(result, match =>
		{
			var content = match.Groups[1].Value;
			var idx = GetOrCreateIndex(content);
			if (!string.IsNullOrEmpty(kanjiClass))
			{
				var className = kanjiClass.Replace("{index}", (idx + 1).ToString());
				return $"<span class=\"{className}\">{content}</span>";
			}
			var color = SpanColors[idx % SpanColors.Length];
			return $"<span style=\"color: {color}\">{content}</span>";
		});

		return result.Trim();
	}

	private static readonly Regex WidthAttrRegex = new(@"width=""\d+(?:\.\d+)?px""", RegexOptions.IgnoreCase);
	private static readonly Regex HeightAttrRegex = new(@"height=""\d+(?:\.\d+)?px""", RegexOptions.IgnoreCase);

	private static string AddDefaultSvgAttributes(string svgContent)
	{
		return SvgTagRegex().Replace(svgContent, match =>
		{
			var svgTag = match.Value;
			var closingBracket = svgTag.LastIndexOf('>');
			if (closingBracket == -1)
			{
				return svgTag;
			}

			var openingPortion = svgTag[..closingBracket];
			var attributes = "";

			if (WidthAttrRegex.IsMatch(openingPortion))
			{
				openingPortion = WidthAttrRegex.Replace(openingPortion, @"width=""24px""", 1);
			}
			else
			{
				attributes += @" width=""24px""";
			}
			if (HeightAttrRegex.IsMatch(openingPortion))
			{
				openingPortion = HeightAttrRegex.Replace(openingPortion, @"height=""24px""", 1);
			}
			else
			{
				attributes += @" height=""24px""";
			}
			if (!openingPortion.Contains("viewBox="))
			{
				attributes += @" viewBox=""0 0 109 109""";
			}
			if (!openingPortion.Contains("preserveAspectRatio="))
			{
				attributes += @" preserveAspectRatio=""xMidYMid meet""";
			}

			return openingPortion + attributes + ">";
		});
	}
}