using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace SparkPoster.Internal;

/// <summary>Assembles a query string and reads the paging cursor back out of a response.</summary>
internal sealed class QueryBuilder
{
    private readonly StringBuilder _builder = new();

    public void Add(string name, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        _builder.Append(_builder.Length == 0 ? '?' : '&')
            .Append(name)
            .Append('=')
            .Append(Uri.EscapeDataString(value));
    }

    public void Add(string name, int? value)
    {
        if (value is { } number)
        {
            Add(name, number.ToString(CultureInfo.InvariantCulture));
        }
    }

    public void Add(string name, bool? value)
    {
        if (value is { } flag)
        {
            Add(name, flag ? "true" : "false");
        }
    }

    public void AddList(string name, IReadOnlyList<string>? values)
    {
        if (values is { Count: > 0 })
        {
            Add(name, string.Join(',', values));
        }
    }

    /// <summary>
    /// The Events API expects <c>YYYY-MM-DDTHH:MM</c> and reads it in the account time zone unless
    /// a separate <c>timezone</c> parameter says otherwise, so values are converted to UTC and the
    /// caller declares the time zone once.
    /// </summary>
    public void AddTimestamp(string name, DateTimeOffset? value)
    {
        if (value is { } moment)
        {
            Add(name, moment.UtcDateTime.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// The suppression list expects <c>YYYY-MM-DDTHH:mm:ssZ</c> — seconds and an explicit offset,
    /// no separate timezone parameter. The instant is sent in UTC.
    /// </summary>
    public void AddOffsetTimestamp(string name, DateTimeOffset? value)
    {
        if (value is { } moment)
        {
            Add(name, moment.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// Pulls the cursor out of the next-page link. SparkPost hands back a ready-made URL with
    /// every filter already in place; only the cursor is carried over, so that the caller can
    /// store it and resume later. The shape of <c>links</c> varies across endpoints, so both
    /// an object with <c>next</c> and an array of <c>rel</c>/<c>href</c> are accepted.
    /// </summary>
    public static string? ExtractNextCursor(JsonNode? links)
    {
        var next = links switch
        {
            JsonObject linkObject => (string?)linkObject["next"],
            JsonArray linkArray => linkArray
                .OfType<JsonObject>()
                .FirstOrDefault(link => (string?)link["rel"] is "next")?["href"]?.GetValue<string>(),
            _ => null,
        };

        if (string.IsNullOrEmpty(next))
        {
            return null;
        }

        var queryStart = next.IndexOf('?', StringComparison.Ordinal);

        if (queryStart < 0)
        {
            return null;
        }

        foreach (var pair in next[(queryStart + 1)..].Split('&'))
        {
            var separator = pair.IndexOf('=', StringComparison.Ordinal);

            if (separator > 0 && pair[..separator] is "cursor")
            {
                return Uri.UnescapeDataString(pair[(separator + 1)..]);
            }
        }

        return null;
    }

    public override string ToString() => _builder.ToString();
}
