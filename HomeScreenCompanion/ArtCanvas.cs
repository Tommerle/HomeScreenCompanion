using SkiaSharp;
using System;
using System.IO;

namespace HomeScreenCompanion
{
    /// <summary>
    /// Drawing helpers shared by the generated art (Top 10 tiles, collection posters and
    /// backgrounds). Written for Emby's bundled SkiaSharp 2.88.3.
    /// </summary>
    internal static class ArtCanvas
    {
        internal static readonly SKColor Base = new SKColor(13, 13, 16);

        internal static void Save(SKSurface surface, string outputPath)
        {
            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, 92);
            using var stream = File.Create(outputPath);
            data.SaveTo(stream);
        }

        internal static int RadiusFor(int cardW) => Math.Max(6, (int)Math.Round(cardW * 0.045));

        // Heavily blurred cover-fit of the poster, pulled toward the base colour (60% by default).
        //
        // The image is drawn through a clamped shader over a rect larger than the canvas so the
        // blur samples real edge pixels instead of transparency (no dark rim). This avoids the
        // tile-mode CreateBlur overload, which Emby's bundled SkiaSharp (2.88.3) does not have.
        internal static void DrawBackdrop(SKCanvas canvas, SKBitmap poster, int w, int h, float darken = 0.6f)
        {
            int sw = w / 4, sh = h / 4;
            using var small = new SKBitmap(new SKImageInfo(sw, sh, SKColorType.Rgba8888, SKAlphaType.Premul));
            using (var sc = new SKCanvas(small))
            {
                var src = CoverSource(poster.Width, poster.Height, sw, sh);
                float scale = sw / src.Width;
                var fit = SKMatrix.CreateScaleTranslation(scale, scale, -src.Left * scale, -src.Top * scale);
                DrawClampedBlurred(sc, poster, fit, 17.5f, sw, sh);
            }

            DrawClampedBlurred(canvas, small, SKMatrix.CreateScale(w / (float)sw, h / (float)sh), 2f, w, h);
            using var tint = new SKPaint { Color = Base.WithAlpha((byte)(255 * darken)) };
            canvas.DrawRect(0, 0, w, h, tint);
        }

        internal static void DrawClampedBlurred(SKCanvas canvas, SKBitmap bitmap, SKMatrix toCanvas, float sigma, int w, int h)
        {
            float pad = sigma * 3;
            using var paint = new SKPaint
            {
                Shader = SKShader.CreateBitmap(bitmap, SKShaderTileMode.Clamp, SKShaderTileMode.Clamp, toCanvas),
                ImageFilter = SKImageFilter.CreateBlur(sigma, sigma),
                FilterQuality = SKFilterQuality.High
            };
            canvas.DrawRect(new SKRect(-pad, -pad, w + pad, h + pad), paint);
        }

        // Elliptical edge darkening; strength is the corner opacity.
        internal static void DrawVignette(SKCanvas canvas, int w, int h, float strength)
        {
            const int stops = 64;
            var colors = new SKColor[stops + 1];
            var positions = new float[stops + 1];
            for (int i = 0; i <= stops; i++)
            {
                float r = i / (float)stops;
                float t = Clamp01((255 * r - 70) / 185f);
                colors[i] = SKColors.Black.WithAlpha((byte)Math.Round(255 * strength * Math.Pow(t, 1.6)));
                positions[i] = r;
            }
            var toEllipse = SKMatrix.CreateScaleTranslation(w / 2f, h / 2f, w / 2f, h / 2f);
            using var paint = new SKPaint
            {
                Shader = SKShader.CreateRadialGradient(new SKPoint(0, 0), 1, colors, positions, SKShaderTileMode.Clamp, toEllipse)
            };
            canvas.DrawRect(0, 0, w, h, paint);
        }

        // Rounded, cover-fitted poster card with a soft drop shadow and a faint hairline edge.
        internal static void DrawCard(SKCanvas canvas, SKBitmap poster, SKRect rect, float radius,
            float shadowBlur = 26, float shadowOffset = 14, byte shadowAlpha = 230)
        {
            var rr = new SKRoundRect(rect, radius);

            using (var shadow = new SKPaint
            {
                Color = SKColors.Black.WithAlpha(shadowAlpha),
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, shadowBlur),
                IsAntialias = true
            })
            {
                var dropped = new SKRoundRect(SKRect.Create(rect.Left, rect.Top + shadowOffset, rect.Width, rect.Height), radius);
                canvas.DrawRoundRect(dropped, shadow);
            }

            canvas.Save();
            canvas.ClipRoundRect(rr, SKClipOperation.Intersect, antialias: true);
            using (var img = new SKPaint { FilterQuality = SKFilterQuality.High, IsAntialias = true })
                canvas.DrawBitmap(poster, CoverSource(poster.Width, poster.Height, (int)rect.Width, (int)rect.Height), rect, img);
            canvas.Restore();

            using var edge = new SKPaint
            {
                Color = SKColors.White.WithAlpha(28),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = Math.Max(1, (int)rect.Width / 300),
                IsAntialias = true
            };
            var inner = rr;
            inner.Deflate(edge.StrokeWidth / 2, edge.StrokeWidth / 2);
            canvas.DrawRoundRect(inner, edge);
        }

        // Centred source crop so the image fills w x h without distortion (CSS object-fit: cover).
        internal static SKRect CoverSource(int srcW, int srcH, int w, int h)
        {
            float scale = Math.Max(w / (float)srcW, h / (float)srcH);
            float cw = w / scale, ch = h / scale;
            float x = (srcW - cw) / 2, y = (srcH - ch) / 2;
            return new SKRect(x, y, x + cw, y + ch);
        }

        internal static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;


        // A font embedded under HomeScreenCompanion.Fonts (Anton.ttf, BebasNeue.ttf).
        internal static SKTypeface LoadFont(string fileName)
        {
            using var stream = typeof(ArtCanvas).Assembly.GetManifestResourceStream("HomeScreenCompanion.Fonts." + fileName);
            if (stream == null) return SKTypeface.Default;
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return SKTypeface.FromData(SKData.CreateCopy(ms.ToArray())) ?? SKTypeface.Default;
        }
    }
}
