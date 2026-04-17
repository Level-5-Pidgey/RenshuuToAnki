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

	[Test]
	public async Task ScrapeFullAsync_ParsesMeaning()
	{
		var jsonResponse = $$"""
			{"stext_kanji": "<div style=' font-size: 165%;'>ten thousand, 10,000</div>"}
			""";

		var handlerMock = new Mock<HttpMessageHandler>();
		handlerMock.Protected()
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
		var result = await scraper.ScrapeFullAsync("万", CancellationToken.None);

		Assert.That(result, Is.Not.Null);
		Assert.That(result!.Meaning, Is.EqualTo("ten thousand, 10,000"));
	}

	[Test]
	public async Task ScrapeFullAsync_ParsesKunyomiAndOnyomi()
	{
		var jsonResponse = $$"""
			{"stext_kanji": "Kunyomi: <span class=\"grey\"><span data-dj='1' class=\"lnk\" onclick=\"dJ('k','r=よろず' )\">よろず</span></span><sub class='little'>x</sub><br/>Onyomi: <span class=\"grey\"><span data-dj='1' class=\"lnk\" onclick=\"dJ('k','r=マン' )\">マン</span></span><sub class='little'>小</sub>, <span class=\"grey\"><span data-dj='1' class=\"lnk\" onclick=\"dJ('k','r=バン' )\">バン</span></span><sub class='little'>中</sub><br/>Strokes: 3<br/>"}
			""";

		var handlerMock = new Mock<HttpMessageHandler>();
		handlerMock.Protected()
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
		var result = await scraper.ScrapeFullAsync("万", CancellationToken.None);

		Assert.That(result, Is.Not.Null);
		Assert.Multiple(() =>
		{
			Assert.That(result!.Kunyomi.Count, Is.EqualTo(1));
			Assert.That(result.Kunyomi[0].Text, Is.EqualTo("よろず"));
			Assert.That(result.Kunyomi[0].SchoolLevel, Is.EqualTo("x"));
			Assert.That(result.Onyomi.Count, Is.EqualTo(2));
			Assert.That(result.Onyomi[0].Text, Is.EqualTo("マン"));
			Assert.That(result.Onyomi[0].SchoolLevel, Is.EqualTo("小"));
			Assert.That(result.Onyomi[1].Text, Is.EqualTo("バン"));
			Assert.That(result.Onyomi[1].SchoolLevel, Is.EqualTo("中"));
		});
	}

	[Test]
	public async Task ScrapeFullAsync_ParsesStrokesAndRadical()
	{
		var jsonResponse = $$"""
			{"stext_kanji": "Strokes: 3<br/>Radical:  <span class=\"noto\"><span data-klook>一</span></span> <span class='little'>(いち)</span><br/>"}
			""";

		var handlerMock = new Mock<HttpMessageHandler>();
		handlerMock.Protected()
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
		var result = await scraper.ScrapeFullAsync("万", CancellationToken.None);

		Assert.That(result, Is.Not.Null);
		Assert.Multiple(() =>
		{
			Assert.That(result!.Strokes, Is.EqualTo(3));
			Assert.That(result.Radical, Is.Not.Null);
			Assert.That(result.Radical!.Character, Is.EqualTo("一"));
			Assert.That(result.Radical.Names, Is.EqualTo(["いち"]));
		});
	}

	[Test]
	public async Task ScrapeFullAsync_ParsesFirstMnemonicOnly()
	{
		var jsonResponse = $$"""
			{"stext_kanji": "<div class='mnemonic_box' id='mnbox_1'><div class='flexbox fcenter'><div id='mnimg_1'><img src='https://example.com/first.svg'/></div><div class='grow'><div id='mnemonic_1'>First mnemonic</div><div class=' indent'><span class='little'>Written by: <a href='/me/Author1'>Author1</a></span></div></div></div></div><div class='mnemonic_box' id='mnbox_2'><div class='flexbox fcenter'><div id='mnimg_2'><img src='https://example.com/second.svg'/></div><div class='grow'><div id='mnemonic_2'>Second mnemonic</div></div></div></div>"}
			""";

		var handlerMock = new Mock<HttpMessageHandler>();
		handlerMock.Protected()
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
		var result = await scraper.ScrapeFullAsync("万", CancellationToken.None);

		Assert.That(result, Is.Not.Null);
		Assert.Multiple(() =>
		{
			Assert.That(result!.Mnemonic, Is.Not.Null);
			Assert.That(result.Mnemonic!.Text, Is.EqualTo("First mnemonic"));
			Assert.That(result.Mnemonic.ImageUrl, Is.EqualTo("https://example.com/first.svg"));
			Assert.That(result.Mnemonic.Author, Is.EqualTo("Author1"));
		});
	}

	[Test]
	public async Task ScrapeFullAsync_ParsesJlpt()
	{
		var jsonResponse = $$"""
			{"stext_kanji": "<div class='pure-u-1-4'>JLPT: N3</div>"}
			""";

		var handlerMock = new Mock<HttpMessageHandler>();
		handlerMock.Protected()
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
		var result = await scraper.ScrapeFullAsync("万", CancellationToken.None);

		Assert.That(result, Is.Not.Null);
		Assert.That(result!.Jlpt, Is.EqualTo("N3"));
	}

	[Test]
	public async Task ScrapeFullAsync_ParsesKentei()
	{
		var jsonResponse = $$"""
			{"stext_kanji": "<div class='pure-u-1-4'>Kanji Kentei: 2級</div>"}
			""";

		var handlerMock = new Mock<HttpMessageHandler>();
		handlerMock.Protected()
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
		var result = await scraper.ScrapeFullAsync("万", CancellationToken.None);

		Assert.That(result, Is.Not.Null);
		Assert.That(result!.Kentei, Is.EqualTo("2級"));
	}
}