using System.Net;
using Console.Services;
using Moq;
using Moq.Protected;

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
		var jsonResponse = $$"""
		                    {"stext_kanji": "<div class=\"mnemonic_box\"><span id=\"suki_mn_123\">287</span><div id=\"mnimg_456\"><img src=\"https://iserve.renshuu.org/img/mns/278.svg\" /></div><div id=\"mnemonic_789\">Your <strong>little sister</strong> isn&apos;t a woman yet...</div><div class=\"indent\"><a href=\"/me/Jessica_Ilha\">Jessica_Ilha</a></div></div>"}
		                    """;

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
				Content = new StringContent(jsonResponse)
			});

		var scraper = CreateScraper(handlerMock);
		var result = await scraper.ScrapeAsync("母", CancellationToken.None);

		Assert.That(result, Is.Not.Null);
		Assert.Multiple(() =>
		{
			Assert.That(result!.Kanji, Is.EqualTo("母"));
			Assert.That(result.ImageUrl, Is.EqualTo("https://iserve.renshuu.org/img/mns/278.svg"));
			Assert.That(result.Author, Is.EqualTo("Jessica_Ilha"));
		});
	}

	[Test]
	public async Task ScrapeAsync_ParsesNestedMnemonicWithAuthor()
	{
		// Real HTML structure from Renshuu with mnemonic_holder > mnemonic_box > flexbox > grow > indent
		var jsonResponse = $$"""
		                    {"stext_kanji": "<div id=\"mnemonic_holder_1763\"><div class=\"mnemonic_box\" id=\"mnbox_739\"><div class=\"flexbox fcenter\"><div id=\"mnimg_739\"><img src=\"https://iserve.renshuu.org/img/mns/6ape8cc4j2rfveget7wqn.svg\"/></div><div class=\"grow\"><div id=\"mnemonic_739\"> A <div class='hunderline_1'>man</div> <div class='ib mn_dpiece flbox_flat'><span data-klook>人</span></div> spreading his <div class='hunderline_2'>arms</div> <div class='ib mn_dpiece flbox_flat'><span data-klook>一</span></div> wide to hold something <strong>big</strong>. </div><div class=\" indent\"><span class=\"little\">Written by: <a href=\"https://www.renshuu.org/me/11822\">rtega</a></span></div></div></div></div></div>"}
		                    """;

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
				Content = new StringContent(jsonResponse)
			});

		var scraper = CreateScraper(handlerMock);
		var result = await scraper.ScrapeAsync("人", CancellationToken.None);

		Assert.That(result, Is.Not.Null);
		Assert.Multiple(() =>
		{
			Assert.That(result!.Kanji, Is.EqualTo("人"));
			Assert.That(result.ImageUrl, Is.EqualTo("https://iserve.renshuu.org/img/mns/6ape8cc4j2rfveget7wqn.svg"));
			Assert.That(result.Author, Is.EqualTo("rtega"));
		});
	}

	[Test]
	public async Task ScrapeAsync_ReturnsNullWhenNoMnemonics()
	{
		var jsonResponse = "{\"stext_kanji\": \"\"}";

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
				Content = new StringContent(jsonResponse)
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
					: new HttpResponseMessage
						{ StatusCode = HttpStatusCode.OK, Content = new StringContent("{\"stext_kanji\": \"<div></div>\"}") };
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