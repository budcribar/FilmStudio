using PageToMovie.Adaptation.Contracts;
using PageToMovie.Adaptation.Conversion;
using Xunit;

namespace PageToMovie.Tests;

public sealed class AdaptationReportParserTests
{
    private const string SampleJson = """
        {"source_complete":"yes","metrics":{"scenes":4,"speaking_cast":3,"body_words":420,"est_runtime_min":2.7},"issues":[{"type":"runtime_pressure","severity":"minor","where":"EXT. LANE","detail":"Felt short.","resolution":"Kept natural length."}],"spec_feedback":["Scene band was ambiguous for rhymes."]}
        """;

    [Fact]
    public void ParseModelJson_reads_metrics_and_issues()
    {
        var report = AdaptationReportParser.ParseModelJson(SampleJson);
        Assert.NotNull(report);
        Assert.Equal("yes", report!.SourceComplete);
        Assert.Equal(4, report.Metrics.Scenes);
        Assert.Equal(3, report.Metrics.SpeakingCast);
        Assert.Equal(420, report.Metrics.BodyWords);
        Assert.Equal(2.7, report.Metrics.EstRuntimeMin, 1);
        Assert.Single(report.Issues);
        Assert.Equal("runtime_pressure", report.Issues[0].Type);
        Assert.Single(report.SpecFeedback);
    }

    [Fact]
    public void ParseModelJson_rejects_garbage()
    {
        Assert.Null(AdaptationReportParser.ParseModelJson("not json"));
        Assert.Null(AdaptationReportParser.ParseModelJson(""));
        Assert.Null(AdaptationReportParser.ParseModelJson(null));
    }

    [Fact]
    public void SplitAdaptationTrailers_strips_both_sidecars()
    {
        var raw = """
            Title: Mary

            FADE IN:

            EXT. LANE - DAY

            MARY walks with a lamb.

            > FADE OUT.

            THE END

            ---VISION_META---
            {"visual_medium":"illustrated_picture_book","render_style_lock":"STYLE LOCK: watercolor","notes":"rhyme"}
            ---END_VISION_META---
            ---ADAPTATION_REPORT---
            {"source_complete":"yes","metrics":{"scenes":1,"speaking_cast":1,"body_words":12,"est_runtime_min":0.1},"issues":[],"spec_feedback":[]}
            ---END_ADAPTATION_REPORT---
            """;

        var (fountain, vision, report) = BookToFountainConverter.SplitAdaptationTrailers(raw);

        Assert.DoesNotContain("VISION_META", fountain, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ADAPTATION_REPORT", fountain, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FADE IN", fountain, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(vision);
        Assert.Equal("illustrated_picture_book", vision!.VisualMedium);
        Assert.NotNull(report);
        Assert.Equal("yes", report!.SourceComplete);
        Assert.Equal(1, report.Metrics.Scenes);
    }

    [Fact]
    public void SplitAdaptationTrailers_report_only_still_strips()
    {
        var raw = """
            Title: X

            FADE IN:

            INT. ROOM - DAY

            Action line here that is long enough.

            NICK
            Hello there friend of mine.

            > FADE OUT.

            THE END

            ---ADAPTATION_REPORT---
            {"source_complete":"uncertain","metrics":{"scenes":1,"speaking_cast":1,"body_words":20,"est_runtime_min":0.2},"issues":[],"spec_feedback":[]}
            ---END_ADAPTATION_REPORT---
            """;

        var (fountain, vision, report) = BookToFountainConverter.SplitAdaptationTrailers(raw);
        Assert.Null(vision);
        Assert.NotNull(report);
        Assert.Equal("uncertain", report!.SourceComplete);
        Assert.DoesNotContain("ADAPTATION_REPORT", fountain, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SplitVisionMetaTrailer_does_not_leak_report_into_fountain()
    {
        var raw = """
            Title: X

            FADE IN:

            INT. ROOM - DAY

            Enough action text for a grounding line in the scene.

            > FADE OUT.

            THE END

            ---VISION_META---
            {"visual_medium":"photoreal_live_action","render_style_lock":"STYLE LOCK: photoreal","notes":"x"}
            ---END_VISION_META---
            ---ADAPTATION_REPORT---
            {"source_complete":"yes","metrics":{"scenes":1,"speaking_cast":0,"body_words":10,"est_runtime_min":0.1},"issues":[],"spec_feedback":[]}
            ---END_ADAPTATION_REPORT---
            """;

        var (fountain, vision) = BookToFountainConverter.SplitVisionMetaTrailer(raw);
        Assert.NotNull(vision);
        Assert.DoesNotContain("ADAPTATION_REPORT", fountain, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("source_complete", fountain, StringComparison.OrdinalIgnoreCase);
    }
}
