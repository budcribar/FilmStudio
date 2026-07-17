using FilmStudio.Engine;
using FilmStudio.Engine.Abstractions;
using Microsoft.Extensions.Logging;

namespace FilmStudio.Fakes;

public sealed class FakeGrokVisionClient : IGrokVisionClient
{
    private readonly ILogger<FakeGrokVisionClient> _log;

    public FakeGrokVisionClient(ILogger<FakeGrokVisionClient> log) => _log = log;

    public bool IsConfigured => true;

    public Task<string> TranscribePageAsync(
        string imagePath,
        int page,
        string model = "grok-4.5",
        CancellationToken ct = default)
    {
        _log.LogInformation("Fake vision transcribe page={Page}", page);
        return Task.FromResult("(illustration only)");
    }

    public Task<CharacterPageClassification> ClassifyCharactersOnImageAsync(
        string imagePath,
        int page,
        IReadOnlyList<CharacterClassifyHint> cast,
        string model = "grok-4.5",
        CancellationToken ct = default)
    {
        _log.LogInformation("Fake vision classify page={Page} cast={N}", page, cast.Count);
        return Task.FromResult(new CharacterPageClassification
        {
            Page = page,
            PageKind = "illustration",
            Matches = new List<CharacterPageMatch>(),
        });
    }
}
