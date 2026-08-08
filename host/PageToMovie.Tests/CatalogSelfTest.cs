using System.Collections.Generic;
using System.Linq;
using PageToMovie.Core.Models;
using Xunit;

namespace PageToMovie.Tests;

/// <summary>
/// Catalog self-test: each modality used by the pipeline must expose at least
/// one catalog entry (keys present). An empty modality means no keys and no fallback.
///
/// Replaces the empty 0-byte placeholder that was committed on master.
/// API: <see cref="ModelCatalog"/> via <c>All</c>, <c>OfKind</c>, <c>TryGet</c>.
/// </summary>
public class CatalogSelfTest
{
    private static ModelCatalog Catalog => ModelCatalog.Instance;

    /// <summary>
    /// Core invariant (catalog-self-test.md): for each modality, keys are present
    /// or a documented fallback exists. Empty catalog fails.
    /// </summary>
    [Fact]
    public void keysPresentOrFallback()
    {
        var text = Catalog.OfKind(ModelCapability.Text).ToList();
        var image = Catalog.OfKind(ModelCapability.Image).ToList();
        var video = Catalog.OfKind(ModelCapability.Video).ToList();
        var audio = Catalog.OfKind(ModelCapability.Audio).ToList();
        var voice = Catalog.OfKind(ModelCapability.Voice).ToList();

        Assert.True(text.Count > 0, "Text catalog empty: no keys and no fallback.");
        Assert.True(image.Count > 0, "Image catalog empty: no keys and no fallback.");
        Assert.True(video.Count > 0, "Video catalog empty: no keys and no fallback.");
        Assert.True(audio.Count > 0, "Audio catalog empty: no keys and no fallback.");
        Assert.True(voice.Count > 0, "Voice catalog empty: no keys and no fallback.");
    }

    [Fact]
    public void All_IsNonEmpty()
    {
        Assert.NotEmpty(Catalog.All);
    }

    [Fact]
    public void AllModalities_HaveDistinctIds()
    {
        foreach (var kind in new[]
                 {
                     ModelCapability.Text, ModelCapability.Image, ModelCapability.Video,
                     ModelCapability.Audio, ModelCapability.Voice
                 })
        {
            var models = Catalog.OfKind(kind).ToList();
            Assert.True(models.Count > 0, $"{kind}: no models");
            var ids = models.Select(m => m.Id).Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
            Assert.Equal(ids.Count, ids.Distinct(System.StringComparer.OrdinalIgnoreCase).Count());
        }
    }

    [Fact]
    public void DurationCapableModels_AdvertiseMaxDuration()
    {
        var durationModels = Catalog.OfKind(ModelCapability.Video)
            .Concat(Catalog.OfKind(ModelCapability.Audio))
            .Where(m => m.SupportsDuration)
            .ToList();

        foreach (var m in durationModels)
        {
            Assert.True(m.MaxDurationSeconds is > 0,
                $"{m.Id}: SupportsDuration=true but MaxDurationSeconds={m.MaxDurationSeconds}");
        }
    }

    [Fact]
    public void TryGet_RoundTrips_FirstTextModel()
    {
        var first = Catalog.OfKind(ModelCapability.Text).FirstOrDefault();
        Assert.NotNull(first);
        Assert.True(Catalog.TryGet(first!.Id, out var got));
        Assert.NotNull(got);
        Assert.Equal(first.Id, got!.Id);
    }

    [Fact]
    public void TextModels_Exist_ForStage1Adaptation()
    {
        var text = Catalog.OfKind(ModelCapability.Text).ToList();
        Assert.NotEmpty(text);
        Assert.All(text, m => Assert.False(string.IsNullOrWhiteSpace(m.Id)));
    }

    [Fact]
    public void VoiceModels_Exist_ForNarrationFallback()
    {
        var voice = Catalog.OfKind(ModelCapability.Voice).ToList();
        Assert.NotEmpty(voice);
        Assert.All(voice, m => Assert.False(string.IsNullOrWhiteSpace(m.Id)));
    }
}
