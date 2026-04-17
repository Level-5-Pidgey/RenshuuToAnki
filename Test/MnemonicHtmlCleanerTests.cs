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
}