using System.Net;

namespace SparkPoster.Tests;

/// <summary>
/// Перехватывает исходящий запрос и отдаёт заранее заданный ответ.
/// Проверяем ровно то, что нужно: какой URL, метод, заголовки и тело мы отправили.
/// </summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

    private FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

    public HttpRequestMessage? LastRequest { get; private set; }

    public string? LastBody { get; private set; }

    public static FakeHttpMessageHandler Returning(HttpStatusCode statusCode, string body, params (string Name, string Value)[] headers)
    {
        return new FakeHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            };

            foreach (var (name, value) in headers)
            {
                response.Headers.TryAddWithoutValidation(name, value);
            }

            return response;
        });
    }

    public HttpClient CreateClient() => new(this, disposeHandler: false);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        LastBody = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);

        return _respond(request);
    }
}
