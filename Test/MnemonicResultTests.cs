using RenshuuMnemonicExtractor.Models;

namespace Test;

public class MnemonicResultTests
{
    [Test]
    public void RecordProperties_AreCorrect()
    {
        var result = new MnemonicResult(
            Kanji: "母",
            ImageUrl: "https://iserve.renshuu.org/img/mns/278.svg",
            Text: "Your <strong>little sister</strong> isn't a woman yet...",
            Author: "Jessica_Ilha",
            HeartCount: 287
        );

        Assert.That(result.Kanji, Is.EqualTo("母"));
        Assert.That(result.ImageUrl, Is.EqualTo("https://iserve.renshuu.org/img/mns/278.svg"));
        Assert.That(result.Text, Is.EqualTo("Your <strong>little sister</strong> isn't a woman yet..."));
        Assert.That(result.Author, Is.EqualTo("Jessica_Ilha"));
        Assert.That(result.HeartCount, Is.EqualTo(287));
    }

    [Test]
    public void FormattedMnemonic_CombinesImageAndText()
    {
        var result = new MnemonicResult(
            Kanji: "母",
            ImageUrl: "https://iserve.renshuu.org/img/mns/278.svg",
            Text: "Your little sister...",
            Author: "Jessica_Ilha",
            HeartCount: 287
        );

        Assert.That(result.FormattedMnemonic, Is.EqualTo(
            "<img src=\"https://iserve.renshuu.org/img/mns/278.svg\"/><br/>Your little sister..."));
    }
}