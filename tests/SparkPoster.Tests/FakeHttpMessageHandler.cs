using System.Net;

namespace SparkPoster.Tests;

/// <summary>
/// Captures the outgoing request and returns a canned response.
/// It covers exactly what matters: which URL, method, headers and body we sent.
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

    /// <summary>
    /// Answers with the given bodies in order, so paging can be exercised. The last body is
    /// repeated if more requests arrive than there are bodies.
    /// </summary>
    public static FakeHttpMessageHandler ReturningSequence(params string[] bodies)
    {
        var index = 0;

        return new FakeHttpMessageHandler(_ =>
        {
            var body = bodies[Math.Min(index++, bodies.Length - 1)];

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            };
        });
    }

    public int RequestCount { get; private set; }

    public HttpClient CreateClient() => new(this, disposeHandler: false);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestCount++;
        LastRequest = request;
        LastBody = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);

        return _respond(request);
    }
}
