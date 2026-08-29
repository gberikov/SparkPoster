using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace SparkPoster;

/// <summary>The entry point for assembling a transmission.</summary>
public static class Transmission
{
    /// <summary>Creates a transmission builder.</summary>
    /// <returns>A new builder.</returns>
    public static TransmissionBuilder Create() => new();
}

/// <summary>
/// Builds a transmission. Not thread-safe: one instance builds one message on one thread.
/// </summary>
/// <remarks>
/// <para>
/// The builder sends nothing: <see cref="Build"/> returns a <see cref="TransmissionRequest"/>
/// which you then hand to <see cref="ITransmissions.SendAsync"/>. The finished request can be
/// stored, serialized, or reused through <c>with</c>.
/// </para>
/// <para>
/// Content is set exactly one way: <see cref="Html"/>/<see cref="Text"/>,
/// <see cref="Template"/>, <see cref="AbTest"/> or <see cref="RawRfc822"/>. Mixing them is
/// detected in <see cref="Build"/>.
/// </para>
/// </remarks>
public sealed class TransmissionBuilder
{
    /// <summary>
    /// Substitution data is serialized without a naming policy: template variable names must
    /// stay exactly as the caller wrote them.
    /// </summary>
    private static readonly JsonSerializerOptions UserDataOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly List<Recipient> _recipients = [];
    private readonly List<Address> _cc = [];
    private readonly List<Address> _bcc = [];
    private readonly List<Attachment> _attachments = [];
    private readonly List<Attachment> _inlineImages = [];
    private Dictionary<string, string>? _headers;
    private TransmissionOptions _options = new();
    private ContentOverride? _override;
    private Address? _from;
    private string? _subject;
    private string? _html;
    private string? _text;
    private string? _ampHtml;
    private string? _replyTo;
    private string? _templateId;
    private bool? _useDraftTemplate;
    private string? _abTestId;
    private string? _rfc822;
    private string? _recipientListId;
    private string? _campaignId;
    private string? _description;
    private string? _returnPath;
    private string? _trackingDomain;
    private JsonNode? _substitutionData;
    private JsonNode? _metadata;

    /// <summary>Sets the sender.</summary>
    /// <param name="email">The sender address.</param>
    /// <param name="name">The display name.</param>
    /// <returns>The same builder.</returns>
    public TransmissionBuilder From(string email, string? name = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        _from = new Address { Email = email, Name = name };
        return this;
    }

    /// <summary>Adds a recipient.</summary>
    /// <param name="email">The recipient address.</param>
    /// <param name="name">The display name.</param>
    /// <returns>The same builder.</returns>
    public TransmissionBuilder To(string email, string? name = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        _recipients.Add(new Recipient { Address = new Address { Email = email, Name = name } });
        return this;
    }

    /// <summary>Adds a fully specified recipient, with its substitution data, metadata and tags.</summary>
    /// <param name="recipient">The recipient.</param>
    /// <returns>The same builder.</returns>
    public TransmissionBuilder To(Recipient recipient)
    {
        ArgumentNullException.ThrowIfNull(recipient);
        _recipients.Add(recipient);
        return this;
    }

    /// <summary>Adds several recipients.</summary>
    /// <param name="recipients">The recipients.</param>
    /// <returns>The same builder.</returns>
    public TransmissionBuilder To(IEnumerable<Recipient> recipients)
    {
        ArgumentNullException.ThrowIfNull(recipients);
        _recipients.AddRange(recipients);
        return this;
    }

    /// <summary>Adds a CC recipient.</summary>
    /// <param name="email">The CC address.</param>
    /// <param name="name">The display name.</param>
    /// <returns>The same builder.</returns>
    /// <remarks>
    /// SparkPost has no dedicated field for copies: a CC recipient is added to the ordinary
    /// recipient list with an overridden <c>To</c> header, and its address is appended to the
    /// <c>CC</c> header. <see cref="Build"/> does that for you.
    /// </remarks>
    public TransmissionBuilder Cc(string email, string? name = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        _cc.Add(new Address { Email = email, Name = name });
        return this;
    }

    /// <summary>Adds a BCC recipient.</summary>
    /// <param name="email">The BCC address.</param>
    /// <param name="name">The display name.</param>
    /// <returns>The same builder.</returns>
    /// <remarks>
    /// A BCC recipient is added to the ordinary recipient list with an overridden <c>To</c>
    /// header and, unlike a CC recipient, is never mentioned in any header.
    /// </remarks>
    public TransmissionBuilder Bcc(string email, string? name = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        _bcc.Add(new Address { Email = email, Name = name });
        return this;
    }

    /// <summary>Sends to a stored recipient list.</summary>
    /// <param name="listId">The list identifier.</param>
    /// <returns>The same builder.</returns>
    public TransmissionBuilder RecipientList(string listId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(listId);
        _recipientListId = listId;
        return this;
    }

    /// <summary>Sets the subject line. Supports the template language.</summary>
    /// <param name="subject">The subject.</param>
    /// <returns>The same builder.</returns>
    public TransmissionBuilder Subject(string subject)
    {
        ArgumentNullException.ThrowIfNull(subject);
        _subject = subject;
        return this;
    }

    /// <summary>Sets the HTML part of the message.</summary>
    /// <param name="html">The HTML content.</param>
    /// <returns>The same builder.</returns>
    public TransmissionBuilder Html(string html)
    {
        ArgumentNullException.ThrowIfNull(html);
        _html = html;
        return this;
    }

    /// <summary>Sets the plain text part of the message.</summary>
    /// <param name="text">The text content.</param>
    /// <returns>The same builder.</returns>
    public TransmissionBuilder Text(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        _text = text;
        return this;
    }

    /// <summary>Sets the AMP part of the message.</summary>
    /// <param name="ampHtml">The AMP content.</param>
    /// <returns>The same builder.</returns>
    public TransmissionBuilder AmpHtml(string ampHtml)
    {
        ArgumentNullException.ThrowIfNull(ampHtml);
        _ampHtml = ampHtml;
        return this;
    }

    /// <summary>Sends using a stored template.</summary>
    /// <param name="templateId">The template identifier.</param>
    /// <param name="useDraft">Use the draft instead of the published version.</param>
    /// <returns>The same builder.</returns>
    public TransmissionBuilder Template(string templateId, bool useDraft = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateId);
        _templateId = templateId;
        _useDraftTemplate = useDraft ? true : null;
        return this;
    }

    /// <summary>Sends the message as an A/B test.</summary>
    /// <param name="abTestId">The A/B test identifier.</param>
    /// <returns>The same builder.</returns>
    /// <remarks>A/B tests only support single-recipient transmissions.</remarks>
    public TransmissionBuilder AbTest(string abTestId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(abTestId);
        _abTestId = abTestId;
        return this;
    }

    /// <summary>Sends a ready-made RFC822 message.</summary>
    /// <param name="rfc822">The message content.</param>
    /// <returns>The same builder.</returns>
    public TransmissionBuilder RawRfc822(string rfc822)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rfc822);
        _rfc822 = rfc822;
        return this;
    }

    /// <summary>Adds an attachment.</summary>
    /// <param name="attachment">The attachment.</param>
    /// <returns>The same builder.</returns>
    public TransmissionBuilder Attach(Attachment attachment)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        _attachments.Add(attachment);
        return this;
    }

    /// <summary>Adds an inline image.</summary>
    /// <param name="image">The image; reference it from the HTML as <c>cid:</c> plus its name.</param>
    /// <returns>The same builder.</returns>
    public TransmissionBuilder InlineImage(Attachment image)
    {
        ArgumentNullException.ThrowIfNull(image);
        _inlineImages.Add(image);
        return this;
    }

    /// <summary>Sets the reply-to address.</summary>
    /// <param name="replyTo">The reply-to address.</param>
    /// <returns>The same builder.</returns>
    public TransmissionBuilder ReplyTo(string replyTo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(replyTo);
        _replyTo = replyTo;
        return this;
    }

    /// <summary>Adds a message header.</summary>
    /// <param name="name">The header name.</param>
    /// <param name="value">The header value.</param>
    /// <returns>The same builder.</returns>
    public TransmissionBuilder Header(string name, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);
        _headers ??= [];
        _headers[name] = value;
        return this;
    }

    /// <summary>Overrides fields of a stored template.</summary>
    /// <param name="contentOverride">The fields to override.</param>
    /// <returns>The same builder.</returns>
    public TransmissionBuilder Override(ContentOverride contentOverride)
    {
        ArgumentNullException.ThrowIfNull(contentOverride);
        _override = contentOverride;
        return this;
    }

    /// <summary>Sets the campaign identifier.</summary>
    /// <param name="campaignId">The campaign identifier.</param>
    /// <returns>The same builder.</returns>
    public TransmissionBuilder CampaignId(string campaignId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);
        _campaignId = campaignId;
        return this;
    }

    /// <summary>Sets the transmission description.</summary>
    /// <param name="description">The description.</param>
    /// <returns>The same builder.</returns>
    public TransmissionBuilder Description(string description)
    {
        ArgumentNullException.ThrowIfNull(description);
        _description = description;
        return this;
    }

    /// <summary>Sets the envelope FROM address.</summary>
    /// <param name="returnPath">The return path.</param>
    /// <returns>The same builder.</returns>
    public TransmissionBuilder ReturnPath(string returnPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(returnPath);
        _returnPath = returnPath;
        return this;
    }

    /// <summary>Sets the tracking domain used to wrap links.</summary>
    /// <param name="trackingDomain">A verified tracking domain.</param>
    /// <returns>The same builder.</returns>
    public TransmissionBuilder TrackingDomain(string trackingDomain)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trackingDomain);
        _trackingDomain = trackingDomain;
        return this;
    }

    /// <summary>
    /// Sets transmission-level substitution data from an arbitrary object.
    /// </summary>
    /// <param name="value">The data; property names are kept exactly as written.</param>
    /// <returns>The same builder.</returns>
    /// <remarks>
    /// Uses reflection, so it is unavailable in trimmed and AOT builds.
    /// Those have the <see cref="JsonTypeInfo{T}"/> overload instead.
    /// </remarks>
    [RequiresUnreferencedCode("Serializing an arbitrary object uses reflection. Use the JsonTypeInfo<T> overload instead.")]
    [RequiresDynamicCode("Serializing an arbitrary object uses reflection. Use the JsonTypeInfo<T> overload instead.")]
    public TransmissionBuilder SubstitutionData(object? value)
    {
        _substitutionData = JsonSerializer.SerializeToNode(value, UserDataOptions);
        return this;
    }

    /// <summary>
    /// Sets transmission-level substitution data. The overload for trimmed and AOT builds.
    /// </summary>
    /// <typeparam name="T">The data type.</typeparam>
    /// <param name="value">The data.</param>
    /// <param name="typeInfo">Type metadata from the caller's source-generated context.</param>
    /// <returns>The same builder.</returns>
    public TransmissionBuilder SubstitutionData<T>(T value, JsonTypeInfo<T> typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        _substitutionData = JsonSerializer.SerializeToNode(value, typeInfo);
        return this;
    }

    /// <summary>
    /// Sets transmission-level metadata from an arbitrary object.
    /// </summary>
    /// <param name="value">The metadata.</param>
    /// <returns>The same builder.</returns>
    /// <remarks>
    /// Uses reflection, so it is unavailable in trimmed and AOT builds.
    /// Those have the <see cref="JsonTypeInfo{T}"/> overload instead.
    /// </remarks>
    [RequiresUnreferencedCode("Serializing an arbitrary object uses reflection. Use the JsonTypeInfo<T> overload instead.")]
    [RequiresDynamicCode("Serializing an arbitrary object uses reflection. Use the JsonTypeInfo<T> overload instead.")]
    public TransmissionBuilder Metadata(object? value)
    {
        _metadata = JsonSerializer.SerializeToNode(value, UserDataOptions);
        return this;
    }

    /// <summary>
    /// Sets transmission-level metadata. The overload for trimmed and AOT builds.
    /// </summary>
    /// <typeparam name="T">The metadata type.</typeparam>
    /// <param name="value">The metadata.</param>
    /// <param name="typeInfo">Type metadata from the caller's source-generated context.</param>
    /// <returns>The same builder.</returns>
    public TransmissionBuilder Metadata<T>(T value, JsonTypeInfo<T> typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        _metadata = JsonSerializer.SerializeToNode(value, typeInfo);
        return this;
    }

    /// <summary>Sends through the sandbox domain.</summary>
    /// <param name="sandbox">Whether to use the sandbox.</param>
    /// <returns>The same builder.</returns>
    public TransmissionBuilder Sandbox(bool sandbox = true)
    {
        _options = _options with { Sandbox = sandbox };
        return this;
    }

    /// <summary>Marks the message as transactional.</summary>
    /// <param name="transactional">Whether the message is transactional.</param>
    /// <returns>The same builder.</returns>
    public TransmissionBuilder Transactional(bool transactional = true)
    {
        _options = _options with { Transactional = transactional };
        return this;
    }

    /// <summary>Controls open tracking.</summary>
    /// <param name="enabled">Whether to track opens.</param>
    /// <returns>The same builder.</returns>
    public TransmissionBuilder OpenTracking(bool enabled)
    {
        _options = _options with { OpenTracking = enabled };
        return this;
    }

    /// <summary>Controls click tracking.</summary>
    /// <param name="enabled">Whether to track clicks.</param>
    /// <returns>The same builder.</returns>
    public TransmissionBuilder ClickTracking(bool enabled)
    {
        _options = _options with { ClickTracking = enabled };
        return this;
    }

    /// <summary>Sets the IP pool to send through.</summary>
    /// <param name="ipPool">The pool identifier.</param>
    /// <returns>The same builder.</returns>
    public TransmissionBuilder IpPool(string ipPool)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ipPool);
        _options = _options with { IpPool = ipPool };
        return this;
    }

    /// <summary>Defers sending until the given moment.</summary>
    /// <param name="startTime">When to send; no more than three days ahead.</param>
    /// <returns>The same builder.</returns>
    public TransmissionBuilder StartTime(DateTimeOffset startTime)
    {
        _options = _options with { StartTime = startTime };
        return this;
    }

    /// <summary>Replaces the sending options wholesale.</summary>
    /// <param name="options">The sending options.</param>
    /// <returns>The same builder.</returns>
    public TransmissionBuilder WithOptions(TransmissionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        return this;
    }

    /// <summary>Assembles the request.</summary>
    /// <returns>The finished send request.</returns>
    /// <exception cref="InvalidOperationException">
    /// Recipients or content are missing, or content was given in more than one form.
    /// </exception>
    public TransmissionRequest Build()
    {
        var recipients = BuildRecipients();
        var content = BuildContent();

        return new TransmissionRequest
        {
            Content = content,
            Recipients = recipients,
            SubstitutionData = _substitutionData,
            Metadata = _metadata,
            Options = _options == new TransmissionOptions() ? null : _options,
            Override = _override,
            CampaignId = _campaignId,
            Description = _description,
            ReturnPath = _returnPath,
            TrackingDomain = _trackingDomain,
        };
    }

    private static string FormatAddress(Address address) =>
        string.IsNullOrEmpty(address.Name) ? address.Email : $"\"{address.Name}\" <{address.Email}>";

    private RecipientSet BuildRecipients()
    {
        if (_recipientListId is not null)
        {
            if (_recipients.Count > 0 || _cc.Count > 0 || _bcc.Count > 0)
            {
                throw new InvalidOperationException(
                    "Recipients were given twice: both as a stored list via RecipientList() and explicitly via To()/Cc()/Bcc().");
            }

            return RecipientSet.StoredList(_recipientListId);
        }

        if (_recipients.Count == 0)
        {
            throw new InvalidOperationException("No recipients were given: call To() or RecipientList().");
        }

        if (_cc.Count == 0 && _bcc.Count == 0)
        {
            return RecipientSet.Inline([.. _recipients]);
        }

        // In SparkPost, copies are ordinary recipients with an overridden To header.
        var headerTo = string.Join(", ", _recipients.Select(recipient => FormatAddress(recipient.Address)));

        var all = new List<Recipient>(_recipients.Count + _cc.Count + _bcc.Count);
        all.AddRange(_recipients);
        all.AddRange(_cc.Concat(_bcc).Select(address => new Recipient
        {
            Address = address with { HeaderTo = headerTo },
        }));

        return RecipientSet.Inline(all);
    }

    private TransmissionContent BuildContent()
    {
        var hasInline = _html is not null || _text is not null || _ampHtml is not null;
        var forms = new List<string>(4);

        if (hasInline)
        {
            forms.Add("inline content");
        }

        if (_templateId is not null)
        {
            forms.Add("a stored template");
        }

        if (_abTestId is not null)
        {
            forms.Add("an A/B test");
        }

        if (_rfc822 is not null)
        {
            forms.Add("RFC822");
        }

        if (forms.Count == 0)
        {
            throw new InvalidOperationException(
                "No content was given: call Html()/Text(), Template(), AbTest() or RawRfc822().");
        }

        if (forms.Count > 1)
        {
            throw new InvalidOperationException(
                $"Content was given in several forms at once ({string.Join(" and ", forms)}), but only one is allowed.");
        }

        if (hasInline && _from is null)
        {
            throw new InvalidOperationException("No sender was given: call From().");
        }

        var headers = _headers;

        if (_cc.Count > 0)
        {
            headers = headers is null ? [] : new Dictionary<string, string>(headers);
            headers["CC"] = string.Join(", ", _cc.Select(FormatAddress));
        }

        return new TransmissionContent
        {
            From = _from,
            Subject = _subject,
            Html = _html,
            Text = _text,
            AmpHtml = _ampHtml,
            ReplyTo = _replyTo,
            Headers = headers,
            Attachments = _attachments.Count > 0 ? [.. _attachments] : null,
            InlineImages = _inlineImages.Count > 0 ? [.. _inlineImages] : null,
            TemplateId = _templateId,
            UseDraftTemplate = _useDraftTemplate,
            AbTestId = _abTestId,
            EmailRfc822 = _rfc822,
        };
    }
}
