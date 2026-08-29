using System.Text.Json.Nodes;

namespace SparkPoster;

/// <summary>Managing stored templates.</summary>
public interface ITemplates
{
    /// <summary>Creates a template.</summary>
    /// <param name="definition">The template definition.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The identifier of the created template.</returns>
    Task<string> CreateAsync(TemplateRequest definition, CancellationToken cancellationToken = default);

    /// <summary>Returns a template.</summary>
    /// <param name="id">The template identifier.</param>
    /// <param name="draft">
    /// <c>true</c> for the draft, <c>false</c> for the published version. When omitted,
    /// SparkPost returns the draft if there is one.
    /// </param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The template.</returns>
    Task<Template> GetAsync(string id, bool? draft = null, CancellationToken cancellationToken = default);

    /// <summary>Returns every template.</summary>
    /// <param name="draft">Filters by whether a draft version exists.</param>
    /// <param name="sharedWithSubaccounts">Filters by whether the template is shared with subaccounts.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The templates, without their content.</returns>
    Task<IReadOnlyList<Template>> ListAsync(
        bool? draft = null,
        bool? sharedWithSubaccounts = null,
        CancellationToken cancellationToken = default);

    /// <summary>Updates a template.</summary>
    /// <param name="id">The template identifier.</param>
    /// <param name="definition">The new values.</param>
    /// <param name="updatePublished">
    /// <c>true</c> to edit the published version directly, leaving the draft alone.
    /// By default the draft is updated.
    /// </param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the template is updated.</returns>
    Task UpdateAsync(
        string id,
        TemplateRequest definition,
        bool updatePublished = false,
        CancellationToken cancellationToken = default);

    /// <summary>Publishes the draft.</summary>
    /// <param name="id">The template identifier.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the draft is published.</returns>
    /// <remarks>
    /// From this point on, transmissions that name this template send the new content —
    /// no change to the sending code is needed.
    /// </remarks>
    Task PublishAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Deletes a template.</summary>
    /// <param name="id">The template identifier.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the template is deleted.</returns>
    /// <remarks>
    /// A template that is in use by a scheduled transmission or an A/B test cannot be deleted:
    /// SparkPost answers with a 409.
    /// </remarks>
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Renders a template with the given substitution data.</summary>
    /// <param name="id">The template identifier.</param>
    /// <param name="substitutionData">The substitution data, or <c>null</c> to render without it.</param>
    /// <param name="draft"><c>true</c> to preview the draft rather than the published version.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The rendered content.</returns>
    Task<TemplateContent> PreviewAsync(
        string id,
        JsonNode? substitutionData = null,
        bool? draft = null,
        CancellationToken cancellationToken = default);
}
