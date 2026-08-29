namespace SparkPoster;

/// <summary>
/// SparkPost event types.
/// </summary>
/// <remarks>
/// Constants rather than an enum: the server keeps adding to the list, and a strict
/// <c>enum</c> would break the parsing of a whole batch on the first unfamiliar value.
/// The current list is always available through
/// <see cref="IWebhooks.GetEventsDocumentationAsync"/>.
/// </remarks>
public static class SparkPostEventTypes
{
    /// <summary>The message was accepted into SparkPost.</summary>
    public const string Injection = "injection";

    /// <summary>The message was delivered.</summary>
    public const string Delivery = "delivery";

    /// <summary>The receiving server rejected the message permanently.</summary>
    public const string Bounce = "bounce";

    /// <summary>A rejection that arrived asynchronously, after the message was accepted.</summary>
    public const string OutOfBand = "out_of_band";

    /// <summary>SparkPost policy rejected the message, for example a suppressed address.</summary>
    public const string PolicyRejection = "policy_rejection";

    /// <summary>Delivery was temporarily delayed.</summary>
    public const string Delay = "delay";

    /// <summary>A spam complaint.</summary>
    public const string SpamComplaint = "spam_complaint";

    /// <summary>The message was opened.</summary>
    public const string Open = "open";

    /// <summary>An open recorded by the initial open pixel.</summary>
    public const string InitialOpen = "initial_open";

    /// <summary>A link was clicked.</summary>
    public const string Click = "click";

    /// <summary>The AMP part was opened.</summary>
    public const string AmpOpen = "amp_open";

    /// <summary>An initial open of the AMP part.</summary>
    public const string AmpInitialOpen = "amp_initial_open";

    /// <summary>A link in the AMP part was clicked.</summary>
    public const string AmpClick = "amp_click";

    /// <summary>The message could not be generated.</summary>
    public const string GenerationFailure = "generation_failure";

    /// <summary>Message generation was rejected.</summary>
    public const string GenerationRejection = "generation_rejection";

    /// <summary>An unsubscribe through the List-Unsubscribe header.</summary>
    public const string ListUnsubscribe = "list_unsubscribe";

    /// <summary>An unsubscribe through a link in the message.</summary>
    public const string LinkUnsubscribe = "link_unsubscribe";

    /// <summary>An inbound message was accepted by a relay webhook.</summary>
    public const string RelayInjection = "relay_injection";

    /// <summary>An inbound message was rejected.</summary>
    public const string RelayRejection = "relay_rejection";

    /// <summary>An inbound message was delivered to your endpoint.</summary>
    public const string RelayDelivery = "relay_delivery";

    /// <summary>A temporary failure delivering an inbound message.</summary>
    public const string RelayTempfail = "relay_tempfail";

    /// <summary>A permanent failure delivering an inbound message.</summary>
    public const string RelayPermfail = "relay_permfail";

    /// <summary>An A/B test completed.</summary>
    public const string AbTestCompleted = "ab_test_completed";

    /// <summary>An A/B test was cancelled.</summary>
    public const string AbTestCancelled = "ab_test_cancelled";
}
