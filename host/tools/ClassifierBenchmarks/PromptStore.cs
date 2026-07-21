using System.Text.Json;

namespace ClassifierBenchmarks;

public sealed record PromptBundle(string Id, string Text, string Label, string Hash, string? Notes);

public static class PromptStore
{
    public static PromptBundle Load(BenchPaths paths, string task, string promptId)
    {
        var path = paths.PromptFile(task, promptId);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Prompt not found: {path}. Add host/evals/classifier_benchmarks/prompts/{task}/{promptId}.txt");

        var text = File.ReadAllText(path).Trim();
        var label = promptId;
        string? notes = null;
        var metaPath = paths.PromptMeta(task, promptId);
        if (File.Exists(metaPath))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(metaPath));
                if (doc.RootElement.TryGetProperty("label", out var l))
                    label = l.GetString() ?? label;
                if (doc.RootElement.TryGetProperty("notes", out var n))
                    notes = n.GetString();
            }
            catch { /* optional meta */ }
        }

        return new PromptBundle(promptId, text, label, ChatRunner.Sha256Short(text), notes);
    }

    public static IEnumerable<string> ListPromptIds(BenchPaths paths, string task)
    {
        var dir = Path.Combine(paths.Prompts, task);
        if (!Directory.Exists(dir)) yield break;
        foreach (var f in Directory.GetFiles(dir, "*.txt").OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            yield return Path.GetFileNameWithoutExtension(f);
    }
}
