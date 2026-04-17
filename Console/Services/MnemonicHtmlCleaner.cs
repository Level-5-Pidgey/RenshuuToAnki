using System.Text.RegularExpressions;

namespace Console.Services;

public class MnemonicHtmlCleaner
{
	private readonly string? _kanjiClass;

	public MnemonicHtmlCleaner(string? kanjiClass = null)
	{
		_kanjiClass = kanjiClass;
	}

	public string Clean(string html)
	{
		if (string.IsNullOrEmpty(html)) return html;

		var result = html;

		// Replace spans with data-klook - these contain individual kanji characters
		// Pattern: <span data-klook="">CHAR</span>
		// Replace with: <span class="configured-class">CHAR</span> or <span>CHAR</span> if no class configured
		if (!string.IsNullOrEmpty(_kanjiClass))
		{
			// Replace with configured class
			result = Regex.Replace(result,
				@"<span data-klook=""[^""]*"">([^<]*)</span>",
				$"<span class=\"{_kanjiClass}\">$1</span>",
				RegexOptions.IgnoreCase);
		}
		else
		{
			// Just remove data-klook attribute, keep the span
			result = Regex.Replace(result,
				@"<span data-klook=""[^""]*"">",
				"<span>",
				RegexOptions.IgnoreCase);
		}

		// Handle mn_dpiece divs that contain only the kanji span - unwrap them
		// Pattern: <div class="ib mn_dpiece flbox_flat"><span class="kanji">人</span></div>
		// These should become just: <span class="kanji">人</span>
		result = Regex.Replace(result,
			@"<div[^>]*\bmn_dpiece\b[^>]*>((?:<span[^>]*>[^<]*</span>)?)</div>",
			"$1",
			RegexOptions.IgnoreCase);

		// Remove hunderline_* classes and their wrapper divs if no other content
		// Pattern: <div class="hunderline_1">content</div> -> content
		result = Regex.Replace(result,
			@"<div[^>]*\bhunderline_\d+\b[^>]*>([^<]*)</div>",
			"$1",
			RegexOptions.IgnoreCase);

		// Remove ib and flbox_* classes from remaining divs/spans
		// Use word boundaries to match complete class names only
		result = Regex.Replace(result,
			@"\b(?:ib|flbox_flat|flbox_\S+)\b",
			"",
			RegexOptions.IgnoreCase);
		// Clean up class attributes - remove empty class=""
		result = Regex.Replace(result,
			@"class=""\s*""",
			"",
			RegexOptions.IgnoreCase);
		// Clean up any double spaces
		result = Regex.Replace(result, @"\s{2,}", " ");

		// Remove empty class attributes
		result = Regex.Replace(result,
			@"(<(?:div|span)[^>]*)class=""""([^>]*>)",
			"$1$2",
			RegexOptions.IgnoreCase);

		// Unwrap empty divs (divs with no attributes and just text content)
		result = Regex.Replace(result,
			@"<div>([^<]*)</div>",
			"$1",
			RegexOptions.IgnoreCase);

		// Clean up any double spaces
		result = Regex.Replace(result, @"\s{2,}", " ");

		return result.Trim();
	}
}