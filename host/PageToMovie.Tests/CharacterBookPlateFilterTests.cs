using PageToMovie.Engine;
using SkiaSharp;
using Xunit;

namespace PageToMovie.Tests;

public class CharacterBookPlateFilterTests : IDisposable
{
    private readonly string _tempDir;

    public CharacterBookPlateFilterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "pagetomovie_plate_filter_tests_" + Guid.NewGuid().ToString("N"));
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
    public void Text_Only_Page_Bitmap_Is_Classified_As_Text_Only()
    {
        // 600x800 bitmap simulating a text-only page (white background with black text lines)
        var path = Path.Combine(_tempDir, "page_002_text.png");
        using (var bitmap = new SKBitmap(600, 800))
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);

            using var paint = new SKPaint
            {
                Color = SKColors.Black,
                TextSize = 16,
                IsAntialias = true
            };

            // Draw paragraph text lines (black text on white page)
            for (int y = 50; y < 750; y += 30)
            {
                canvas.DrawText($"Line of book text at y={y} describing Buster running through the garden.", 40, y, paint);
            }

            using var stream = File.Create(path);
            bitmap.Encode(stream, SKEncodedImageFormat.Png, 90);
        }

        bool isTextOnly = CharacterBookPlateService.IsTextOnlyImageFile(path);
        Assert.True(isTextOnly, "Text-only page (black text on white background) must be classified as text-only.");
    }

    [Fact]
    public void Character_Illustration_Page_Bitmap_Is_Classified_As_Illustration()
    {
        // 600x800 bitmap simulating a character illustration page (colorful character artwork)
        var path = Path.Combine(_tempDir, "page_005_buster_art.png");
        using (var bitmap = new SKBitmap(600, 800))
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);

            // Draw colorful character artwork (Buster's blue coat, red bed, green grass)
            using var bluePaint = new SKPaint { Color = SKColors.DeepSkyBlue, Style = SKPaintStyle.Fill };
            using var redPaint = new SKPaint { Color = SKColors.Crimson, Style = SKPaintStyle.Fill };
            using var greenPaint = new SKPaint { Color = SKColors.ForestGreen, Style = SKPaintStyle.Fill };
            using var brownPaint = new SKPaint { Color = SKColors.SaddleBrown, Style = SKPaintStyle.Fill };

            canvas.DrawRect(0, 500, 600, 300, greenPaint); // Grass
            canvas.DrawRoundRect(100, 200, 400, 350, 20, 20, redPaint); // Bed
            canvas.DrawCircle(300, 300, 80, bluePaint); // Character sweater
            canvas.DrawCircle(300, 220, 50, brownPaint); // Character head

            using var stream = File.Create(path);
            bitmap.Encode(stream, SKEncodedImageFormat.Png, 90);
        }

        bool isTextOnly = CharacterBookPlateService.IsTextOnlyImageFile(path);
        Assert.False(isTextOnly, "Colorful character illustration page must be classified as an illustration (NOT text-only).");
    }

    [Fact]
    public void Character_Line_Art_Drawing_Is_Classified_As_Illustration()
    {
        // 600x800 bitmap simulating a character line art drawing (sketches/drawings with dark outlines and shading)
        var path = Path.Combine(_tempDir, "page_007_line_art.png");
        using (var bitmap = new SKBitmap(600, 800))
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);

            using var artPaint = new SKPaint
            {
                Color = SKColors.DarkSlateBlue,
                StrokeWidth = 4,
                Style = SKPaintStyle.Stroke,
                IsAntialias = true
            };

            // Draw intricate character line-art drawing across the page
            for (int r = 20; r < 250; r += 15)
            {
                canvas.DrawCircle(300, 400, r, artPaint);
                canvas.DrawRect(300 - r, 400 - r, r * 2, r * 2, artPaint);
            }

            using var stream = File.Create(path);
            bitmap.Encode(stream, SKEncodedImageFormat.Png, 90);
        }

        bool isTextOnly = CharacterBookPlateService.IsTextOnlyImageFile(path);
        Assert.False(isTextOnly, "Character line art drawing page must be classified as an illustration (NOT text-only).");
    }
}
