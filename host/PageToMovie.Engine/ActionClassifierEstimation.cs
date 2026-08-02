using System.Text.Json.Serialization;

namespace PageToMovie.Engine;

public sealed record ActionClassifierEstimation(
    [property: JsonPropertyName("matchCategoryId")] string MatchCategoryId,
    [property: JsonPropertyName("estimatedOverheadSec")] double EstimatedOverheadSec,
    [property: JsonPropertyName("confidenceScore")] double ConfidenceScore,
    [property: JsonPropertyName("explanation")] string Explanation);
