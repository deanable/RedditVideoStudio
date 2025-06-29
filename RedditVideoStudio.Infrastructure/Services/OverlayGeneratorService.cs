using Microsoft.Extensions.Logging;
using RedditVideoStudio.Core.Interfaces;
using RedditVideoStudio.Shared.Configuration;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RedditVideoStudio.Infrastructure.Services
{
    /// <summary>
    /// Implements the IImageService using the SkiaSharp library to generate
    /// images from text and create video thumbnails.
    /// </summary>
    public class OverlayGeneratorService : IImageService
    {
        private readonly IAppConfiguration _appConfig;
        private readonly ILogger<OverlayGeneratorService> _logger;

        public OverlayGeneratorService(IAppConfiguration appConfig, ILogger<OverlayGeneratorService> logger)
        {
            _appConfig = appConfig;
            _logger = logger;
        }

        public Task<string> GenerateImageFromTextAsync(string text, string outputPath, CancellationToken cancellationToken)
        {
            var settings = _appConfig.Settings.ImageGeneration;
            using var font = CreateFont(settings.FontFamily, settings.FontSize);
            return CreateImageFromText(text, outputPath, settings, font, cancellationToken);
        }

        // In C:\Users\Dean Kruger\source\repos\RedditVideoStudio\RedditVideoStudio.Infrastructure\Services\OverlayGeneratorService.cs

        public async Task<string> GenerateThumbnailAsync(string backgroundImagePath, string text, string outputPath, CancellationToken cancellationToken)
        {
            if (!File.Exists(backgroundImagePath))
            {
                throw new FileNotFoundException("Thumbnail background image not found.", backgroundImagePath);
            }

            // --- START OF CORRECTION ---

            // Define a standard size for YouTube thumbnails
            const int thumbnailWidth = 1280;
            const int thumbnailHeight = 720;

            using var originalBitmap = SKBitmap.Decode(backgroundImagePath);

            // Create a new bitmap with the desired thumbnail dimensions
            using var resizedBitmap = new SKBitmap(thumbnailWidth, thumbnailHeight);

            // Resize the original image and draw it onto the new bitmap
            // This uses high-quality bicubic resampling
            using (var canvas = new SKCanvas(resizedBitmap))
            {
                canvas.DrawBitmap(originalBitmap, new SKRect(0, 0, thumbnailWidth, thumbnailHeight), new SKPaint { FilterQuality = SKFilterQuality.High });
            }

            // Use the resized bitmap for the surface
            using var surface = SKSurface.Create(new SKImageInfo(thumbnailWidth, thumbnailHeight));
            var surfaceCanvas = surface.Canvas;
            surfaceCanvas.DrawBitmap(resizedBitmap, 0, 0);

            // --- END OF CORRECTION ---

            var settings = _appConfig.Settings.ImageGeneration;
            using var font = CreateFont(settings.FontFamily, settings.ThumbnailFontSize, SKFontStyle.Bold);

            using var textFillPaint = new SKPaint(font)
            {
                IsAntialias = true,
                Color = SKColors.White,
                Style = SKPaintStyle.Fill,
                TextAlign = SKTextAlign.Center
            };

            using var textStrokePaint = new SKPaint(font)
            {
                IsAntialias = true,
                Color = SKColor.Parse(settings.ThumbnailFontOutlineColor),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = settings.ThumbnailFontOutlineWidth,
                TextAlign = SKTextAlign.Center
            };

            var lines = BreakTextIntoLines(font, text, surfaceCanvas.DeviceClipBounds.Width * 0.9f);
            var totalTextHeight = lines.Count * font.Size * 1.2f;
            var startY = (surfaceCanvas.DeviceClipBounds.Height - totalTextHeight) / 2 + font.Size;

            foreach (var line in lines)
            {
                var x = surfaceCanvas.DeviceClipBounds.Width / 2f;
                surfaceCanvas.DrawText(line, x, startY, textStrokePaint);
                surfaceCanvas.DrawText(line, x, startY, textFillPaint);
                startY += font.Size * 1.2f;
            }

            using var image = surface.Snapshot();
            // Using a quality of 90 is a good balance for thumbnails.
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
            await using var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
            await data.AsStream().CopyToAsync(stream, cancellationToken);

            _logger.LogInformation("Thumbnail generated successfully at {Path}", outputPath);
            return outputPath;
        }
        private async Task<string> CreateImageFromText(string text, string outputPath, ImageGenerationSettings settings, SKFont font, CancellationToken cancellationToken)
        {
            // Create the canvas and clear it to transparent.
            using var surface = SKSurface.Create(new SKImageInfo(settings.ImageWidth, settings.ImageHeight, SKColorType.Bgra8888, SKAlphaType.Premul));
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.Transparent);

            float maxTextWidth = settings.ImageWidth - (2 * settings.ExteriorPadding);
            var textLines = BreakTextIntoLines(font, text, maxTextWidth);

            // Check if there is any actual text to render after breaking it into lines.
            if (textLines.Any())
            {
                // Only draw the background rectangle and text if there are lines to draw.
                using var textPaint = new SKPaint(font) { Color = SKColor.Parse(settings.TextColor), IsAntialias = true, TextAlign = SKTextAlign.Center };
                using var backgroundPaint = new SKPaint { Color = SKColor.Parse(settings.RectangleColor).WithAlpha((byte)(settings.BackgroundOpacity * 255)), IsAntialias = true };

                float textBlockHeight = (textLines.Count * font.Size * 1.5f);
                float rectHeight = textBlockHeight + (2 * settings.InteriorPadding);
                float rectWidth = maxTextWidth + (2 * settings.InteriorPadding);

                float rectLeft = (settings.ImageWidth - rectWidth) / 2;
                float rectTop = (settings.ImageHeight - rectHeight) / 2;
                var rect = new SKRect(rectLeft, rectTop, rectLeft + rectWidth, rectTop + rectHeight);

                canvas.DrawRoundRect(rect, 25, 25, backgroundPaint);

                var startY = rect.Top + settings.InteriorPadding + font.Size;
                float centerX = settings.ImageWidth / 2f;
                foreach (var line in textLines)
                {
                    canvas.DrawText(line, centerX, startY, textPaint);
                    startY += font.Size * 1.5f;
                }
            }
            else
            {
                _logger.LogWarning("No text lines to render for overlay image at {Path}. A blank, transparent image will be created.", outputPath);
            }

            // Always create and save an image file, even if it's just transparent.
            // This prevents a FileNotFoundException in downstream processes that expect this file to exist.
            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            await using var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
            await data.AsStream().CopyToAsync(stream, cancellationToken);

            return outputPath;
        }

        private SKFont CreateFont(string fontFamily, float size)
        {
            return CreateFont(fontFamily, size, SKFontStyle.Normal);
        }

        private SKFont CreateFont(string fontFamily, float size, SKFontStyle style)
        {
            var typeface = SKTypeface.FromFamilyName(fontFamily, style);
            if (typeface == null)
            {
                _logger.LogWarning("Font family '{FontFamily}' not found. Falling back to default typeface.", fontFamily);
                typeface = SKTypeface.Default;
            }
            return new SKFont(typeface, size);
        }

        private List<string> BreakTextIntoLines(SKFont font, string text, float maxWidth)
        {
            var lines = new List<string>();
            var words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (!words.Any()) return lines;
            var currentLine = "";
            foreach (var word in words)
            {
                var testLine = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
                if (font.MeasureText(testLine) > maxWidth)
                {
                    if (!string.IsNullOrEmpty(currentLine)) lines.Add(currentLine);
                    currentLine = word;
                }
                else
                {
                    currentLine = testLine;
                }
            }
            lines.Add(currentLine);
            return lines;
        }
    }
}