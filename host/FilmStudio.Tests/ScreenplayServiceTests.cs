using FilmStudio.Core.Options;
using FilmStudio.Engine;
using Microsoft.Extensions.Options;
using Xunit;

namespace FilmStudio.Tests;

public class ScreenplayServiceTests : IDisposable
{
    private readonly string _root;
    private readonly ProjectStore _store;

    public ScreenplayServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "fs-screenplay-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "projects", "Demo"));
        var opts = Options.Create(new FilmStudioOptions
        {
            WorkspaceRoot = _root,
        });
        _store = new ProjectStore(opts);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch { /* ignore */ }
    }

    [Fact]
    public void Save_draft_then_sign_off_materialises_stage1()
    {
        const string projectId = "Demo";
        var fountain = """
            Title: Test Script
            Author: Unit Test

            INT. LAB - DAY

            A scientist adjusts a dial.

            SCIENTIST
            Almost there.

            EXT. ROOF - NIGHT

            Rain.
            """;

        var save = ScreenplayService.SaveDraft(_store, projectId, fountain);
        Assert.True(save.Ok);
        Assert.True(save.Status.DraftExists);
        Assert.True(save.Status.Dirty);
        Assert.False(save.Status.Signed);
        Assert.True(save.Status.SceneHeadingCount >= 2);

        var sign = ScreenplayService.SignOff(_store, projectId);
        Assert.True(sign.Ok, sign.Error);
        Assert.Equal(2, sign.SceneCount);
        Assert.True(sign.Status.Signed);
        Assert.False(sign.Status.Dirty);
        Assert.True(sign.Status.ReadyForShots);
        Assert.True(sign.HashChanged);

        var scenesPath = _store.ResolveScenesJsonPath(projectId);
        Assert.True(File.Exists(scenesPath));
        Assert.Contains("Test Script", File.ReadAllText(scenesPath), StringComparison.OrdinalIgnoreCase);

        // Second sign-off with no edits: hash not changed
        var sign2 = ScreenplayService.SignOff(_store, projectId);
        Assert.True(sign2.Ok);
        Assert.False(sign2.HashChanged);
    }

    [Fact]
    public void Edit_after_sign_off_marks_dirty_and_blocks_ready_until_reapprove()
    {
        const string projectId = "Demo";
        ScreenplayService.SaveDraft(_store, projectId, "INT. ROOM - DAY\n\nHello.\n");
        var sign = ScreenplayService.SignOff(_store, projectId);
        Assert.True(sign.Ok);
        Assert.True(sign.Status.ReadyForShots);

        ScreenplayService.SaveDraft(_store, projectId, "INT. ROOM - DAY\n\nHello world.\n");
        var status = ScreenplayService.Get(_store, projectId).Status;
        Assert.True(status.Dirty);
        Assert.False(status.Signed);
        // Stage 1 still on disk from previous sign-off, but ReadyForShots requires signed draft
        Assert.False(status.ReadyForShots);
    }

    [Fact]
    public void Import_as_draft_does_not_write_stage1()
    {
        const string projectId = "Demo";
        var r = ScreenplayService.ImportAsDraft(_store, projectId, "INT. A - DAY\n\nAction.\n", "mine.fountain");
        Assert.True(r.Ok);
        Assert.True(r.Status.DraftExists);
        var scenesPath = _store.ResolveScenesJsonPath(projectId);
        Assert.False(File.Exists(scenesPath));
    }

    [Fact]
    public void Create_draft_from_book_wraps_prose()
    {
        const string projectId = "Demo";
        var source = Path.Combine(_store.GetProjectDir(projectId), "source");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "book_full.txt"),
            "Chapter one.\n\nOnce upon a time there was a dog who loved naps.\n\nThe end.");

        var r = ScreenplayService.CreateDraftFromBook(_store, projectId);
        Assert.True(r.Ok, r.Error);
        Assert.True(r.Status.DraftExists);
        var text = ScreenplayService.Get(_store, projectId).Text;
        Assert.Contains("Title:", text);
        Assert.Contains("INT.", text);
        Assert.Contains("dog who loved naps", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Adaptation_status_next_step_sign_screenplay_when_draft_dirty()
    {
        const string projectId = "Demo";
        ScreenplayService.SaveDraft(_store, projectId, "INT. A - DAY\n\nX.\n");
        var status = _store.GetAdaptationStatus(projectId);
        Assert.Equal("sign_screenplay", status.NextStep);
        Assert.True(status.Screenplay.DraftExists);
        Assert.True(status.Screenplay.Dirty);
    }

    [Fact]
    public void BookTextToFountainDraft_is_valid_enough_to_parse()
    {
        var f = ScreenplayService.BookTextToFountainDraft("My Book", "Para one.\n\nPara two.");
        var parsed = FountainParser.Parse(f);
        Assert.True(parsed.TitlePage.ContainsKey("Title"));
        Assert.Contains(parsed.Elements, e => e.Type == FountainParser.ElementType.SceneHeading);
        Assert.Contains(parsed.Elements, e => e.Type == FountainParser.ElementType.Action);
    }
}
