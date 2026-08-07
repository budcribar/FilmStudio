using System.Text.Json;
using PageToMovie.Engine.Collaboration;
using Xunit;

namespace PageToMovie.Tests.Collaboration;

public sealed class AutoTextMergerTests
{
    [Fact] public void Identical_sides_no_conflict()
    {
        var r = AutoTextMerger.Merge("a\nb", "a\nb", "a\nb");
        Assert.False(r.HasConflicts); Assert.Equal("a\nb", r.MergedText);
    }
    [Fact] public void Only_ours_changed_takes_ours()
    {
        var r = AutoTextMerger.Merge("base\nline", "base\nours", "base\nline");
        Assert.False(r.HasConflicts); Assert.Equal("base\nours", r.MergedText);
    }
    [Fact] public void Only_theirs_changed_takes_theirs()
    {
        var r = AutoTextMerger.Merge("base\nline", "base\nline", "base\ntheirs");
        Assert.False(r.HasConflicts); Assert.Equal("base\ntheirs", r.MergedText);
    }
    [Fact] public void Non_overlapping_changes_auto_merge()
    {
        var r = AutoTextMerger.Merge("A\nB\nC\nD", "A\nB-ours\nC\nD", "A\nB\nC\nD-theirs");
        Assert.False(r.HasConflicts);
        Assert.Contains("B-ours", r.MergedText); Assert.Contains("D-theirs", r.MergedText);
    }
    [Fact] public void Overlapping_change_emits_markers_in_Auto()
    {
        var r = AutoTextMerger.Merge("same\nconflict\nsame", "same\nours-val\nsame", "same\ntheirs-val\nsame");
        Assert.True(r.HasConflicts); Assert.Contains("<<<<<<< ours", r.MergedText);
    }
    [Fact] public void PreferOurs_resolves_overlap()
    {
        var r = AutoTextMerger.Merge("x\ny\nz", "x\nours\nz", "x\ntheirs\nz", AutoTextMerger.Strategy.PreferOurs);
        Assert.Contains("ours", r.MergedText);
        Assert.DoesNotContain("<<<<<<<", r.MergedText);
    }
    [Fact] public void PreferTheirs_resolves_overlap()
    {
        var r = AutoTextMerger.Merge("x\ny\nz", "x\nours\nz", "x\ntheirs\nz", AutoTextMerger.Strategy.PreferTheirs);
        Assert.False(r.HasConflicts); Assert.Equal("x\ntheirs\nz", r.MergedText);
    }
    [Fact] public void Union_appends_non_overlapping_lines()
    {
        var r = AutoTextMerger.Merge("a", "a\nb", "a\nc", AutoTextMerger.Strategy.Union);
        Assert.False(r.HasConflicts); Assert.Contains("b", r.MergedText); Assert.Contains("c", r.MergedText);
    }
    [Fact] public void Json_objects_merge_non_conflicting_keys()
    {
        var merger = new AutoProjectMerger();
        using var b = JsonDocument.Parse("""{"a":1,"b":2}""");
        using var o = JsonDocument.Parse("""{"a":1,"b":20,"c":3}""");
        using var th = JsonDocument.Parse("""{"a":1,"b":2,"d":4}""");
        var r = merger.MergeJsonObjects(b.RootElement, o.RootElement, th.RootElement);
        Assert.False(r.HasConflicts);
        Assert.True(r.Merged.TryGetProperty("c", out _)); Assert.True(r.Merged.TryGetProperty("d", out _));
    }
    [Fact] public void Json_conflicting_key_with_Auto_reports_conflict()
    {
        var merger = new AutoProjectMerger();
        using var b = JsonDocument.Parse("""{"k":"base"}""");
        using var o = JsonDocument.Parse("""{"k":"ours"}""");
        using var th = JsonDocument.Parse("""{"k":"theirs"}""");
        var r = merger.MergeJsonObjects(b.RootElement, o.RootElement, th.RootElement);
        Assert.True(r.HasConflicts); Assert.Contains("k", r.ConflictPaths);
    }
    [Fact] public void Json_PreferTheirs_on_conflict()
    {
        var merger = new AutoProjectMerger();
        using var b = JsonDocument.Parse("""{"k":"base"}""");
        using var o = JsonDocument.Parse("""{"k":"ours"}""");
        using var th = JsonDocument.Parse("""{"k":"theirs"}""");
        var r = merger.MergeJsonObjects(b.RootElement, o.RootElement, th.RootElement, AutoTextMerger.Strategy.PreferTheirs);
        Assert.False(r.HasConflicts);
        Assert.Equal("theirs", r.Merged.GetProperty("k").GetString());
    }
    [Fact] public async Task MergeTextFiles_writes_output()
    {
        var root = Path.Combine(Path.GetTempPath(), "ptm-m-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var bas = Path.Combine(root, "b.txt"); var o = Path.Combine(root, "o.txt");
            var th = Path.Combine(root, "t.txt"); var outp = Path.Combine(root, "out.txt");
            await File.WriteAllTextAsync(bas, "A\nB\nC");
            await File.WriteAllTextAsync(o, "A\nB1\nC");
            await File.WriteAllTextAsync(th, "A\nB\nC1");
            var r = await new AutoProjectMerger().MergeTextFilesAsync(bas, o, th, outp);
            Assert.True(r.Success); Assert.False(r.HasConflicts);
            var text = await File.ReadAllTextAsync(outp);
            Assert.Contains("B1", text); Assert.Contains("C1", text);
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }
}
