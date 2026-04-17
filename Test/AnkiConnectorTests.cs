using System.Net;
using Moq;
using Moq.Protected;
using RenshuuMnemonicExtractor.Services;
using System.Text.Json;

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
        [
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
        Assert.That(result[0].NoteId, Is.EqualTo(1502298033753));
        Assert.That(result[0].Fields["Front"].Value, Is.EqualTo("内容"));
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
                Content = new StringContent("[true]")
            });

        var connector = CreateConnector(handlerMock);
        var result = await connector.UpdateNoteAsync(123, "Mnemonic", "<img src=\"test\"/>");

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
                    : new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent("[true]") };
            });

        var connector = CreateConnector(handlerMock);
        var result = await connector.UpdateNoteAsync(123, "Mnemonic", "text");

        Assert.That(result, Is.True);
        Assert.That(callCount, Is.EqualTo(2));
    }
}