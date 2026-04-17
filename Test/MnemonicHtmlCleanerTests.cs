using Console.Services;
using NUnit.Framework;

namespace Test;

public class MnemonicHtmlCleanerTests
{
	[Test]
	public void Clean_RemovesDataKlookAttribute()
	{
		var cleaner = new MnemonicHtmlCleaner();
		var input = @"A <div class=""hunderline_1"">man</div> <div class=""ib mn_dpiece flbox_flat""><span data-klook="""">人</span></div>";

		var result = cleaner.Clean(input);

		Assert.That(result, Does.Not.Contains("data-klook"));
	}

	[Test]
	public void Clean_ReplacesMnDpieceWithConfiguredClass()
	{
		var cleaner = new MnemonicHtmlCleaner("kanji");
		var input = @"<div class=""ib mn_dpiece flbox_flat""><span data-klook="""">人</span></div>";

		var result = cleaner.Clean(input);

		Assert.That(result, Does.Contain("class=\"kanji\""));
		Assert.That(result, Does.Not.Contains("mn_dpiece"));
	}

	[Test]
	public void Clean_RemovesHunderlineClasses()
	{
		var cleaner = new MnemonicHtmlCleaner();
		var input = @"<div class=""hunderline_1"">man</div> spreading <div class=""hunderline_2"">arms</div>";

		var result = cleaner.Clean(input);

		Assert.That(result, Does.Not.Contains("hunderline_1"));
		Assert.That(result, Does.Not.Contains("hunderline_2"));
	}

	[Test]
	public void Clean_RemovesIbAndFlboxClasses()
	{
		var cleaner = new MnemonicHtmlCleaner();
		var input = @"<div class=""ib flbox_flat"">text</div>";

		var result = cleaner.Clean(input);

		Assert.That(result, Does.Not.Contains("ib"));
		Assert.That(result, Does.Not.Contains("flbox_flat"));
	}

	[Test]
	public void Clean_PreservesTextContent()
	{
		var cleaner = new MnemonicHtmlCleaner();
		var input = @"A <div class=""hunderline_1"">man</div> spreading his <div class=""hunderline_2"">arms</div>";

		var result = cleaner.Clean(input);

		Assert.That(result, Does.Contain("A man spreading his arms"));
	}

	[Test]
	public void Clean_UnwrapsEmptyDivs()
	{
		var cleaner = new MnemonicHtmlCleaner();
		var input = @"<div class=""hunderline_1"">man</div>";

		var result = cleaner.Clean(input);

		// hunderline_1 removed, div should be unwrapped since no other classes remain
		Assert.That(result, Does.Not.Contains("<div"));
		Assert.That(result, Does.Contain("man"));
	}

	[Test]
	public void Clean_HandlesFullMnemonicExample()
	{
		var cleaner = new MnemonicHtmlCleaner("kanji");
		var input = @"<img src=""https://iserve.renshuu.org/img/mns/6ape8cc4j2rfveget7wqn.svg""/><br/> A <div class=""hunderline_1"">man</div> <div class=""ib mn_dpiece flbox_flat""><span data-klook="""">人</span></div> spreading his <div class=""hunderline_2"">arms</div> <div class=""ib mn_dpiece flbox_flat""><span data-klook="""">一</span></div> wide to hold something <strong>big</strong>.";

		var result = cleaner.Clean(input);

		// Check data-klook removed
		Assert.That(result, Does.Not.Contains("data-klook"));

		// Check kanji spans have configured class
		Assert.That(result, Does.Contain("class=\"kanji\""));

		// Check Renshuu-specific classes removed
		Assert.That(result, Does.Not.Contains("mn_dpiece"));
		Assert.That(result, Does.Not.Contains("hunderline_1"));
		Assert.That(result, Does.Not.Contains("hunderline_2"));
		Assert.That(result, Does.Not.Contains("flbox_flat"));

		// Check content preserved
		Assert.That(result, Does.Contain("A man"));
		Assert.That(result, Does.Contain("spreading his arms"));
		Assert.That(result, Does.Contain("wide to hold something"));
	}

	[Test]
	public void Create_RequiresIndexPlaceholder()
	{
		var ex = Assert.Throws<ArgumentException>(() => MnemonicHtmlCleaner.Create("kanji"));
		Assert.That(ex!.Message, Does.Contain("{index}"));
	}

	[Test]
	public void Create_AcceptsValidIndexPlaceholder()
	{
		var cleaner = MnemonicHtmlCleaner.Create("kanji-{index}");
		const string input = """<span data-klook="">人</span>""";

		var result = cleaner.Clean(input);

		Assert.That(result, Does.Contain(@"class=""kanji-1"""));
	}

	[Test]
	public void Create_AcceptsIndexPlaceholderAtStart()
	{
		var cleaner = MnemonicHtmlCleaner.Create("{index}-kanji");
		const string input = """<span data-klook="">人</span>""";

		var result = cleaner.Clean(input);

		Assert.That(result, Does.Contain(@"class=""1-kanji"""));
	}

	[Test]
	public void Clean_AssignsSequentialIndicesWithIndexPlaceholder()
	{
		var cleaner = MnemonicHtmlCleaner.Create("kanji-{index}");
		const string input = """<span data-klook="">人</span><span data-klook="">大</span><span data-klook="">一</span>""";

		var result = cleaner.Clean(input);

		Assert.That(result, Does.Contain(@"class=""kanji-1"""));
		Assert.That(result, Does.Contain(@"class=""kanji-2"""));
		Assert.That(result, Does.Contain(@"class=""kanji-3"""));
	}

	[Test]
	public void Clean_AssignsSameIndexToSameContent()
	{
		var cleaner = new MnemonicHtmlCleaner();
		const string input = """<span data-klook="">彡</span> first <span data-klook="">大</span> second <span data-klook="">彡</span> third""";

		var result = cleaner.Clean(input);

		// 彡 appears twice but should only use one color
		var matches = System.Text.RegularExpressions.Regex.Matches(result, @"style=""color: #fc3199""");
		Assert.That(matches.Count, Is.EqualTo(2)); // Both 彡 occurrences share color
	}

	[Test]
	public void Clean_ConvertsMnDpieceWithDataTipToSpan()
	{
		var cleaner = new MnemonicHtmlCleaner();
		var input = """<div class=" mn_dpiece " data-tip="&nbsp;" onmouseover="hooktip(this,$(this).data('tip'))">マ</div>""";

		var result = cleaner.Clean(input);

		Assert.That(result, Does.Contain("<span style="));
		Assert.That(result, Does.Contain(">マ</span>"));
		Assert.That(result, Does.Not.Contains("mn_dpiece"));
		Assert.That(result, Does.Not.Contains("data-tip"));
	}

	[Test]
	public void Clean_ConvertsMnDpieceWithDataTipToStyledSpan()
	{
		var cleaner = MnemonicHtmlCleaner.Create("kanji-{index}");
		const string input = """<div class=" mn_dpiece " data-tip="&nbsp;" onmouseover="hooktip(this,$(this).data('tip'))">マ</div>""";

		var result = cleaner.Clean(input);

		Assert.That(result, Does.Contain(@"class=""kanji-1"">マ</span>"));
	}

	[Test]
	public void Clean_ConvertsMnSpieceSvgToSpan()
	{
		var cleaner = new MnemonicHtmlCleaner();
		const string input = """<div class=" mn_dpiece mn_spiece"><svg class="im" width="109px" height="109px" viewBox="4.25 2.25 100.75 106.5" xmlns="http://www.w3.org/2000/svg"><path d="M42.12,14.75" style="fill:none;stroke:#000;stroke-width:3"></path></svg></div>""";

		var result = cleaner.Clean(input);

		Assert.That(result, Does.Contain("<span style="));
		Assert.That(result, Does.Contain("<svg"));
		Assert.That(result, Does.Not.Contains("mn_spiece"));
		Assert.That(result, Does.Not.Contains("mn_dpiece"));
	}

	[Test]
	public void Clean_SvgContentGetsUniqueIndex()
	{
		var cleaner = MnemonicHtmlCleaner.Create("svg-{index}");
		const string input = """<div class="mn_spiece"><svg class="im"><path d="M1"></path></svg></div>""";

		var result = cleaner.Clean(input);

		Assert.That(result, Does.Contain("""class="svg-1">"""));
		Assert.That(result, Does.Contain("<svg"));
	}

	[Test]
	public void Clean_PreservesColorOrderWhenContentIsUnique()
	{
		var cleaner = new MnemonicHtmlCleaner();
		const string input = """<span data-klook="">人</span><span data-klook="">大</span><span data-klook="">一</span>""";

		var result = cleaner.Clean(input);

		Assert.That(result, Does.Contain(@"style=""color: #fc3199""")); // 人
		Assert.That(result, Does.Contain(@"style=""color: #f5c10f""")); // 大
		Assert.That(result, Does.Contain(@"style=""color: #aa1aff""")); // 一
	}

	[Test]
	public void Clean_MnDpieceDataTipSkipsNbspOnlyContent()
	{
		var cleaner = new MnemonicHtmlCleaner();
		// When inner content is just nbsp, the div should be kept as-is
		const string input = """<div class="mn_dpiece" data-tip="&nbsp;">&nbsp;</div>""";

		var result = cleaner.Clean(input);

		// Should keep original since content is just nbsp
		Assert.That(result, Does.Contain("mn_dpiece"));
	}
}