using PageToMovie.Engine;
using SkiaSharp;
using Xunit;

namespace PageToMovie.Tests;

public class BookPlateLayoutGateTests : IDisposable
{
    private readonly string _tempDir;

    public BookPlateLayoutGateTests()
    {
        CharacterBookPlateService.ClearTextOnlyCache();
        _tempDir = Path.Combine(Path.GetTempPath(), "pagetomovie_layout_gate_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch { /* ignore */ }
    }

    [Fact]
    public void QualityGate_TextOnly_Pages_Are_Never_Classified_As_Illustrations()
    {
        // Generate 5 text-only book pages (white background with black text)
        for (int i = 1; i <= 5; i++)
        {
            var path = Path.Combine(_tempDir, $"page_00{i}_text.png");
            using var bitmap = new SKBitmap(600, 800);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.White);

            using var font = new SKFont(SKTypeface.Default, 16);
            using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true };

            for (int y = 60; y < 750; y += 40)
            {
                canvas.DrawText($"Page {i} text block line at y={y} describing the story events in detail.", 30, y, font, paint);
            }

            using (var stream = File.Create(path))
            {
                bitmap.Encode(stream, SKEncodedImageFormat.Png, 90);
            }

            bool isTextOnly = CharacterBookPlateService.IsTextOnlyImageFile(path);
            Assert.True(isTextOnly, $"Text-only page {i} MUST be detected as text-only.");
        }
    }

    [Fact]
    public void QualityGate_Illustrated_Pages_Are_Always_Preserved_As_Illustrations()
    {
        // Generate 5 illustrated book pages (with colorful character artwork)
        var colors = new[] { SKColors.DeepSkyBlue, SKColors.Crimson, SKColors.ForestGreen, SKColors.Orange, SKColors.Purple };
        for (int i = 1; i <= 5; i++)
        {
            var path = Path.Combine(_tempDir, $"page_00{i}_art.png");
            using var bitmap = new SKBitmap(600, 800);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.White);

            using var artPaint = new SKPaint { Color = colors[(i - 1) % colors.Length], Style = SKPaintStyle.Fill };
            canvas.DrawRoundRect(100, 150, 400, 500, 30, 30, artPaint);

            using (var stream = File.Create(path))
            {
                bitmap.Encode(stream, SKEncodedImageFormat.Png, 90);
            }

            bool isTextOnly = CharacterBookPlateService.IsTextOnlyImageFile(path);
            Assert.False(isTextOnly, $"Illustrated page {i} MUST be preserved as an illustration candidate.");
        }
    }
}
