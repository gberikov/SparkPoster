using System.Text.Json.Serialization;
using SparkPoster.Internal;

namespace SparkPoster;

/// <summary>
/// Получатели письма: либо перечисленные явно, либо ссылка на сохранённый список.
/// </summary>
/// <remarks>
/// В JSON эти два варианта выглядят по-разному — массив против объекта с <c>list_id</c>, —
/// поэтому они и объединены в один тип.
/// </remarks>
[JsonConverter(typeof(RecipientSetJsonConverter))]
public sealed class RecipientSet
{
    private RecipientSet(IReadOnlyList<Recipient>? items, string? listId)
    {
        Items = items;
        ListId = listId;
    }

    /// <summary>Явно перечисленные получатели. <c>null</c>, если используется сохранённый список.</summary>
    public IReadOnlyList<Recipient>? Items { get; }

    /// <summary>Идентификатор сохранённого списка. <c>null</c>, если получатели перечислены явно.</summary>
    public string? ListId { get; }

    /// <summary>Перечисляет получателей явно.</summary>
    /// <param name="recipients">Получатели.</param>
    /// <returns>Набор получателей.</returns>
    public static RecipientSet Inline(IReadOnlyList<Recipient> recipients)
    {
        ArgumentNullException.ThrowIfNull(recipients);
        return new RecipientSet(recipients, listId: null);
    }

    /// <summary>Ссылается на сохранённый список получателей.</summary>
    /// <param name="listId">Идентификатор списка.</param>
    /// <returns>Набор получателей.</returns>
    /// <remarks>
    /// Переопределения на уровне получателя при таком варианте игнорируются,
    /// а субаккаунты сохранённые списки не поддерживают вовсе.
    /// </remarks>
    public static RecipientSet StoredList(string listId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(listId);
        return new RecipientSet(items: null, listId);
    }
}
