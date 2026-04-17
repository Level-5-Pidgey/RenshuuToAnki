using System.Text.RegularExpressions;

namespace Console.Services;

public partial class MnemonicHtmlCleaner(string? kanjiClass = null)
{
	private static readonly string[] SpanColors = [
		"#fc3199", 
		"#f5c10f", 
		"#aa1aff", 
		"#31a0f6",
		"#f54e0f",
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

		// Replace spans with data-klook - these contain individual kanji characters
		// Pattern: <span data-klook="">CHAR</span>
		// Replace with: <span class="configured-class-#">CHAR</span> or <span color="#...">CHAR</span> if no class configured
		// Same content gets the same index (and thus same color) for consistency
		if (!string.IsNullOrEmpty(kanjiClass))
		{
			// Replace {index} placeholder in the class format string with the content-based index
			result = SpanReplacementRegex().Replace(result, match =>
			{
				var charContent = match.Groups[1].Value;
				return $"<span class=\"{kanjiClass.Replace("{index}", GetOrCreateIndex(charContent).ToString())}\">{charContent}</span>";
			});
		}
		else
		{
			// Add color attribute cycling through the palette based on content
			result = SpanReplacementRegex().Replace(result, match =>
			{
				var charContent = match.Groups[1].Value;
				var idx = GetOrCreateIndex(charContent);
				var color = SpanColors[idx % SpanColors.Length];
				return $"<span color=\"{color}\">{charContent}</span>";
			});
		}

		// Handle mn_dpiece divs with data-tip containing direct text (not wrapped in span)
		// Pattern: <div class="mn_dpiece" data-tip="content">TEXT</div>
		// Convert to: <span class="configured-class">TEXT</span> or <span color="#...">TEXT</span>
		result = MnDpieceTipRegex().Replace(result, match =>
		{
			var innerContent = match.Groups[2].Value;
			// data-tip often contains &nbsp; for spacing - only create span if meaningful
			if (string.IsNullOrWhiteSpace(innerContent) || innerContent == "\u00A0" || innerContent == "&nbsp;")
			{
				return match.Value; // Keep original if no meaningful content
			}

			if (!string.IsNullOrEmpty(kanjiClass))
			{
				return $"<span class=\"{kanjiClass.Replace("{index}", GetOrCreateIndex(innerContent).ToString())}\">{innerContent}</span>";
			}
			else
			{
				var idx = GetOrCreateIndex(innerContent);
				var color = SpanColors[idx % SpanColors.Length];
				return $"<span color=\"{color}\">{innerContent}</span>";
			}
		});

		// Handle mn_spiece divs containing inline SVGs - wrap the SVG in a styled span
		// Pattern: <div class="mn_dpiece mn_spiece"><svg>...</svg></div>
		// Convert to: <span class="..."><svg>...</svg></span> or <span color="..."><svg>...</svg></span>
		result = MnSpieceSvgRegex().Replace(result, match =>
		{
			var svgContent = match.Groups[1].Value;
			if (!string.IsNullOrEmpty(kanjiClass))
			{
				return $"<span class=\"{kanjiClass.Replace("{index}", GetOrCreateIndex(svgContent).ToString())}\">{svgContent}</span>";
			}
			else
			{
				var idx = GetOrCreateIndex(svgContent);
				var color = SpanColors[idx % SpanColors.Length];
				return $"<span color=\"{color}\">{svgContent}</span>";
			}
		});

		// Handle mn_dpiece divs that contain only the kanji span - unwrap them
		// Pattern: <div class="ib mn_dpiece flbox_flat"><span class="kanji">人</span></div>
		// These should become just: <span class="kanji">人</span>
		result = KanjiSpanRegex().Replace(result, "$1");

		// Remove hunderline_* classes and their wrapper divs if no other content
		// Pattern: <div class="hunderline_1">content</div> -> content
		result = UnderlineRegex().Replace(result, "$1");

		// Remove ib and flbox_* classes from remaining divs/spans
		// Use word boundaries to match complete class names only
		result = FlexboxRegex().Replace(result, "");
		// Clean up class attributes - remove empty class=""
		result = ClassAttributeRegex().Replace(result, "");
		// Clean up any double spaces
		result = DoubleSpaceRegex().Replace(result, " ");

		// Remove empty class attributes
		result = EmptyClassAttrRegex().Replace(result, "$1$2");

		// Unwrap empty divs (divs with no attributes and just text content)
		result = UnwrapEmptyDivRegex().Replace(result, "$1");

		// Clean up any double spaces again to be sure
		result = DoubleSpaceRegex().Replace(result, " ");

		return result.Trim();
	}
}