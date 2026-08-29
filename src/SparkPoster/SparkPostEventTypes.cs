namespace SparkPoster;

/// <summary>
/// Типы событий SparkPost.
/// </summary>
/// <remarks>
/// Константы, а не перечисление: список пополняется на стороне сервера, и строгий
/// <c>enum</c> ронял бы разбор целого батча на первом незнакомом значении. Актуальный
/// список всегда можно получить через <see cref="IWebhooks.GetEventsDocumentationAsync"/>.
/// </remarks>
public static class SparkPostEventTypes
{
    /// <summary>Письмо принято в SparkPost.</summary>
    public const string Injection = "injection";

    /// <summary>Письмо доставлено.</summary>
    public const string Delivery = "delivery";

    /// <summary>Постоянный отказ принимающего сервера.</summary>
    public const string Bounce = "bounce";

    /// <summary>Отказ, пришедший асинхронно, уже после принятия письма.</summary>
    public const string OutOfBand = "out_of_band";

    /// <summary>Письмо отклонено политикой SparkPost — например, адресом из списка подавления.</summary>
    public const string PolicyRejection = "policy_rejection";

    /// <summary>Временная задержка доставки.</summary>
    public const string Delay = "delay";

    /// <summary>Жалоба на спам.</summary>
    public const string SpamComplaint = "spam_complaint";

    /// <summary>Открытие письма.</summary>
    public const string Open = "open";

    /// <summary>Открытие, зафиксированное пикселем начального открытия.</summary>
    public const string InitialOpen = "initial_open";

    /// <summary>Переход по ссылке.</summary>
    public const string Click = "click";

    /// <summary>Открытие AMP-версии.</summary>
    public const string AmpOpen = "amp_open";

    /// <summary>Начальное открытие AMP-версии.</summary>
    public const string AmpInitialOpen = "amp_initial_open";

    /// <summary>Переход по ссылке в AMP-версии.</summary>
    public const string AmpClick = "amp_click";

    /// <summary>Не удалось сформировать письмо.</summary>
    public const string GenerationFailure = "generation_failure";

    /// <summary>Формирование письма отклонено.</summary>
    public const string GenerationRejection = "generation_rejection";

    /// <summary>Отписка через заголовок List-Unsubscribe.</summary>
    public const string ListUnsubscribe = "list_unsubscribe";

    /// <summary>Отписка по ссылке в письме.</summary>
    public const string LinkUnsubscribe = "link_unsubscribe";

    /// <summary>Входящее письмо принято relay-вебхуком.</summary>
    public const string RelayInjection = "relay_injection";

    /// <summary>Входящее письмо отклонено.</summary>
    public const string RelayRejection = "relay_rejection";

    /// <summary>Входящее письмо доставлено вашему эндпоинту.</summary>
    public const string RelayDelivery = "relay_delivery";

    /// <summary>Временная ошибка доставки входящего письма.</summary>
    public const string RelayTempfail = "relay_tempfail";

    /// <summary>Постоянная ошибка доставки входящего письма.</summary>
    public const string RelayPermfail = "relay_permfail";

    /// <summary>A/B-тест завершён.</summary>
    public const string AbTestCompleted = "ab_test_completed";

    /// <summary>A/B-тест отменён.</summary>
    public const string AbTestCancelled = "ab_test_cancelled";
}
