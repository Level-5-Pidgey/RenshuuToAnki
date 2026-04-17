using System.Net;
using Moq;
using Moq.Protected;
using RenshuuMnemonicExtractor.Models;
using RenshuuMnemonicExtractor.Services;

namespace Test;

public class RenshuuScraperTests
{
    private static RenshuuScraper CreateScraper(Mock<HttpMessageHandler> handlerMock)
    {
        var httpClient = new HttpClient(handlerMock.Object);
        return new RenshuuScraper(httpClient, "https://www.renshuu.org", "/index.php?page=misc/lookup_kanji");
    }

    [Test]
    public async Task ScrapeAsync_ParsesMnemonicFromHtml()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div class=""mnemonic_box"">
    <span id=""suki_mn_123"">287</span>
    <div id=""mnimg_456""><img src=""https://iserve.renshuu.org/img/mns/278.svg"" /></div>
    <div id=""mnemonic_789"">Your <strong>little sister</strong> isn't a woman yet...</div>
    <div class=""indent""><a href=""/me/Jessica_Ilha"">Jessica_Ilha</a></div>
</div>
</body></html>";

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(html)
            });

        var scraper = CreateScraper(handlerMock);
        var result = await scraper.ScrapeAsync("母", CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Kanji, Is.EqualTo("母"));
        Assert.That(result.ImageUrl, Is.EqualTo("https://iserve.renshuu.org/img/mns/278.svg"));
        Assert.That(result.HeartCount, Is.EqualTo(287));
        Assert.That(result.Author, Is.EqualTo("Jessica_Ilha"));
    }

    [Test]
    public async Task ScrapeAsync_SelectsHighestHeartCountMnemonic()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div class=""mnemonic_box"">
    <span id=""suki_mn_1"">50</span>
    <div id=""mnimg_1""><img src=""https://example.com/1.svg"" /></div>
    <div id=""mnemonic_1"">Low hearts</div>
    <div class=""indent""><a href=""/me/Author1"">Author1</a></div>
</div>
<div class=""mnemonic_box"">
    <span id=""suki_mn_2"">500</span>
    <div id=""mnimg_2""><img src=""https://example.com/2.svg"" /></div>
    <div id=""mnemonic_2"">High hearts</div>
    <div class=""indent""><a href=""/me/Author2"">Author2</a></div>
</div>
</body></html>";

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(html)
            });

        var scraper = CreateScraper(handlerMock);
        var result = await scraper.ScrapeAsync("test", CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.HeartCount, Is.EqualTo(500));
        Assert.That(result.Text, Is.EqualTo("High hearts"));
    }

    [Test]
    public async Task ScrapeAsync_ReturnsNullWhenNoMnemonics()
    {
        var html = @"<!DOCTYPE html><html><body><p>No mnemonics here</p></body></html>";

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(html)
            });

        var scraper = CreateScraper(handlerMock);
        var result = await scraper.ScrapeAsync("test", CancellationToken.None);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task ScrapeAsync_RetriesOnNon200()
    {
        var callCount = 0;
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1
                    ? new HttpResponseMessage { StatusCode = HttpStatusCode.ServiceUnavailable }
                    : new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent("<html></html>") };
            });

        var scraper = CreateScraper(handlerMock);
        await scraper.ScrapeAsync("母", CancellationToken.None);

        Assert.That(callCount, Is.EqualTo(2));
    }

    [Test]
    public async Task ScrapeAsync_ReturnsNullOn404()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.NotFound });

        var scraper = CreateScraper(handlerMock);
        var result = await scraper.ScrapeAsync("invalid", CancellationToken.None);

        Assert.That(result, Is.Null);
    }
}