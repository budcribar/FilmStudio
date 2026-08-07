using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PageToMovie.Engine.Collaboration;

namespace PageToMovie.Api.Collaboration;

public static class MergeEndpoints
{
    public static IEndpointRouteBuilder MapMergeEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/merge");
        g.MapPost("/text", MergeText);
        g.MapPost("/json", MergeJson);
        return app;
    }

    static IResult MergeText(MergeTextRequest body, IAutoProjectMerger merger)
    {
        if (body is null) return Results.BadRequest(new { error = "body required" });
        var strategy = ParseStrategy(body.Strategy);
        var outcome = merger.MergeText(body.Base, body.Ours, body.Theirs, strategy);
        return Results.Ok(new
        {
            mergedText = outcome.MergedText,
            hasConflicts = outcome.HasConflicts,
            autoResolvedCount = outcome.AutoResolvedCount,
            conflictCount = outcome.Conflicts.Count,
            conflicts = outcome.Conflicts.Select(h => new
            {
                baseStartLine = h.BaseStartLine,
                baseLines = h.BaseLines,
                oursLines = h.OursLines,
                theirsLines = h.TheirsLines,
            }),
        });
    }

    static IResult MergeJson(MergeJsonRequest body, IAutoProjectMerger merger)
    {
        if (body is null) return Results.BadRequest(new { error = "body required" });
        try
        {
            using var oursDoc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body.Ours) ? "{}" : body.Ours);
            using var theirsDoc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body.Theirs) ? "{}" : body.Theirs);
            JsonElement? baseEl = null;
            JsonDocument? baseDoc = null;
            if (!string.IsNullOrWhiteSpace(body.Base))
            {
                baseDoc = JsonDocument.Parse(body.Base);
                baseEl = baseDoc.RootElement;
            }
            var strategy = ParseStrategy(body.Strategy);
            var result = merger.MergeJsonObjects(baseEl, oursDoc.RootElement, theirsDoc.RootElement, strategy);
            baseDoc?.Dispose();
            return Results.Ok(new
            {
                merged = result.Merged,
                hasConflicts = result.HasConflicts,
                autoResolvedCount = result.AutoResolvedCount,
                conflictPaths = result.ConflictPaths,
            });
        }
        catch (JsonException ex)
        {
            return Results.BadRequest(new { error = "invalid JSON: " + ex.Message });
        }
    }

    static AutoTextMerger.Strategy ParseStrategy(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return AutoTextMerger.Strategy.Auto;
        return Enum.TryParse<AutoTextMerger.Strategy>(s, ignoreCase: true, out var v)
            ? v : AutoTextMerger.Strategy.Auto;
    }

    public sealed record MergeTextRequest(string? Base, string? Ours, string? Theirs, string? Strategy = null);
    public sealed record MergeJsonRequest(string? Base, string? Ours, string? Theirs, string? Strategy = null);
}
