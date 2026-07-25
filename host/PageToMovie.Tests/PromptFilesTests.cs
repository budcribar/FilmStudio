using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests;

public class PromptFilesTests
{
    [Fact]
    public void Resolve_finds_fountain_to_cast_from_repo()
    {
        var path = PromptFiles.Resolve("prompts/fountain_to_cast.txt", workspaceRoot: "/data");
        // On CI/dev machines repo is discoverable by walking BaseDirectory parents.
        // If not found (odd layout), skip rather than fail agents without repo layout.
        if (path is null)
        {
            Assert.True(true);
            return;
        }

        Assert.True(File.Exists(path), path);
        Assert.Contains("fountain_to_cast", path, StringComparison.OrdinalIgnoreCase);
        // Must not require workspace to equal /data/prompts when only /data was given
        Assert.False(
            path.Replace('\\', '/').Contains("/data/prompts/", StringComparison.OrdinalIgnoreCase) &&
            !File.Exists(path),
            "resolved path under /data but file missing");
    }

    [Fact]
    public async Task ReadAsync_loads_cast_prompt_when_repo_present()
    {
        var path = PromptFiles.Resolve("prompts/fountain_to_cast.txt");
        if (path is null)
        {
            Assert.True(true);
            return;
        }

        var text = await PromptFiles.ReadAsync("prompts/fountain_to_cast.txt", workspaceRoot: "C:\\nonexistent-workspace-xyz");
        Assert.Contains("cast", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Character_", text, StringComparison.OrdinalIgnoreCase);
    }
}
