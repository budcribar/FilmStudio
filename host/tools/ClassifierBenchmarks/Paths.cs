namespace ClassifierBenchmarks;

public sealed class BenchPaths
{
    public string RepoRoot { get; }
    public string Root { get; }
    public string Gold { get; }
    public string Prompts { get; }
    public string History { get; }
    public string Runs { get; }
    public string Reports { get; }
    public string HistoryIndex { get; }

    public BenchPaths(string repoRoot)
    {
        RepoRoot = repoRoot;
        Root = Path.Combine(repoRoot, "host", "evals", "classifier_benchmarks");
        Gold = Path.Combine(Root, "gold");
        Prompts = Path.Combine(Root, "prompts");
        History = Path.Combine(Root, "history");
        Runs = Path.Combine(History, "runs");
        Reports = Path.Combine(Root, "reports");
        HistoryIndex = Path.Combine(History, "index.json");
        Directory.CreateDirectory(Gold);
        Directory.CreateDirectory(Prompts);
        Directory.CreateDirectory(Runs);
        Directory.CreateDirectory(Reports);
    }

    public static string FindRepoRoot()
    {
        var d = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (d != null)
        {
            if (Directory.Exists(Path.Combine(d.FullName, "projects")) &&
                Directory.Exists(Path.Combine(d.FullName, "host")))
                return d.FullName;
            d = d.Parent;
        }
        return Directory.GetCurrentDirectory();
    }

    public string GoldFile(string projectId, string task) =>
        Path.Combine(Gold, projectId, task + ".json");

    public string PromptFile(string task, string promptId) =>
        Path.Combine(Prompts, task, promptId + ".txt");

    public string PromptMeta(string task, string promptId) =>
        Path.Combine(Prompts, task, promptId + ".meta.json");
}
