using Console.Models;

namespace Test;

public class KanjiResultTests
{
	[Test]
	public void FormattedMnemonic_WithMnemonic_ReturnsImgAndText()
	{
		var mn = new MnemonicInfo("It looks like a T", "https://example.com/img.svg", "Author");
		var result = new KanjiResult(
			Kanji: "万",
			ImageUrl: "https://example.com/w.svg",
			Meaning: "ten thousand",
			Kunyomi: [new Reading("よろず", "x")],
			Onyomi: [new Reading("マン", "小"), new Reading("バン", "中")],
			Radical: new RadicalInfo("一", ["いち"]),
			Strokes: 3,
			Mnemonic: mn,
			Jlpt: "N1",
			Kentei: "2級");

		var formatted = result.FormattedMnemonic;
		Assert.That(formatted, Is.EqualTo("<img src=\"https://example.com/img.svg\"/><br/>It looks like a T"));
	}

	[Test]
	public void FormattedMnemonic_WithNullMnemonic_ReturnsEmpty()
	{
		var result = new KanjiResult(
			Kanji: "万", ImageUrl: "", Meaning: "", Kunyomi: [], Onyomi: [],
			Radical: null, Strokes: 0, Mnemonic: null, Jlpt: null, Kentei: null);

		Assert.That(result.FormattedMnemonic, Is.EqualTo(""));
	}

	[Test]
	public void Reading_StoresTextAndSchoolLevel()
	{
		var r = new Reading("マン", "小");
		Assert.Multiple(() =>
		{
			Assert.That(r.Text, Is.EqualTo("マン"));
			Assert.That(r.SchoolLevel, Is.EqualTo("小"));
		});
	}

	[Test]
	public void RadicalInfo_StoresCharacterAndNames()
	{
		var rad = new RadicalInfo("一", ["いち", "だいがしら"]);
		Assert.Multiple(() =>
		{
			Assert.That(rad.Character, Is.EqualTo("一"));
			Assert.That(rad.Names, Is.EqualTo(["いち", "だいがしら"]));
		});
	}
}