namespace SparkPoster;

/// <summary>
/// A stored template.
/// </summary>
/// <remarks>
/// A template can hold both a draft and a published version. Transmissions use the published
/// version by default, which is what lets you work on the next revision while the current one
/// keeps sending.
/// </remarks>
public sealed record Template
{
    /// <summary>The template identifier, used as <c>template_id</c> when sending.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>The template name.</summary>
    public string? Name { get; init; }

    /// <summary>A description of the template.</summary>
    public string? Description { get; init; }

    /// <summary>Whether the version returned is the published one.</summary>
    public bool? Published { get; init; }

    /// <summary>Whether the template has a draft version.</summary>
    public bool? HasDraft { get; init; }

    /// <summary>Whether the template has a published version.</summary>
    public bool? HasPublished { get; init; }

    /// <summary>Whether subaccounts may use this template.</summary>
    public bool? SharedWithSubaccounts { get; init; }

    /// <summary>The template content.</summary>
    public TemplateContent? Content { get; init; }

    /// <summary>Template-level sending options.</summary>
    public TemplateOptions? Options { get; init; }

    /// <summary>When the template was last changed.</summary>
    public DateTimeOffset? LastUpdateTime { get; init; }
}

/// <summary>Creating or updating a template.</summary>
public sealed record TemplateRequest
{
    /// <summary>
    /// The template identifier. Generated from <see cref="Name"/> when omitted, and
    /// immutable afterwards.
    /// </summary>
    public string? Id { get; init; }

    /// <summary>The template name.</summary>
    public string? Name { get; init; }

    /// <summary>A description of the template.</summary>
    public string? Description { get; init; }

    /// <summary>
    /// Whether to store the content as the published version. When <c>false</c> or omitted,
    /// the content is stored as a draft.
    /// </summary>
    public bool? Published { get; init; }

    /// <summary>Whether subaccounts may use this template.</summary>
    public bool? SharedWithSubaccounts { get; init; }

    /// <summary>The template content.</summary>
    public TemplateContent? Content { get; init; }

    /// <summary>Template-level sending options.</summary>
    public TemplateOptions? Options { get; init; }
}

/// <summary>The content of a template.</summary>
/// <remarks>
/// The template language works in <c>from</c>, <c>subject</c>, <c>text</c>, <c>html</c>,
/// <c>amp_html</c>, <c>reply_to</c> and the headers.
/// </remarks>
public sealed record TemplateContent
{
    /// <summary>The sender.</summary>
    public Address? From { get; init; }

    /// <summary>The subject line.</summary>
    public string? Subject { get; init; }

    /// <summary>The HTML part.</summary>
    public string? Html { get; init; }

    /// <summary>The plain text part.</summary>
    public string? Text { get; init; }

    /// <summary>The AMP part.</summary>
    public string? AmpHtml { get; init; }

    /// <summary>The reply-to address.</summary>
    public string? ReplyTo { get; init; }

    /// <summary>Additional message headers.</summary>
    public IReadOnlyDictionary<string, string>? Headers { get; init; }

    /// <summary>Attachments stored with the template.</summary>
    public IReadOnlyList<Attachment>? Attachments { get; init; }

    /// <summary>Inline images stored with the template.</summary>
    public IReadOnlyList<Attachment>? InlineImages { get; init; }

    /// <summary>A whole message in RFC822 format, as an alternative to the fields above.</summary>
    public string? EmailRfc822 { get; init; }
}

/// <summary>Template-level sending options.</summary>
public sealed record TemplateOptions
{
    /// <summary>Track opens.</summary>
    public bool? OpenTracking { get; init; }

    /// <summary>Track link clicks.</summary>
    public bool? ClickTracking { get; init; }

    /// <summary>Treat messages built from this template as transactional.</summary>
    public bool? Transactional { get; init; }
}
