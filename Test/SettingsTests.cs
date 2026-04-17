using RenshuuMnemonicExtractor;

namespace Test;

public class SettingsTests
{
    [Test]
    public void DefaultValues_AreSensible()
    {
        var settings = new Settings();

        Assert.That(settings.Query, Is.EqualTo("tag:Languages::Japanese::Writing::Kanji"));
        Assert.That(settings.AnkiConnectUrl, Is.EqualTo("http://localhost:8765"));
        Assert.That(settings.RequestsPerMinute, Is.EqualTo(120));
        Assert.That(settings.ReadOnly, Is.True);
    }

    [Test]
    public void AllProperties_SetViaConstructor()
    {
        var settings = new Settings(
            query: "deck:Kanji",
            ankiConnectUrl: "http://localhost:9999",
            requestsPerMinute: 60,
            readOnly: false
        );

        Assert.That(settings.Query, Is.EqualTo("deck:Kanji"));
        Assert.That(settings.AnkiConnectUrl, Is.EqualTo("http://localhost:9999"));
        Assert.That(settings.RequestsPerMinute, Is.EqualTo(60));
        Assert.That(settings.ReadOnly, Is.False);
    }
}