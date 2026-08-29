using System.Text.Json.Serialization;
using SparkPoster.Internal;

namespace SparkPoster;

/// <summary>
/// The recipients of a transmission: either listed explicitly or referenced as a stored list.
/// </summary>
/// <remarks>
/// The two forms look different on the wire — an array versus an object with <c>list_id</c> —
/// which is exactly why they are united in a single type.
/// </remarks>
[JsonConverter(typeof(RecipientSetJsonConverter))]
public sealed class RecipientSet
{
    private RecipientSet(IReadOnlyList<Recipient>? items, string? listId)
    {
        Items = items;
        ListId = listId;
    }

    /// <summary>The explicitly listed recipients. <c>null</c> when a stored list is used.</summary>
    public IReadOnlyList<Recipient>? Items { get; }

    /// <summary>The stored list identifier. <c>null</c> when recipients are listed explicitly.</summary>
    public string? ListId { get; }

    /// <summary>Lists the recipients explicitly.</summary>
    /// <param name="recipients">The recipients.</param>
    /// <returns>The recipient set.</returns>
    public static RecipientSet Inline(IReadOnlyList<Recipient> recipients)
    {
        ArgumentNullException.ThrowIfNull(recipients);
        return new RecipientSet(recipients, listId: null);
    }

    /// <summary>References a stored recipient list.</summary>
    /// <param name="listId">The list identifier.</param>
    /// <returns>The recipient set.</returns>
    /// <remarks>
    /// Per-recipient overrides are ignored in this form, and subaccounts cannot use stored
    /// lists at all.
    /// </remarks>
    public static RecipientSet StoredList(string listId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(listId);
        return new RecipientSet(items: null, listId);
    }
}
