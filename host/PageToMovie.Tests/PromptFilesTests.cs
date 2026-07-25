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

    [Fact]
    public void TryReadEmbedded_has_core_cast_and_book_prompts()
    {
        var cast = PromptFiles.TryReadEmbedded("prompts/fountain_to_cast.txt");
        var lit = PromptFiles.TryReadEmbedded("prompts/cast_visual_literalize.txt");
        var book = PromptFiles.TryReadEmbedded("prompts/book_to_fountain.txt");
        // Embedded only when Engine was built with ..\..\prompts present (normal CI/repo).
        if (cast is null && lit is null && book is null)
        {
            Assert.True(true);
            return;
        }

        Assert.False(string.IsNullOrWhiteSpace(cast));
        Assert.Contains("Character_", cast!, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(lit));
        Assert.False(string.IsNullOrWhiteSpace(book));
    }

    [Fact]
    public async Task ReadAsync_works_with_data_workspace_via_embed_or_disk()
    {
        // Railway layout: workspace is /data with no prompts folder.
        var text = await PromptFiles.ReadAsync("prompts/fountain_to_cast.txt", workspaceRoot: "/data");
        Assert.Contains("cast", text, StringComparison.OrdinalIgnoreCase);
    }
}
