using System.Net;
using Moq;
using Moq.Protected;
using Console.Services;

namespace Test;

public class AnkiConnectorTests
{
	private static AnkiConnector CreateConnector(Mock<HttpMessageHandler> handlerMock)
	{
		var httpClient = new HttpClient(handlerMock.Object);
		return new AnkiConnector(httpClient, "http://localhost:8765");
	}

	[Test]
	public async Task NotesInfoAsync_ReturnsNotesFromQuery()
	{
		var jsonResponse = """
		                   {
		                     "error": null,
		                     "result": [
		                       {
		                         "noteId": 1502298033753,
		                         "modelName": "Basic",
		                         "tags": ["tag1"],
		                         "fields": {
		                           "Front": { "value": "内容", "order": 0 },
		                           "Back": { "value": "meaning", "order": 1 }
		                         },
		                         "mod": 1718377864,
		                         "cards": [1498938915662]
		                       }
		                     ]
		                   }
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

		var connector = CreateConnector(handlerMock);
		var result = await connector.NotesInfoAsync("deck:Kanji");

		Assert.That(result.Length, Is.EqualTo(1));
		Assert.Multiple(() =>
		{
			Assert.That(result[0].NoteId, Is.EqualTo(1502298033753));
			Assert.That(result[0].Fields["Front"].Value, Is.EqualTo("内容"));
		});
	}

	[Test]
	public async Task UpdateNoteAsync_CallsUpdateNoteAction()
	{
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
				Content = new StringContent("{\"error\":null,\"result\":[true]}")
			});

		var connector = CreateConnector(handlerMock);
		var result = await connector.UpdateNoteAsync(123, "<img src=\"test\"/>");

		Assert.That(result, Is.True);
		handlerMock.Protected().Verify(
			"SendAsync",
			Times.Once(),
			ItExpr.Is<HttpRequestMessage>(req =>
				req.Content != null &&
				req.Content.ReadAsStringAsync().Result.Contains("updateNote")),
			ItExpr.IsAny<CancellationToken>());
	}

	[Test]
	public async Task UpdateNoteAsync_RetriesOnceOnFailure()
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
					: new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent("{\"error\":null,\"result\":[true]}") };
			});

		var connector = CreateConnector(handlerMock);
		var result = await connector.UpdateNoteAsync(123, "text");

		Assert.Multiple(() =>
		{
			Assert.That(result, Is.True);
			Assert.That(callCount, Is.EqualTo(2));
		});
	}

	[Test]
	public async Task NotesInfoAsync_FieldMatchingIsCaseInsensitive()
	{
		var jsonResponse = """
		                   {
		                     "error": null,
		                     "result": [
		                       {
		                         "noteId": 1502298033753,
		                         "modelName": "Basic",
		                         "tags": [],
		                         "fields": {
		                           "kanji": { "value": "日", "order": 0 },
		                           "mnemonic": { "value": "", "order": 1 }
		                         },
		                         "mod": 1718377864,
		                         "cards": [1498938915662]
		                       }
		                     ]
		                   }
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

		var connector = CreateConnector(handlerMock);
		var result = await connector.NotesInfoAsync("deck:Kanji");

		Assert.That(result.Length, Is.EqualTo(1));
		Assert.Multiple(() =>
		{
			Assert.That(result[0].Fields.ContainsKey("kanji"), Is.True);
			Assert.That(result[0].Fields.ContainsKey("KANJI"), Is.True);
			Assert.That(result[0].Fields["kanji"].Value, Is.EqualTo("日"));
		});
	}

	[Test]
	public async Task UpdateNoteFieldsAsync_SendsAllFieldsInOneCall()
	{
		long? capturedNoteId = null;
		Dictionary<string, string>? capturedFields = null;

		var handlerMock = new Mock<HttpMessageHandler>();
		handlerMock.Protected()
			.Setup<Task<HttpResponseMessage>>(
				"SendAsync",
				ItExpr.IsAny<HttpRequestMessage>(),
				ItExpr.IsAny<CancellationToken>())
			.ReturnsAsync(new HttpResponseMessage
			{
				StatusCode = HttpStatusCode.OK,
				Content = new StringContent("{\"error\":null,\"result\":[true]}")
			})
			.Callback<HttpRequestMessage, CancellationToken>((req, _) =>
			{
				var body = req.Content.ReadAsStringAsync().Result;
				using var doc = System.Text.Json.JsonDocument.Parse(body);
				capturedNoteId = doc.RootElement.GetProperty("params").GetProperty("note").GetProperty("id").GetInt64();
				var fields = doc.RootElement.GetProperty("params").GetProperty("note").GetProperty("fields");
				capturedFields = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(fields.GetRawText());
			});

		var httpClient = new HttpClient(handlerMock.Object);
		var connector = new AnkiConnector(httpClient, "http://localhost:8765");

		var fields = new Dictionary<string, string>
		{
			["Meaning"] = "ten thousand",
			["Kunyomi"] = "よろず (x)"
		};
		var result = await connector.UpdateNoteFieldsAsync(12345, fields, CancellationToken.None);

		Assert.That(result, Is.True);
		Assert.That(capturedNoteId, Is.EqualTo(12345));
		Assert.Multiple(() =>
		{
			Assert.That(capturedFields!["Meaning"], Is.EqualTo("ten thousand"));
			Assert.That(capturedFields["Kunyomi"], Is.EqualTo("よろず (x)"));
		});
	}
}