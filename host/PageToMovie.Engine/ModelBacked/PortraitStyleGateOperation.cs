using PageToMovie.Engine.Abstractions;
using PageToMovie.Engine.ModelExecution;

namespace PageToMovie.Engine.ModelBacked;

/// <summary>
/// The vision-call sibling of <see cref="Stage2DirectiveOperation"/> — same corrective-retry
/// shape (inject the previous response + validation issues on a correction attempt), but calls
/// <see cref="IVisionClient.CompleteWithImagesAsync"/> with an attached image instead of
/// <see cref="IChatClient.CompleteAsync"/>.
/// </summary>
internal sealed record PortraitStyleGateInput(string Prompt, string ImagePath, string Model, string Detail);

internal sealed class PortraitStyleGateOperation(IVisionClient vision, string name, string version)
    : IModelOperation<PortraitStyleGateInput, string>
{
    public string OperationName => name;
    public string PromptVersion => version;

    public async Task<ModelResponse<string>> ExecuteAsync(
        PortraitStyleGateInput input, ModelAttemptContext<string> context, CancellationToken ct)
    {
        var prompt = input.Prompt;
        if (context.Kind == ModelAttemptKind.Correction)
            prompt += "\n\nCORRECT THE PREVIOUS RESPONSE. Return complete JSON only.\n" +
                      string.Join("\n", context.ValidationIssues.Select(i => $"- {i.Path ?? "$"}: {i.Message}")) +
                      "\nPrevious response:\n" + context.PreviousResponse;
        var raw = await vision.CompleteWithImagesAsync(
            prompt, new[] { input.ImagePath }, input.Model, input.Detail, ct).ConfigureAwait(false);
        return new(raw, input.Model);
    }
}

/// <summary>Adapts <see cref="CharacterDesignService.ParsePortraitStyleGateResponse"/> (unchanged,
/// still directly unit-tested) into the shared parse-result shape.</summary>
internal sealed class PortraitStyleGateResponseParser : IModelResponseParser<string, CharacterDesignService.PortraitStyleGateResult>
{
    public ModelParseResult<CharacterDesignService.PortraitStyleGateResult> Parse(string response)
    {
        var parsed = CharacterDesignService.ParsePortraitStyleGateResponse(response);
        return parsed is not null
            ? ModelParseResult<CharacterDesignService.PortraitStyleGateResult>.Success(parsed)
            : ModelParseResult<CharacterDesignService.PortraitStyleGateResult>.Failure(
                new ModelValidationIssue("invalid_json", "Style check returned an unreadable response.", "$"));
    }
}

/// <summary>Rejects a medium the parser didn't recognize — nothing checked this before; a model
/// hallucinating an unlisted medium value would previously sail through as a parsed result.</summary>
internal sealed class PortraitStyleGateValidator : IModelResultValidator<CharacterDesignService.PortraitStyleGateResult>
{
    private static readonly HashSet<string> KnownMedia =
        new(StringComparer.OrdinalIgnoreCase) { "photoreal", "illustration", "sketch", "other" };

    public IReadOnlyList<ModelValidationIssue> Validate(CharacterDesignService.PortraitStyleGateResult result) =>
        KnownMedia.Contains(result.Medium)
            ? Array.Empty<ModelValidationIssue>()
            : [new ModelValidationIssue("invalid_medium",
                $"medium '{result.Medium}' is not one of photoreal|illustration|sketch|other.", "$.medium")];
}
