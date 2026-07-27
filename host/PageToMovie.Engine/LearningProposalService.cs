using System.Text;
using PageToMovie.Core.Models;
using PageToMovie.Engine.Abstractions;
using Microsoft.Extensions.Logging;

namespace PageToMovie.Engine;

/// <summary>Admin-gated: propose prompt/rule text from recent fail events (chat).</summary>
public sealed class LearningProposalService
{
    private readonly ReviewEventStore _learning;
    private readonly IChatClient _chat;
    private readonly ILogger<LearningProposalService> _log;

    public LearningProposalService(
        ReviewEventStore learning,
        IChatClient chat,
        ILogger<LearningProposalService> log)
    {
        _learning = learning;
        _chat = chat;
        _log = log;
    }

    public async Task<ProposeLearningRulesResult> ProposeAsync(
        ProposeLearningRulesRequest req,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);
        var n = Math.Clamp(req.LastNFails <= 0 ? 50 : req.LastNFails, 5, 200);
        // Scan full log then filter fails — Query(take:N) of mixed events can bury fails under passes
        var fails = _learning.ReadAll()
            .Where(e =>
                string.IsNullOrWhiteSpace(req.ProjectId) ||
                string.Equals(e.ProjectId, req.ProjectId, StringComparison.OrdinalIgnoreCase))
            .Where(e =>
                string.Equals(e.Type, "clip_fail", StringComparison.OrdinalIgnoreCase) ||
                (string.Equals(e.Type, "auto_review", StringComparison.OrdinalIgnoreCase) &&
                 string.Equals(e.Suggestion, "fail", StringComparison.OrdinalIgnoreCase)))
            .Where(e => string.IsNullOrWhiteSpace(req.Category) ||
                        string.Equals(e.Category, req.Category, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(e => e.Ts)
            .Take(n)
            .ToList();

        if (fails.Count == 0)
        {
            return new ProposeLearningRulesResult
            {
                Ok = false,
                Error = "No fail events found for the filters.",
                FailEventsUsed = 0,
            };
        }

        var cats = fails
            .Select(f => string.IsNullOrWhiteSpace(f.Category) ? "other" : f.Category!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("Recent film QC fails (newest first):");
        var i = 0;
        foreach (var f in fails)
        {
            i++;
            sb.AppendLine(
                $"{i}. [{f.Type}] project={f.ProjectId} S{f.Scene:D2}C{f.Clip:D2} " +
                $"cat={f.Category ?? "?"} note={Trim(f.Note, 50)}");
            if (!string.IsNullOrWhiteSpace(f.Before) || !string.IsNullOrWhiteSpace(f.After))
                sb.AppendLine($"   before/after present: beforeLen={f.Before?.Length ?? 0} afterLen={f.After?.Length ?? 0}");
        }

        var system =
            "You help improve a film generation pipeline. From QC fail notes, propose 3–7 concise " +
            "house rules for video prompt construction (and auto-review checks). " +
            "Output plain text bullet list only. No markdown fences. Each bullet one sentence. " +
            "Do not invent book-specific plot; keep rules general and actionable.";

        if (!_chat.IsConfigured)
        {
            // Deterministic offline proposal for tests / no key
            var offline = string.Join("\n", cats.Select(c =>
                $"- Strengthen checks and gen guidance for category '{c}' based on {fails.Count} recent fails."));
            return new ProposeLearningRulesResult
            {
                Ok = true,
                Proposal = offline + "\n- Prefer continuity from previous clip tail; flag jumps as fail when clear.",
                FailEventsUsed = fails.Count,
                Categories = cats,
            };
        }

        try
        {
            var proposal = await _chat.CompleteAsync(
                    system, sb.ToString(), model: "grok-4.5", temperature: 0.3, ct,
                    mode: ChatCallModes.LearningPropose)
                .ConfigureAwait(false);
            return new ProposeLearningRulesResult
            {
                Ok = true,
                Proposal = proposal.Trim(),
                FailEventsUsed = fails.Count,
                Categories = cats,
            };
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Propose learning rules failed");
            return new ProposeLearningRulesResult
            {
                Ok = false,
                Error = ex.Message,
                FailEventsUsed = fails.Count,
                Categories = cats,
            };
        }
    }

    public async Task<ReviewComparisonInsightsDto> SynthesizePromptImprovementsAsync(
        string? projectId = null,
        CancellationToken ct = default)
    {
        var insights = _learning.GetReviewComparison(projectId);
        var gaps = insights.Discrepancies
            .Where(d => d.DiscrepancyType != "AGREEMENT")
            .Take(30)
            .ToList();

        if (gaps.Count == 0)
        {
            insights.PromptImprovementProposal = "No discrepancies found between Human and AI reviews yet. As operators review clips, differences will be tracked here.";
            return insights;
        }

        var sb = new StringBuilder();
        sb.AppendLine("Discrepancies between Human Reviews and AI Auto-Reviews:");
        var idx = 0;
        foreach (var g in gaps)
        {
            idx++;
            sb.AppendLine($"{idx}. [{g.DiscrepancyType}] project={g.ProjectId} S{g.SceneNumber:D2}C{g.ClipNumber:D2}");
            sb.AppendLine($"   Human Verdict: {g.HumanVerdict.ToUpper()} | Human Note: {g.Note}");
            sb.AppendLine($"   AI Verdict: {g.AiVerdict.ToUpper()} (Score: {g.AiScore}/10) | AI Reasoning: {g.AiReasoning}");
        }

        var systemPrompt =
            "You are an expert AI prompt engineer optimizing automated film vision review prompts. " +
            "Compare human director reviews against AI auto-review verdicts. " +
            "Identify why the AI missed human quality expectations (e.g. AI too permissive) or penalized acceptable clips (e.g. AI too strict). " +
            "Output plain text bullet recommendations for updating system prompts in ClipAutoReviewService and MovieAutoReviewService to align AI judgment with human directors.";

        if (!_chat.IsConfigured)
        {
            insights.PromptImprovementProposal =
                "- [AI Too Permissive]: Require explicit verification of character wardrobe/costume lock across scene cuts.\n" +
                "- [AI Too Strict]: Allow subtle lighting shifts between angles if primary subject remains clear.\n" +
                "- [General]: Update auto-review prompt to weight action continuity higher than minor background rendering quirks.";
            return insights;
        }

        try
        {
            var proposal = await _chat.CompleteAsync(
                systemPrompt,
                sb.ToString(),
                model: "grok-4.5",
                temperature: 0.3,
                ct: ct,
                mode: ChatCallModes.LearningPropose).ConfigureAwait(false);

            insights.PromptImprovementProposal = proposal.Trim();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to synthesize prompt improvements from review discrepancies");
            insights.PromptImprovementProposal = "Error generating prompt recommendations: " + ex.Message;
        }

        return insights;
    }

    // Token-accurate now (was raw character count) — see PromptTokenizer.
    private static string Trim(string? s, int maxTokens) => PromptTokenizer.TruncateToTokens(s, maxTokens);
}
