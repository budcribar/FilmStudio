using FilmStudio.Engine.Abstractions;
using Microsoft.Extensions.Logging;

namespace FilmStudio.Fakes;

/// <summary>Returns minimal valid-looking JSON for Stage 1 style prompts.</summary>
public sealed class FakeGrokChatClient : IGrokChatClient
{
    private readonly ILogger<FakeGrokChatClient> _log;

    public FakeGrokChatClient(ILogger<FakeGrokChatClient> log) => _log = log;

    public bool IsConfigured => true;

    public Task<string> CompleteAsync(
        string systemPrompt,
        string userPrompt,
        string model = "grok-4.5",
        double temperature = 0.2,
        CancellationToken ct = default)
    {
        _log.LogInformation("Fake chat complete model={Model} userLen={Len}", model, userPrompt.Length);
        // Minimal Stage1-shaped stub — real Stage1 may need richer JSON; fakes for wiring only
        const string json = """
            {
              "global_production_variables": {
                "character_seed_tokens": {},
                "location_seed_tokens": {}
              },
              "scenes": []
            }
            """;
        return Task.FromResult(json);
    }
}
