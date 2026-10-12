using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using static HomeScreenCompanion.ArtCanvas;

namespace HomeScreenCompanion
{
    /// <summary>
    /// The "Customise" settings of a top list's rank badge (HomeSectionSettings.BadgeOptions): a
    /// small flat JSON object such as {"shape":"roundsq","font":"bebas","medal":"metal"}. The
    /// list's BadgeStyle still says the type (neutral / slate-grey / emby-green / ocean-blue /
    /// soft-red / violet = Number badge in that colour, top10 = Top 10 tile, none = No number).
    /// A missing key (or "" for the whole thing) means today's look, so lists without options are
    /// drawn by the original code, byte for byte. Keys:
    ///   font      lemonmilk | an ArtFonts id          number font
    ///   num       filled | outline | gradient | metallic
    ///   numcolour / outcolour  #rrggbb                number fill / outline colour
    ///   outline   thin | normal | thick
    ///   size      s | m | l | xl                      (Number badge)
    ///   shape     circle roundsq pill ribbon banner diag netflix numonly bigoutline hidden (Number badge)
    ///   colour    #rrggbb                             badge colour (a preset colour lives in BadgeStyle)
    ///   medal     flat | metal                        gold / silver / bronze for ranks 1-3
    ///   shadow    true | false
    ///   pos       tl tr bl br bc                      (Number badge)
    ///   tilepos   beside | behind                     (Top 10 tile)
    ///   tilebg    flat | solid | blur, tilebgcolour #rrggbb (Top 10 tile)
    ///   special   bigger | crown | glow               rank 1 only
    ///   move, weeks, plays, logo  true | false        extras: rank movement chip, "N wks" line,
    ///                                                 "N plays" line, Netflix-style TOP 10 corner logo
    ///   moveeq    true | false                        movement chip also shows "=" for an unchanged rank
    ///   logolabel top10 | top10rank                   TOP 10 logo: plain, or with "#rank" on a second line
    ///   logosize  s | m | l                           TOP 10 logo size (m = the original size)
    /// </summary>
    internal sealed class BadgeOptions
    {
        public string? Font, Num, NumColour, OutColour, Outline, Size, Shape, Colour, Medal, Pos, TilePos, TileBg, TileBgColour, Special, LogoLabel, LogoSize;
        public bool? Shadow, Move, MoveEq, Weeks, Plays, Logo;

        public static readonly BadgeOptions None = new BadgeOptions();

        public static readonly string[] Shapes = { "circle", "roundsq", "pill", "ribbon", "banner", "diag", "netflix", "numonly", "bigoutline", "hidden" };
        private static readonly string[] Positions = { "tl", "tr", "bl", "br", "bc" };

        public bool IsDefault => Font == null && Num == null && NumColour == null && OutColour == null && Outline == null && Size == null
            && Shape == null && Colour == null && Medal == null && Pos == null && TilePos == null && TileBg == null && TileBgColour == null
            && Special == null && LogoLabel == null && LogoSize == null && Shadow != true && !AnyExtras;

        public bool AnyExtras => Move == true || Weeks == true || Plays == true || Logo == true;

        public static BadgeOptions Parse(string? json)
        {
            var o = new BadgeOptions();
            if (string.IsNullOrWhiteSpace(json)) return o;
            foreach (Match m in Regex.Matches(json, "\"(\\w+)\"\\s*:\\s*(\"(?:[^\"\\\\]|\\\\.)*\"|-?\\d+(?:\\.\\d+)?|true|false|null)"))
            {
                string key = m.Groups[1].Value.ToLowerInvariant(), raw = m.Groups[2].Value;
                string? s = raw.StartsWith("\"") ? raw.Substring(1, raw.Length - 2).Trim().ToLowerInvariant() : null;
                bool? b = raw == "true" ? true : raw == "false" ? false : (bool?)null;
                bool hex = s != null && Regex.IsMatch(s, "^#[0-9a-f]{6}$");
                switch (key)
                {
                    case "font": if (s != null && BadgeFonts.Exists(s)) o.Font = s; break;
                    case "num": if (s == "filled" || s == "outline" || s == "gradient" || s == "metallic") o.Num = s; break;
                    case "numcolour": if (hex) o.NumColour = s; break;
                    case "outcolour": if (hex) o.OutColour = s; break;
                    case "outline": if (s == "thin" || s == "normal" || s == "thick") o.Outline = s; break;
                    case "size": if (s == "s" || s == "m" || s == "l" || s == "xl") o.Size = s; break;
                    case "shape": if (s != null && Shapes.Contains(s)) o.Shape = s; break;
                    case "colour": if (hex) o.Colour = s; break;
                    case "medal": if (s == "flat" || s == "metal") o.Medal = s; break;
                    case "pos": if (s != null && Positions.Contains(s)) o.Pos = s; break;
                    case "tilepos": if (s == "beside" || s == "behind") o.TilePos = s; break;
                    case "tilebg": if (s == "flat" || s == "solid" || s == "blur") o.TileBg = s; break;
                    case "tilebgcolour": if (hex) o.TileBgColour = s; break;
                    case "special": if (s == "bigger" || s == "crown" || s == "glow") o.Special = s; break;
                    case "shadow": o.Shadow = b; break;
                    case "move": o.Move = b; break;
                    case "moveeq": o.MoveEq = b; break;
                    case "weeks": o.Weeks = b; break;
                    case "plays": o.Plays = b; break;
                    case "logo": o.Logo = b; break;
                    case "logolabel": if (s == "top10" || s == "top10rank") o.LogoLabel = s; break;
                    case "logosize": if (s == "s" || s == "m" || s == "l") o.LogoSize = s; break;
                }
            }
            return o;
        }

        // The movement chip for this title, or null: an unchanged rank ("=") only when moveeq is ticked.
        internal string? MoveChip(RankExtras ex) => Move != true || ex.Move == null || (ex.Move == "=" && MoveEq != true) ? null : ex.Move;

        // TOP 10 logo scale: S / M (the original size) / L.
        internal float LogoScale => LogoSize == "s" ? 0.75f : LogoSize == "l" ? 1.4f : 1f;
        // The rank drawn under the logo ("TOP 10 #3"), or null for the plain logo.
        internal int? LogoRank(int rank) => LogoLabel == "top10rank" ? rank : (int?)null;

        internal static SKColor Hex(string? hex, SKColor fallback) => hex != null && SKColor.TryParse(hex, out var c) ? c : fallback;
    }

    /// <summary>Per-title values for the extras (null = not known yet, nothing drawn).</summary>
    internal sealed class RankExtras
    {
        public string? Move;   // "+2", "-1", "=", "NEW"
        public int? Weeks;
        public int? Plays;
        public bool Sample;    // preview: sample values
    }

    /// <summary>
    /// Inside the server a top list's look travels as one string: the BadgeStyle, plus "|" and the
    /// BadgeOptions JSON when there are options. Every existing style string is still valid as is.
    /// </summary>
    internal static class BadgeLook
    {
        public static string Combine(string? style, string? options)
        {
            style = string.IsNullOrWhiteSpace(style) ? "neutral" : style!.Trim();
            // Only the Top 10 tile is customised; options saved for a number badge (4.2.0.32-.36) are ignored.
            if (!IsTile(style)) return style;
            return string.IsNullOrWhiteSpace(options) || options!.Trim() == "{}" ? style : style + "|" + options.Trim();
        }

        public static (string Style, string Options) Split(string? look)
        {
            if (string.IsNullOrEmpty(look)) return ("neutral", "");
            int i = look!.IndexOf('|');
            return i < 0 ? (look, "") : (look.Substring(0, i), look.Substring(i + 1));
        }

        /// <summary>The look stored in a top list's HomeSectionSettings.</summary>
        public static string FromSettings(IDictionary<string, string>? settings, string fallbackStyle)
        {
            string style = settings != null && settings.TryGetValue("BadgeStyle", out var bs) && !string.IsNullOrWhiteSpace(bs) ? bs : fallbackStyle;
            string opts = settings != null && settings.TryGetValue("BadgeOptions", out var bo) ? bo ?? "" : "";
            return Combine(style, opts);
        }

        public static string StyleOf(string? look) => Split(look).Style;
        public static bool IsTile(string? style) => string.Equals(style, "top10", StringComparison.OrdinalIgnoreCase);
        public static bool WantsHistory(string? look) => IsTile(StyleOf(look)) && BadgeOptions.Parse(Split(look).Options).AnyExtras;
        public static bool WantsPlays(string? look) => IsTile(StyleOf(look)) && BadgeOptions.Parse(Split(look).Options).Plays == true;
    }

    /// <summary>Number fonts: LemonMilk (today's badge font) and every art font.</summary>
    internal static class BadgeFonts
    {
        public static readonly (string Id, string Name, string File)[] All =
        {
            ("lemonmilk", "Lemon Milk", ""),
            ("anton", "Anton", "Anton.ttf"),
            ("bebas", "Bebas Neue", "BebasNeue.ttf"),
            ("roboto", "Roboto", "Roboto-Bold.ttf"),
            ("dmserif", "DM Serif", "DMSerifDisplay.ttf"),
            ("alfaslab", "Alfa Slab", "AlfaSlabOne.ttf"),
            ("yesteryear", "Yesteryear", "Yesteryear.ttf"),
            ("creepster", "Creepster", "Creepster.ttf"),
            ("christmas", "Mountains of Christmas", "MountainsOfChristmas.ttf"),
            ("orbitron", "Orbitron", "Orbitron.ttf"),
            ("rye", "Rye", "Rye.ttf"),
            ("luckiestguy", "Luckiest Guy", "LuckiestGuy.ttf"),
            ("greatvibes", "Great Vibes", "GreatVibes.ttf"),
        };

        private static readonly Dictionary<string, SKTypeface> Cache = new Dictionary<string, SKTypeface>();

        public static bool Exists(string id) => All.Any(f => f.Id == id);

        public static SKTypeface Face(string id)
        {
            lock (Cache)
            {
                if (Cache.TryGetValue(id, out var t)) return t;
                var entry = All.FirstOrDefault(f => f.Id == id);
                if (entry.Id == null) entry = All[0];
                if (entry.Id == "lemonmilk")
                {
                    using var s = typeof(BadgeFonts).Assembly.GetManifestResourceStream("HomeScreenCompanion.LemonMilk.otf");
                    if (s == null) t = SKTypeface.Default;
                    else { using var ms = new MemoryStream(); s.CopyTo(ms); t = SKTypeface.FromData(SKData.CreateCopy(ms.ToArray())) ?? SKTypeface.Default; }
                }
                else t = LoadFont(entry.File);
                Cache[id] = t;
                return t;
            }
        }

        /// <summary>The font's group in the art popup (Lemon Milk, the badge's own font, is General).</summary>
        public static string Group(string id) => ArtFonts.All.FirstOrDefault(f => f.Id == id)?.Group ?? "General";

        private static readonly Dictionary<string, string> SampleCache = new Dictionary<string, string>();

        /// <summary>The font's name (small) over "1234567890", both drawn in the font (PNG data: URL), for the popup's font tiles.</summary>
        public static string Sample(string id)
        {
            lock (SampleCache) if (SampleCache.TryGetValue(id, out var cached)) return cached;
            var name = All.FirstOrDefault(f => f.Id == id).Name ?? id;
            const int w = 360, h = 100;
            using var surface = SKSurface.Create(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul));
            var c = surface.Canvas;
            c.Clear(SKColors.Transparent);
            using var p = new SKPaint { Typeface = Face(id), Color = SKColors.White, IsAntialias = true, TextAlign = SKTextAlign.Center };
            BadgeRenderer.DrawFit(c, name, p, w / 2f, 20, w - 40, 22);
            using var d = new SKPaint { Typeface = Face(id), Color = new SKColor(0xF5, 0xC5, 0x18), IsAntialias = true, TextAlign = SKTextAlign.Center };
            BadgeRenderer.DrawFit(c, "1234567890", d, w / 2f, 66, w - 16, 40);
            using var img = surface.Snapshot();
            using var data = img.Encode(SKEncodedImageFormat.Png, 100);
            var url = "data:image/png;base64," + Convert.ToBase64String(data.ToArray());
            lock (SampleCache) SampleCache[id] = url;
            return url;
        }
    }

    /// <summary>
    /// Draws the ranked art of a top-list entry. With no options (every list made before the
    /// Customise popup) it runs the original code unchanged; options and extras go through the
    /// customisable renderers below (shapes, fonts and tile looks from the badge mood board).
    /// </summary>
    internal static class BadgeRenderer
    {
        // ── dispatch ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Writes outputBase.jpg (from the poster) and outputBase-thumb.jpg (from the thumb) for one
        /// entry. look = BadgeStyle or "BadgeStyle|options".
        /// </summary>
        public static void RenderRanked(string? poster, string? thumb, int rank, string outputBase, string look,
            RankExtras? extras = null, Action<string, Exception>? onError = null)
        {
            var (style, json) = BadgeLook.Split(look);
            var o = BadgeOptions.Parse(json);
            extras ??= new RankExtras();
            void Try(string what, Action a) { try { a(); } catch (Exception ex) { onError?.Invoke(what, ex); } }

            // "top10": Netflix-style art composed from the poster — a portrait version for the
            // Primary image and a landscape tile for the Thumb, so the row works with either image
            // type. The big numeral only has room for ranks 1-10; later ranks get the circle badge.
            if (string.Equals(style, "top10", StringComparison.OrdinalIgnoreCase))
            {
                bool tile = poster != null && rank <= 10;
                bool custom = !o.IsDefault;
                if (tile)
                    Try("top 10 poster", () => { if (custom) RenderCard(poster!, rank, outputBase + ".jpg", o, extras); else TopTenTileRenderer.RenderPoster(poster!, rank, outputBase + ".jpg"); });
                else if (poster != null)
                    Try("poster badge", () => { if (custom) CustomBadge(poster, rank, outputBase + ".jpg", "neutral", TileToBadge(o), extras); else CreateRankedPoster(poster, rank, outputBase + ".jpg", "neutral"); });

                if (tile)
                    Try("top 10 tile", () => { if (custom) RenderTile(poster!, rank, outputBase + "-thumb.jpg", o, extras); else TopTenTileRenderer.Render(poster!, rank, outputBase + "-thumb.jpg"); });
                else if (thumb != null)
                    Try("thumb badge", () => { if (custom) CustomBadge(thumb, rank, outputBase + "-thumb.jpg", "neutral", TileToBadge(o), extras); else CreateRankedPoster(thumb, rank, outputBase + "-thumb.jpg", "neutral"); });
                return;
            }

            // The colour presets (and No number): the original drawing; number-badge options are not used.
            if (poster != null)
                Try("poster badge", () => CreateRankedPoster(poster, rank, outputBase + ".jpg", style));
            if (thumb != null)
                Try("thumb badge", () => CreateRankedPoster(thumb, rank, outputBase + "-thumb.jpg", style));
        }

        // Ranks past 10 on a Top 10 list get a circle badge; it keeps the tile's number look.
        private static BadgeOptions TileToBadge(BadgeOptions t) => new BadgeOptions
        {
            Font = t.Font, Num = t.Num == "outline" ? null : t.Num, NumColour = t.Num == "outline" ? null : t.NumColour, Medal = t.Medal, Shadow = t.Shadow,
            Move = t.Move, MoveEq = t.MoveEq, Weeks = t.Weeks, Plays = t.Plays, Logo = t.Logo, LogoLabel = t.LogoLabel, LogoSize = t.LogoSize
        };

        // ── the original circle badge (unchanged) ────────────────────────────────────────
        internal static void CreateRankedPoster(string sourcePath, int rank, string outputPath, string badgeStyle = "neutral")
        {
            // "none" means no rank badge at all: the source image is copied as-is so the rest of
            // the pipeline (ApplyRankedImages, DateModified refresh) still finds a file on disk.
            if (string.Equals(badgeStyle, "none", StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(sourcePath, outputPath, overwrite: true);
                return;
            }

            using var original = SKBitmap.Decode(sourcePath);
            if (original == null)
                throw new InvalidOperationException($"SkiaSharp could not decode '{sourcePath}' (unsupported format or corrupt file)");

            using var surface = SKSurface.Create(new SKImageInfo(original.Width, original.Height))
                ?? throw new InvalidOperationException($"SkiaSharp could not create a {original.Width}x{original.Height} surface");
            var canvas = surface.Canvas;
            canvas.DrawBitmap(original, 0, 0);

            float shortSide = Math.Min(original.Width, original.Height);
            float radius = shortSide * 0.15f;
            float margin = shortSide * 0.04f;
            float cx = margin + radius;
            float cy = margin + radius;

            SKColor bgColor = PresetColour(badgeStyle);
            SKColor textColor = SKColors.White;

            using var bgPaint = new SKPaint { Color = bgColor, IsAntialias = true };
            canvas.DrawCircle(cx, cy, radius, bgPaint);

            var text = rank.ToString();
            float fontSize = radius * 1.1f;

            using var fontStream = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("HomeScreenCompanion.LemonMilk.otf");
            using var typeface = fontStream != null ? SKTypeface.FromStream(fontStream) : SKTypeface.Default;
            using var textPaint = new SKPaint
            {
                Color = textColor,
                TextSize = fontSize,
                IsAntialias = true,
                Typeface = typeface
            };

            float maxTextWidth = radius * 1.6f;
            while (textPaint.MeasureText(text) > maxTextWidth && textPaint.TextSize > 1f)
                textPaint.TextSize -= 1f;

            float textWidth = textPaint.MeasureText(text);
            var metrics = textPaint.FontMetrics;
            float textX = cx - textWidth / 2;
            float textY = cy - (metrics.Ascent + metrics.Descent) / 2;
            canvas.DrawText(text, textX, textY, textPaint);

            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, 92);
            using var stream = File.Create(outputPath);
            data.SaveTo(stream);
        }

        // The six Number badge colours (the old "Badge style" presets).
        internal static SKColor PresetColour(string? badgeStyle)
        {
            switch (badgeStyle?.ToLowerInvariant())
            {
                case "slate-grey": return new SKColor(0x41, 0x41, 0x4B, 224);
                case "emby-green": return new SKColor(0x52, 0xB5, 0x4B, 200);
                case "ocean-blue": return new SKColor(0x2E, 0x86, 0xC1, 210);
                case "soft-red": return new SKColor(0xC9, 0x45, 0x45, 210);
                case "violet": return new SKColor(0x7B, 0x52, 0xB5, 210);
                default: return new SKColor(0, 0, 0, 210);
            }
        }

        // ── text helpers ─────────────────────────────────────────────────────────────────
        internal static SKRect Ink(SKPaint p, string s) { var b = new SKRect(); p.MeasureText(s, ref b); return b; }

        // Sizes the paint so the text's ink fits maxW x maxH; returns the origin that centres it on (cx, cy).
        internal static SKPoint FitCentre(SKPaint p, string s, float cx, float cy, float maxW, float maxH)
        {
            var align = p.TextAlign;
            p.TextAlign = SKTextAlign.Left;
            p.TextSize = Math.Max(4, maxH * 2f);
            var b = Ink(p, s);
            while ((b.Width > maxW || b.Height > maxH) && p.TextSize > 2) { p.TextSize -= Math.Max(0.5f, p.TextSize * 0.02f); b = Ink(p, s); }
            p.TextAlign = align;
            float ox = cx - b.MidX;
            if (align == SKTextAlign.Center) ox += p.MeasureText(s) / 2;
            return new SKPoint(ox, cy - b.MidY);
        }

        internal static void DrawFit(SKCanvas c, string s, SKPaint p, float cx, float cy, float maxW, float maxH)
        {
            var o = FitCentre(p, s, cx, cy, maxW, maxH);
            c.DrawText(s, o.X, o.Y, p);
        }

        private static SKColor Lighten(SKColor c, float t) => new SKColor((byte)(c.Red + (255 - c.Red) * t), (byte)(c.Green + (255 - c.Green) * t), (byte)(c.Blue + (255 - c.Blue) * t), c.Alpha);
        private static SKColor Darken(SKColor c, float t) => new SKColor((byte)(c.Red * (1 - t)), (byte)(c.Green * (1 - t)), (byte)(c.Blue * (1 - t)), c.Alpha);

        // ── medals ───────────────────────────────────────────────────────────────────────
        private static SKColor[] MedalStops(int rank) => rank switch
        {
            1 => new[] { new SKColor(0xFF, 0xF3, 0xB0), new SKColor(0xD9, 0xAE, 0x3A), new SKColor(0x8C, 0x6A, 0x14) },
            2 => new[] { new SKColor(0xFF, 0xFF, 0xFF), new SKColor(0xC4, 0xC6, 0xCE), new SKColor(0x6C, 0x70, 0x7A) },
            _ => new[] { new SKColor(0xF6, 0xC6, 0x95), new SKColor(0xC4, 0x78, 0x33), new SKColor(0x6E, 0x3A, 0x12) },
        };
        private static SKColor MedalText(int rank) => rank == 1 ? new SKColor(0x3A, 0x2A, 0x05) : rank == 2 ? new SKColor(0x26, 0x28, 0x30) : new SKColor(0x2E, 0x15, 0x04);
        private static SKColor MedalFlat(int rank) => rank == 1 ? new SKColor(0xD4, 0xAF, 0x37, 235) : rank == 2 ? new SKColor(0xB8, 0xBC, 0xC6, 235) : new SKColor(0xB8, 0x73, 0x33, 235);
        private static readonly SKColor[] Chrome = { new SKColor(0xFA, 0xFA, 0xFC), new SKColor(0xB4, 0xB6, 0xC0), new SKColor(0x6A, 0x6C, 0x78), new SKColor(0xE6, 0xE8, 0xEE), new SKColor(0x8A, 0x8C, 0x96) };
        private static readonly float[] ChromePos = { 0f, 0.38f, 0.5f, 0.62f, 1f };

        // ── the number, in any style ─────────────────────────────────────────────────────
        private sealed class NumLook
        {
            public SKTypeface Face = SKTypeface.Default;
            public string Style = "filled";
            public SKColor Fill = SKColors.White, Out = SKColors.Black;
            public float Stroke;          // outline width outside the glyph
            public SKColor[]? Gradient;   // overrides Fill
            public float[]? GradientPos;
            public bool Shadow;
            public float ShadowBlur;
        }

        private static void DrawNumber(SKCanvas c, string text, float x, float y, float size, NumLook L, SKTextAlign align = SKTextAlign.Left, float scaleX = 1)
        {
            SKPaint P() => new SKPaint { Typeface = L.Face, TextSize = size, TextAlign = align, TextScaleX = scaleX, IsAntialias = true };
            using var fill = P();
            var ink = Ink(fill, "8");
            float top = y + ink.Top, bottom = y + ink.Bottom;
            if (L.Shadow)
                using (var sh = P())
                {
                    sh.Color = SKColors.Black.WithAlpha(200);
                    sh.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, Math.Max(1, L.ShadowBlur));
                    if (L.Stroke > 0) { sh.Style = SKPaintStyle.StrokeAndFill; sh.StrokeWidth = L.Stroke * 2; }
                    c.DrawText(text, x + size * 0.02f, y + size * 0.04f, sh);
                }
            if (L.Stroke > 0)
                using (var st = P())
                {
                    st.Color = L.Out; st.Style = SKPaintStyle.Stroke; st.StrokeWidth = L.Stroke * 2; st.StrokeJoin = SKStrokeJoin.Round;
                    c.DrawText(text, x, y, st);
                }
            if (L.Gradient != null)
            {
                fill.Color = SKColors.White;
                fill.Shader = SKShader.CreateLinearGradient(new SKPoint(0, top), new SKPoint(0, bottom), L.Gradient, L.GradientPos, SKShaderTileMode.Clamp);
            }
            else fill.Color = L.Fill;
            c.DrawText(text, x, y, fill);
        }

        private static void ApplyNumStyle(NumLook L, string style, SKColor colour, int rank, string? medal, float strokeUnit)
        {
            L.Style = style;
            L.Fill = colour;
            switch (style)
            {
                case "outline": L.Stroke = strokeUnit; break;
                case "gradient": L.Gradient = new[] { Lighten(colour, 0.45f), colour, Darken(colour, 0.5f) }; L.GradientPos = new[] { 0f, 0.45f, 1f }; break;
                case "metallic":
                    L.Gradient = Chrome; L.GradientPos = ChromePos;
                    L.Out = new SKColor(0x0A, 0x0A, 0x0C); L.Stroke = Math.Max(1, strokeUnit * 0.35f); break;
            }
            if (medal != null && rank <= 3)
            {
                if (medal == "metal") { var s = MedalStops(rank); L.Gradient = new[] { s[0], s[1], s[2], s[0], s[1] }; L.GradientPos = ChromePos; if (L.Stroke <= 0) { L.Out = new SKColor(0x0A, 0x0A, 0x0C); L.Stroke = Math.Max(1, strokeUnit * 0.35f); } }
                else { L.Gradient = null; L.Fill = MedalFlat(rank).WithAlpha(255); }
            }
        }

        private static float StrokeFactor(string? outline) => outline == "thin" ? 0.45f : outline == "thick" ? 2f : 1f;

        // ── Number badge (customised) ────────────────────────────────────────────────────
        internal static void CustomBadge(string sourcePath, int rank, string outputPath, string style, BadgeOptions o, RankExtras? extras)
        {
            using var src = SKBitmap.Decode(sourcePath)
                ?? throw new InvalidOperationException($"SkiaSharp could not decode '{sourcePath}' (unsupported format or corrupt file)");
            int w = src.Width, h = src.Height;
            using var surface = SKSurface.Create(new SKImageInfo(w, h))
                ?? throw new InvalidOperationException($"SkiaSharp could not create a {w}x{h} surface");
            var c = surface.Canvas;
            c.DrawBitmap(src, 0, 0);
            DrawBadge(c, w, h, rank, style, o, extras ?? new RankExtras());
            Save(surface, outputPath);
        }

        private static void DrawBadge(SKCanvas c, int w, int h, int rank, string style, BadgeOptions o, RankExtras ex)
        {
            float s = Math.Min(w, h);
            float size = o.Size == "s" ? 0.10f : o.Size == "l" ? 0.21f : o.Size == "xl" ? 0.27f : 0.15f;
            if (rank == 1 && o.Special == "bigger") size *= 1.3f;
            float r = s * size, m = s * 0.04f;
            string txt = rank.ToString(), shape = o.Shape ?? "circle", pos = o.Pos ?? "tl";
            bool right = pos == "tr" || pos == "br";

            // Extras drawn under the badge: the weeks / plays lines at the bottom.
            DrawStatLines(c, new SKRect(0, 0, w, h), ex, o, s * 0.06f, false);

            var bgColour = o.Colour != null ? BadgeOptions.Hex(o.Colour, SKColors.Black).WithAlpha(220) : PresetColour(style);
            bool medal = o.Medal != null && rank <= 3;
            var L = new NumLook { Face = BadgeFonts.Face(o.Font ?? "lemonmilk"), Out = BadgeOptions.Hex(o.OutColour, SKColors.Black), Shadow = o.Shadow == true && (shape == "numonly" || shape == "bigoutline"), ShadowBlur = r * 0.12f };
            var numColour = BadgeOptions.Hex(o.NumColour, o.Num == "gradient" ? new SKColor(0x52, 0xB5, 0x4B) : SKColors.White);
            ApplyNumStyle(L, o.Num ?? "filled", numColour, rank, null, r * 0.06f * StrokeFactor(o.Outline));
            if (medal && o.Medal == "flat") { bgColour = MedalFlat(rank); if (o.Num == null || o.Num == "filled") L.Fill = rank == 3 ? SKColors.White : MedalText(rank); }
            if (medal && o.Medal == "metal" && (o.Num == null || o.Num == "filled")) L.Fill = MedalText(rank);

            SKPoint Place(float bw, float bh) => pos switch
            {
                "tr" => new SKPoint(w - m - bw, m),
                "bl" => new SKPoint(m, h - m - bh),
                "br" => new SKPoint(w - m - bw, h - m - bh),
                "bc" => new SKPoint((w - bw) / 2, h - m - bh),
                _ => new SKPoint(m, m),
            };

            // The shape as a path (none for number-only looks); the number's box inside it.
            SKPath? path = null; SKRect numBox = default; string label = txt; float rot = 0; SKPoint rotAt = default; SKRect? square = null;
            switch (shape)
            {
                case "circle":
                { var pt = Place(2 * r, 2 * r); path = new SKPath(); path.AddCircle(pt.X + r, pt.Y + r, r); numBox = SKRect.Create(pt.X + r - r * 0.68f, pt.Y + r - r * 0.48f, r * 1.36f, r * 0.96f); break; }
                case "roundsq":
                { var pt = Place(2 * r, 2 * r); var box = SKRect.Create(pt.X, pt.Y, 2 * r, 2 * r); path = new SKPath(); path.AddRoundRect(box, r * 0.3f, r * 0.3f); numBox = SKRect.Create(box.MidX - r * 0.75f, box.MidY - r * 0.5f, r * 1.5f, r * 1.0f); break; }
                case "pill":
                {
                    label = "#" + txt; float bh = r * 1.2f;
                    using var tp = new SKPaint { Typeface = L.Face, IsAntialias = true }; FitCentre(tp, label, 0, 0, 99999, bh * 0.62f);
                    float bw = Ink(tp, label).Width + bh * 0.9f;
                    var pt = Place(bw, bh); var box = SKRect.Create(pt.X, pt.Y, bw, bh);
                    path = new SKPath(); path.AddRoundRect(box, bh / 2, bh / 2); numBox = SKRect.Create(box.Left + bh * 0.45f, box.MidY - bh * 0.31f, bw - bh * 0.9f + 1, bh * 0.62f); break;
                }
                case "ribbon":
                {
                    float bw = r * 1.45f, bh = r * 2.6f, notch = r * 0.5f;
                    float x = right ? w - m * 1.5f - bw : pos == "bc" ? (w - bw) / 2 : m * 1.5f;
                    path = new SKPath(); path.MoveTo(x, 0); path.LineTo(x + bw, 0); path.LineTo(x + bw, bh); path.LineTo(x + bw / 2, bh - notch); path.LineTo(x, bh); path.Close();
                    float cy = (bh - notch) * 0.55f; numBox = SKRect.Create(x + bw * 0.11f, cy - r * 0.475f, bw * 0.78f, r * 0.95f); break;
                }
                case "banner":
                {
                    label = "#" + txt; float bh = r * 1.35f, bw = r * 2.5f, notch = r * 0.45f;
                    float y = pos == "bl" || pos == "br" || pos == "bc" ? h - m * 2.2f - bh : m * 2.2f;
                    path = new SKPath();
                    if (!right) { path.MoveTo(0, y); path.LineTo(bw, y); path.LineTo(bw - notch, y + bh / 2); path.LineTo(bw, y + bh); path.LineTo(0, y + bh); }
                    else { path.MoveTo(w, y); path.LineTo(w - bw, y); path.LineTo(w - bw + notch, y + bh / 2); path.LineTo(w - bw, y + bh); path.LineTo(w, y + bh); }
                    path.Close();
                    float cx = right ? w - (bw + notch) / 2 + notch * 0.2f : (bw - notch) / 2 + m * 0.3f;
                    float mw = bw - notch - m; numBox = SKRect.Create(cx - mw / 2, y + bh / 2 - bh * 0.31f, mw, bh * 0.62f); break;
                }
                case "diag":
                {
                    float d = r * 2.1f, t = r * 1.05f;
                    bool bottom = pos == "bl" || pos == "br" || pos == "bc";
                    rotAt = new SKPoint(right ? w - d / 1.4142f : d / 1.4142f, bottom ? h - d / 1.4142f : d / 1.4142f);
                    rot = (right ? 45 : -45) * (bottom ? -1 : 1);
                    path = new SKPath(); path.AddRect(new SKRect(-d * 2, -t / 2, d * 2, t / 2));
                    numBox = SKRect.Create(-r * 0.8f, -t * 0.36f, r * 1.6f, t * 0.72f); break;
                }
                case "netflix":
                {
                    // The one TOP 10 square (the real posters' drawing), "#rank" on its second line, flush in the corner.
                    float bw = SquareWidth(s, o.Size);
                    if (rank == 1 && o.Special == "bigger") bw *= 1.3f;
                    var sq = SquareRect(w, h, bw, true, pos);
                    square = SKRect.Create(sq.Left, sq.Top, bw, bw * 1.12f);
                    path = new SKPath(); path.AddRect(sq); break;
                }
            }

            c.Save();
            if (rot != 0) { c.Translate(rotAt.X, rotAt.Y); c.RotateDegrees(rot); }
            if (path != null)
            {
                var pb = path.Bounds;
                if (rank == 1 && o.Special == "glow")
                    using (var glow = new SKPaint { Color = new SKColor(0xFF, 0xC8, 0x3A, 235), MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, r * 0.25f), IsAntialias = true, Style = SKPaintStyle.StrokeAndFill, StrokeWidth = r * 0.18f })
                        c.DrawPath(path, glow);
                if (o.Shadow == true)
                    using (var sh = new SKPaint { Color = SKColors.Black.WithAlpha(150), MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, r * 0.12f), IsAntialias = true })
                    { c.Save(); c.Translate(r * 0.05f, r * 0.1f); c.DrawPath(path, sh); c.Restore(); }
                if (square is SKRect sqr)
                    DrawLogoBox(c, sqr, rank, o.Colour != null ? BadgeOptions.Hex(o.Colour, LogoRed) : LogoRed);
                else
                {
                using var bg = new SKPaint { Color = bgColour, IsAntialias = true };
                if (medal && o.Medal == "metal")
                {
                    bg.Color = SKColors.White;
                    bg.Shader = SKShader.CreateLinearGradient(new SKPoint(pb.Left, pb.Top), new SKPoint(pb.Right, pb.Bottom), MedalStops(rank), new[] { 0f, 0.5f, 1f }, SKShaderTileMode.Clamp);
                }
                c.DrawPath(path, bg);
                if (medal && o.Medal == "metal")
                    using (var ring = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = r * 0.09f, Color = MedalStops(rank)[0].WithAlpha(230), IsAntialias = true })
                    { c.Save(); c.ClipPath(path, SKClipOperation.Intersect, true); c.DrawPath(path, ring); c.Restore(); }
                using var tp = new SKPaint { Typeface = L.Face, IsAntialias = true };
                var origin = FitCentre(tp, label, numBox.MidX, numBox.MidY, numBox.Width, numBox.Height);
                DrawNumber(c, label, origin.X, origin.Y, tp.TextSize, L);
                }
            }
            else if (shape == "numonly")
            {
                float nh = r * 1.5f;
                using var tp = new SKPaint { Typeface = L.Face, IsAntialias = true }; FitCentre(tp, txt, 0, 0, 99999, nh);
                var ib = Ink(tp, txt);
                var pt = Place(ib.Width, nh);
                if (o.Shadow != false) L.Shadow = true; L.ShadowBlur = r * 0.14f;
                if (L.Stroke <= 0 && o.Num == null) { L.Stroke = r * 0.03f; L.Out = SKColors.Black.WithAlpha(200); }
                if (medal && L.Gradient == null) L.Fill = MedalFlat(rank).WithAlpha(255);
                if (medal && o.Medal == "metal") { var st = MedalStops(rank); L.Gradient = new[] { st[0], st[1], st[2], st[0], st[1] }; L.GradientPos = ChromePos; }
                DrawNumber(c, txt, pt.X - ib.Left, pt.Y - ib.Top, tp.TextSize, L);
            }
            else if (shape == "bigoutline")
            {
                using (var g = new SKPaint { Shader = SKShader.CreateLinearGradient(new SKPoint(0, h * 0.55f), new SKPoint(0, h), new[] { SKColors.Transparent, SKColors.Black.WithAlpha(200) }, null, SKShaderTileMode.Clamp) })
                    c.DrawRect(0, h * 0.55f, w, h * 0.45f, g);
                using var tp = new SKPaint { Typeface = BadgeFonts.Face(o.Font ?? "anton"), IsAntialias = true }; FitCentre(tp, txt, 0, 0, w * 0.8f, h * 0.30f);
                var ib = Ink(tp, txt);
                float ox = right ? w - m - ib.Right - s * 0.01f : pos == "bc" ? (w - ib.Width) / 2 - ib.Left : m - ib.Left + s * 0.01f;
                float oy = h - m - ib.Bottom;
                var BL = new NumLook { Face = tp.Typeface, Out = BadgeOptions.Hex(o.OutColour, SKColors.White), Shadow = o.Shadow == true, ShadowBlur = s * 0.02f };
                ApplyNumStyle(BL, o.Num ?? "outline", BadgeOptions.Hex(o.NumColour, SKColors.Black).WithAlpha(o.NumColour == null ? (byte)150 : (byte)255), rank, o.Medal, s * 0.007f * StrokeFactor(o.Outline));
                DrawNumber(c, txt, ox, oy, tp.TextSize, BL);
            }
            var bounds = path != null ? path.Bounds : SKRect.Create(m, m, 2 * r, 2 * r);
            c.Restore();
            if (rot != 0) bounds = SKRect.Create(right ? w - r * 2.4f : 0, (pos == "bl" || pos == "br" || pos == "bc") ? h - r * 2.4f : 0, r * 2.4f, r * 2.4f);
            if (rank == 1 && o.Special == "crown" && shape != "hidden")
            {
                float cw = Math.Max(r * 1.2f, s * 0.12f), ch = cw * 0.62f;
                DrawCrown(c, bounds.MidX, Math.Max(2, bounds.Top - ch * 0.55f), cw, ch);
            }

            // Extras beside the badge: movement chip, TOP 10 logo (top corner opposite the badge).
            SKRect? logoBox = o.Logo == true ? NetflixLogoRect(w, h, right ? "tl" : "tr", o, rank) : (SKRect?)null;
            var mv = o.MoveChip(ex);
            if (mv != null)
            {
                float ch = Math.Max(s * 0.07f, r * 0.62f);
                float cw = Chip(c, mv, 0, 0, ch, true);
                float x = right ? bounds.Left - m * 0.6f - cw : bounds.Right + m * 0.6f;
                if (shape == "hidden") x = right ? w - m - cw : m;
                float y = shape == "hidden" ? (pos.StartsWith("b") ? h - m - ch : m) : bounds.MidY - ch / 2;
                if (x < 0 || x + cw > w || (logoBox is SKRect lb && lb.IntersectsWith(SKRect.Create(x, y, cw, ch)))) { x = Math.Max(m, Math.Min(w - m - cw, bounds.Left)); y = pos.StartsWith("b") ? bounds.Top - ch - m * 0.5f : bounds.Bottom + m * 0.5f; }
                Chip(c, mv, x, y, ch);
            }
            if (logoBox is SKRect lr) DrawLogoBox(c, SKRect.Create(lr.Left, lr.Top, lr.Width, lr.Width * 1.12f), o.LogoRank(rank));
        }

        // ── Top 10 tile (customised) ─────────────────────────────────────────────────────
        private const int TW = TopTenTileRenderer.Width, TH = TopTenTileRenderer.Height, TileCardH = 610, TileNumeralBoxW = 640;
        private static readonly SKColor TileOutline = new SKColor(150, 150, 160);

        private static NumLook TileNum(BadgeOptions o, int rank, SKColor bgFill)
        {
            string style = o.Num ?? "outline";
            int stroke = o.Outline == "thin" ? 4 : o.Outline == "thick" ? 18 : 9;
            var L = new NumLook { Face = BadgeFonts.Face(o.Font ?? "anton"), Out = BadgeOptions.Hex(o.OutColour, TileOutline), Shadow = o.Shadow == true, ShadowBlur = 18 };
            var colour = o.NumColour != null ? BadgeOptions.Hex(o.NumColour, bgFill) : style == "outline" ? bgFill : style == "gradient" ? new SKColor(0x52, 0xB5, 0x4B) : new SKColor(0xEC, 0xEC, 0xF0);
            ApplyNumStyle(L, style, colour, rank, o.Medal, stroke);
            if (style == "metallic" || (o.Medal == "metal" && rank <= 3)) L.Stroke = 3;
            if (rank == 1 && o.Special == "glow") L.Out = new SKColor(0xFF, 0xD8, 0x6A);
            if (rank == 1 && o.Special == "bigger" && style == "outline" && o.NumColour == null) { L.Stroke = 0; L.Fill = new SKColor(0xEC, 0xEC, 0xF0); }
            return L;
        }

        private static float TileNumeralSize(SKTypeface face, float maxW, float maxCap, float stroke)
        {
            float size = 1100;
            while (size > 50)
            {
                using var p = new SKPaint { Typeface = face, TextSize = size };
                var b = Ink(p, "10");
                if (b.Width + stroke * 2 <= maxW && -b.Top + stroke <= maxCap) return size;
                size -= 4;
            }
            return size;
        }

        private static SKColor TileBackground(SKCanvas c, SKBitmap poster, int w, int h, BadgeOptions o)
        {
            if (o.TileBg == "solid") { var col = BadgeOptions.Hex(o.TileBgColour, new SKColor(0x16, 0x22, 0x3A)); c.Clear(col); return col; }
            if (o.TileBg == "blur") { c.Clear(TopTenTileRenderer.PageBackground); DrawBackdrop(c, poster, w, h); return TopTenTileRenderer.PageBackground; }
            c.Clear(TopTenTileRenderer.PageBackground);
            return TopTenTileRenderer.PageBackground;
        }

        private static void Glow(SKCanvas c, string num, float x, float y, float size, SKTypeface face, float scaleX, float stroke)
        {
            using var glow = new SKPaint { Typeface = face, TextSize = size, TextAlign = SKTextAlign.Right, TextScaleX = scaleX, IsAntialias = true,
                Color = new SKColor(0xFF, 0xC8, 0x3A, 230), Style = SKPaintStyle.Stroke, StrokeWidth = Math.Max(20, stroke * 5), MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 30) };
            c.DrawText(num, x, y, glow);
        }

        internal static void RenderTile(string posterPath, int rank, string outputPath, BadgeOptions o, RankExtras? ex)
        {
            ex ??= new RankExtras();
            using var poster = SKBitmap.Decode(posterPath)
                ?? throw new InvalidOperationException($"SkiaSharp could not decode '{posterPath}' (unsupported format or corrupt file)");
            using var surface = SKSurface.Create(new SKImageInfo(TW, TH, SKColorType.Rgba8888, SKAlphaType.Premul))
                ?? throw new InvalidOperationException($"SkiaSharp could not create a {TW}x{TH} surface");
            var c = surface.Canvas;
            var bgFill = TileBackground(c, poster, TW, TH, o);
            var L = TileNum(o, rank, bgFill);
            bool behind = o.TilePos == "behind", bigger = rank == 1 && o.Special == "bigger";
            int cardH = bigger ? 660 : TileCardH;
            int pw = (int)(cardH / 1.5), py = (TH - cardH) / 2;
            float stroke = Math.Max(L.Stroke, 9);
            float size = bigger ? TileNumeralSize(L.Face, 760, 690, stroke)
                       : behind ? TileNumeralSize(L.Face, 760, cardH * 1.08f, stroke) : TileNumeralSize(L.Face, TileNumeralBoxW, TileCardH * 1.02f, stroke);
            var num = rank.ToString();
            using var measure = new SKPaint { Typeface = L.Face, TextSize = size };
            int numW = (int)(Ink(measure, num).Width + stroke * 2);
            int overlap = behind ? (int)Math.Max(numW * 0.32f, pw * 0.12f) : (int)(pw * 0.05);
            int groupW = numW - overlap + pw;
            int px = (TW - groupW) / 2 + numW - overlap;
            px = Math.Min(px, TW - 90 - pw);
            float textX = px + overlap, textY = py + (bigger ? cardH + 10 : behind ? cardH + 18 : cardH);
            if (rank == 1 && o.Special == "glow") Glow(c, num, textX, textY, size, L.Face, 1, L.Stroke);
            DrawNumber(c, num, textX, textY, size, L, SKTextAlign.Right);
            if (rank == 1 && o.Special == "crown")
            {
                var nb = Ink(measure, num);
                float top = textY + Ink(measure, "8").Top;
                DrawCrown(c, textX - measure.MeasureText(num) + nb.MidX - 10, Math.Max(4, top - 62), 170, 105);
            }
            var card = new SKRect(px, py, px + pw, py + cardH);
            DrawCard(c, poster, card, RadiusFor(pw));
            DrawStatLines(c, card, ex, o, cardH * 0.065f, true);
            if (o.Logo == true) NetflixLogoOn(c, card, o, rank);
            if (o.MoveChip(ex) is string mv) Chip(c, mv, 24, 16, 92);
            Save(surface, outputPath);
        }

        internal static void RenderCard(string posterPath, int rank, string outputPath, BadgeOptions o, RankExtras? ex)
        {
            ex ??= new RankExtras();
            const int w = TopTenTileRenderer.PosterWidth, h = TopTenTileRenderer.PosterHeight, margin = 16;
            using var poster = SKBitmap.Decode(posterPath)
                ?? throw new InvalidOperationException($"SkiaSharp could not decode '{posterPath}' (unsupported format or corrupt file)");
            using var surface = SKSurface.Create(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul))
                ?? throw new InvalidOperationException($"SkiaSharp could not create a {w}x{h} surface");
            var c = surface.Canvas;
            var bgFill = TileBackground(c, poster, w, h, o);
            var L = TileNum(o, rank, bgFill);
            int crop = (h - w * 3 / 4) / 2;
            int py = crop + margin, ph = h - py * 2, pw = (int)(ph / 1.5), px = w - margin - pw;
            int overlap = (int)(pw * (o.TilePos == "behind" ? 0.22 : 0.10));
            int safe = crop + 27;
            float stroke = Math.Max(L.Stroke, 9);
            float size = 1400;
            while (size > 50) { using var pp = new SKPaint { Typeface = L.Face, TextSize = size }; if (-Ink(pp, "8").Top + stroke <= h - safe * 2) break; size -= 4; }
            var num = rank.ToString();
            using var measure = new SKPaint { Typeface = L.Face, TextSize = size };
            float room = px + overlap - margin;
            float inkW = Ink(measure, num).Width + stroke * 2;
            float sx = inkW > room ? room / inkW : 1;
            if (rank == 1 && o.Special == "glow") Glow(c, num, px + overlap, h - safe, size, L.Face, sx, L.Stroke);
            DrawNumber(c, num, px + overlap, h - safe, size, L, SKTextAlign.Right, sx);
            if (rank == 1 && o.Special == "crown")
            {
                measure.TextScaleX = sx;
                var nb = Ink(measure, num);
                float x0 = px + overlap - measure.MeasureText(num);
                DrawCrown(c, x0 + nb.MidX, safe + 4, 230, 140);
            }
            var card = new SKRect(px, py, px + pw, py + ph);
            DrawCard(c, poster, card, RadiusFor(pw));
            DrawStatLines(c, card, ex, o, ph * 0.055f, true);
            if (o.Logo == true) NetflixLogoOn(c, card, o, rank);
            if (o.MoveChip(ex) is string mv) Chip(c, mv, margin + 10, crop + margin + 6, 110);
            Save(surface, outputPath);
        }

        // ── #1 crown ─────────────────────────────────────────────────────────────────────
        private static void DrawCrown(SKCanvas c, float cx, float top, float cw, float ch)
        {
            using var p = new SKPath();
            float l = cx - cw / 2, r = cx + cw / 2, b = top + ch;
            p.MoveTo(l, b); p.LineTo(l - cw * 0.04f, top + ch * 0.18f); p.LineTo(l + cw * 0.27f, top + ch * 0.55f); p.LineTo(cx, top);
            p.LineTo(r - cw * 0.27f, top + ch * 0.55f); p.LineTo(r + cw * 0.04f, top + ch * 0.18f); p.LineTo(r, b); p.Close();
            c.Save(); c.RotateDegrees(-12, cx, b);
            using (var sh = new SKPaint { Color = SKColors.Black.WithAlpha(180), MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, ch * 0.08f), IsAntialias = true })
            { c.Save(); c.Translate(ch * 0.04f, ch * 0.08f); c.DrawPath(p, sh); c.Restore(); }
            using (var f = new SKPaint { IsAntialias = true, Shader = SKShader.CreateLinearGradient(new SKPoint(0, top), new SKPoint(0, b), new[] { new SKColor(0xFF, 0xF0, 0x9A), new SKColor(0xE0, 0xA8, 0x20), new SKColor(0x9A, 0x6A, 0x08) }, null, SKShaderTileMode.Clamp) })
                c.DrawPath(p, f);
            using (var st = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = Math.Max(1, ch * 0.04f), Color = new SKColor(0x5A, 0x3C, 0x00), StrokeJoin = SKStrokeJoin.Round })
                c.DrawPath(p, st);
            using (var gem = new SKPaint { IsAntialias = true, Color = new SKColor(0xD0, 0x20, 0x30) })
                c.DrawCircle(cx, top + ch * 0.68f, ch * 0.1f, gem);
            using (var dot = new SKPaint { IsAntialias = true, Color = new SKColor(0xFF, 0xF4, 0xC0) })
            { c.DrawCircle(cx, top, ch * 0.07f, dot); c.DrawCircle(l - cw * 0.04f, top + ch * 0.18f, ch * 0.07f, dot); c.DrawCircle(r + cw * 0.04f, top + ch * 0.18f, ch * 0.07f, dot); }
            c.Restore();
        }

        // ── extras ───────────────────────────────────────────────────────────────────────
        // Movement chip: "+2" (up), "-1" (down), "NEW", "=". Returns its width.
        private static float Chip(SKCanvas c, string mv, float x, float y, float hgt, bool measureOnly = false)
        {
            SKColor col; string label; int dir = 0;
            if (mv == "NEW") { col = new SKColor(0xE5, 0x9E, 0x10); label = "NEW"; }
            else if (mv == "=") { col = new SKColor(0x70, 0x70, 0x7A); label = "="; }
            else if (mv.StartsWith("+")) { col = new SKColor(0x2E, 0xA0, 0x43); label = mv.Substring(1); dir = 1; }
            else { col = new SKColor(0xD0, 0x3A, 0x3A); label = mv.TrimStart('-'); dir = -1; }
            using var tp = new SKPaint { Typeface = BadgeFonts.Face("roboto"), Color = SKColors.White, IsAntialias = true };
            FitCentre(tp, "0", 0, 0, 9999, hgt * 0.5f);
            var lb = Ink(tp, label == "=" ? "0" : label);
            float tri = dir != 0 ? hgt * 0.5f : 0, gap = dir != 0 ? hgt * 0.14f : 0, pad = hgt * 0.32f;
            float wdt = pad * 2 + tri + gap + lb.Width;
            if (measureOnly) return wdt;
            using var bg = new SKPaint { Color = col, IsAntialias = true };
            using (var sh = new SKPaint { Color = SKColors.Black.WithAlpha(150), MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, hgt * 0.08f), IsAntialias = true })
                c.DrawRoundRect(SKRect.Create(x, y + hgt * 0.05f, wdt, hgt), hgt * 0.22f, hgt * 0.22f, sh);
            c.DrawRoundRect(SKRect.Create(x, y, wdt, hgt), hgt * 0.22f, hgt * 0.22f, bg);
            float cy = y + hgt / 2, tx = x + pad;
            using var white = new SKPaint { Color = SKColors.White, IsAntialias = true };
            if (dir != 0)
            {
                using var path = new SKPath();
                if (dir > 0) { path.MoveTo(tx, cy + tri * 0.43f); path.LineTo(tx + tri, cy + tri * 0.43f); path.LineTo(tx + tri / 2, cy - tri * 0.43f); }
                else { path.MoveTo(tx, cy - tri * 0.43f); path.LineTo(tx + tri, cy - tri * 0.43f); path.LineTo(tx + tri / 2, cy + tri * 0.43f); }
                path.Close();
                c.DrawPath(path, white);
            }
            if (label == "=")
            {
                float th = hgt * 0.08f;
                c.DrawRect(tx, cy - th * 2, lb.Width, th, white); c.DrawRect(tx, cy + th, lb.Width, th, white);
            }
            else
            {
                var lb2 = Ink(tp, label);
                c.DrawText(label, tx + tri + gap - lb2.Left, cy - lb2.MidY, tp);
            }
            return wdt;
        }

        // The "N wks" and "▶ N plays" lines at the bottom of an area (the poster, or the tile's card).
        private static void DrawStatLines(SKCanvas c, SKRect area, RankExtras ex, BadgeOptions o, float th, bool rounded)
        {
            string? weeks = o.Weeks == true && ex.Weeks != null ? (ex.Weeks == 1 ? "1st week" : ex.Weeks + " wks in list") : null;
            string? plays = o.Plays == true && ex.Plays != null ? ex.Plays + (ex.Plays == 1 ? " play" : " plays") : null;
            if (weeks == null && plays == null) return;
            int lines = (weeks != null ? 1 : 0) + (plays != null ? 1 : 0);
            float gh = th * (2.4f + 1.4f * lines);
            c.Save();
            if (rounded) c.ClipRoundRect(new SKRoundRect(area, RadiusFor((int)area.Width)), SKClipOperation.Intersect, true);
            using (var g = new SKPaint { Shader = SKShader.CreateLinearGradient(new SKPoint(0, area.Bottom - gh), new SKPoint(0, area.Bottom), new[] { SKColors.Transparent, SKColors.Black.WithAlpha(225) }, null, SKShaderTileMode.Clamp) })
                c.DrawRect(area.Left, area.Bottom - gh, area.Width, gh, g);
            c.Restore();
            using var tp = new SKPaint { Typeface = BadgeFonts.Face("roboto"), Color = SKColors.White, IsAntialias = true };
            FitCentre(tp, "0", 0, 0, 9999, th);
            float x = area.Left + th * 0.8f, baseY = area.Bottom - th * 0.9f;
            if (plays != null)
            {
                float s = th, px = x;
                using (var p = new SKPath())
                {
                    p.MoveTo(px, baseY - s); p.LineTo(px + s * 0.88f, baseY - s / 2); p.LineTo(px, baseY); p.Close();
                    using var green = new SKPaint { Color = new SKColor(0x52, 0xB5, 0x4B), IsAntialias = true };
                    c.DrawPath(p, green);
                }
                c.DrawText(plays, px + s * 1.35f, baseY, tp);
                baseY -= th * 1.6f;
            }
            if (weeks != null)
            {
                tp.Color = new SKColor(225, 225, 230);
                c.DrawText(weeks.ToUpperInvariant(), x, baseY, tp);
            }
        }

        // Netflix-style red "TOP 10" logo in a top corner of the poster: the whole area it covers
        // (with the "#rank" line when the label asks for it). Size M = the original 17% of the short side.
        private static SKRect NetflixLogoRect(int w, int h, string corner, BadgeOptions o, int rank)
        {
            float s = Math.Min(w, h), bw = s * 0.17f * o.LogoScale;
            return SKRect.Create(corner == "tl" ? 0 : w - bw, 0, bw, LogoBoxHeight(bw, o.LogoRank(rank) != null));
        }
        private static void NetflixLogoOn(SKCanvas c, SKRect card, BadgeOptions o, int rank)
        {
            float bw = card.Width * 0.2f * o.LogoScale, bh = bw * 1.12f;
            c.Save();
            c.ClipRoundRect(new SKRoundRect(card, RadiusFor((int)card.Width)), SKClipOperation.Intersect, true);
            DrawLogoBox(c, SKRect.Create(card.Right - bw, card.Top, bw, bh), o.LogoRank(rank));
            c.Restore();
        }
        private static float LogoBoxHeight(float bw, bool withRank) => bw * 1.12f + (withRank ? bw * 0.5f : 0);
        // The TOP 10 logo in "logo" (the TOP / 10 square), with "#rank" on a red second line under it
        // when rank is given (the real posters' "TOP 10 #3" label and the list tiles' Logo label).
        private static readonly SKColor LogoRed = new SKColor(0xE5, 0x09, 0x14);
        private static void DrawLogoBox(SKCanvas c, SKRect logo, int? rank, SKColor? colour = null)
        {
            var red = colour ?? LogoRed;
            if (rank == null) { DrawLogo(c, logo, red); return; }
            float bw = logo.Width, bh = logo.Height, rh = bw * 0.5f;
            using (var bg = new SKPaint { Color = red, IsAntialias = true })
                c.DrawRect(SKRect.Create(logo.Left, logo.Top, bw, bh + rh), bg);
            DrawLogo(c, logo, red);
            using (var line = new SKPaint { Color = SKColors.White.WithAlpha(150), IsAntialias = true })
                c.DrawRect(SKRect.Create(logo.Left + bw * 0.2f, logo.Top + bh - bw * 0.02f, bw * 0.6f, Math.Max(1f, bw * 0.02f)), line);
            using var t = new SKPaint { Typeface = BadgeFonts.Face("roboto"), Color = SKColors.White, IsAntialias = true };
            DrawFit(c, "#" + rank, t, logo.MidX, logo.Top + bh + rh * 0.45f, bw * 0.7f, rh * 0.62f);
        }
        // TOP 10 square width: S / M / L (the real posters' sizes) and XL, a share of the image's short side.
        private static float SquareWidth(float shortSide, string? size) => shortSide * (size == "s" ? 0.14f : size == "l" ? 0.24f : size == "xl" ? 0.30f : 0.18f);
        // The square (with its "#rank" line when withRank) flush in a corner: tl, tr, bl, br or bc.
        private static SKRect SquareRect(int w, int h, float bw, bool withRank, string pos)
        {
            float bh = LogoBoxHeight(bw, withRank);
            float x = pos == "tr" || pos == "br" ? w - bw : pos == "bc" ? (w - bw) / 2f : 0;
            float y = pos == "bl" || pos == "br" || pos == "bc" ? h - bh : 0;
            return SKRect.Create(x, y, bw, bh);
        }
        private static void DrawLogo(SKCanvas c, SKRect box, SKColor red)
        {
            using var bg = new SKPaint { Color = red, IsAntialias = true };
            c.DrawRect(box, bg);
            using var t1 = new SKPaint { Typeface = BadgeFonts.Face("roboto"), Color = SKColors.White, IsAntialias = true };
            DrawFit(c, "TOP", t1, box.MidX, box.Top + box.Height * 0.3f, box.Width * 0.62f, box.Height * 0.2f);
            using var t2 = new SKPaint { Typeface = BadgeFonts.Face("roboto"), Color = SKColors.White, IsAntialias = true };
            DrawFit(c, "10", t2, box.MidX, box.Top + box.Height * 0.66f, box.Width * 0.7f, box.Height * 0.36f);
        }

        /// <summary>
        /// "Badge the real posters too": the red TOP 10 square (DrawLogoBox) flush in a corner of the
        /// real poster (tl, tr, bl, bc, br), with "#rank" on a second line when withRank. size s/m/l
        /// ≈ 14/18/24% of the poster width. Writes a new JPEG; the source file is only read.
        /// </summary>
        internal static void RealPosterBadge(string sourcePath, string outputPath, int rank, bool withRank, string size, string corner)
        {
            using var src = SKBitmap.Decode(sourcePath)
                ?? throw new InvalidOperationException($"SkiaSharp could not decode '{sourcePath}'");
            int w = src.Width, h = src.Height;
            using var surface = SKSurface.Create(new SKImageInfo(w, h))
                ?? throw new InvalidOperationException($"SkiaSharp could not create a {w}x{h} surface");
            var c = surface.Canvas;
            c.DrawBitmap(src, 0, 0);
            // The same square as the Number badge's "TOP 10 square" shape (DrawBadge), in the chosen corner.
            float bw = SquareWidth(Math.Min(w, h), size);
            var sq = SquareRect(w, h, bw, withRank, corner);
            DrawLogoBox(c, SKRect.Create(sq.Left, sq.Top, bw, bw * 1.12f), withRank ? rank : (int?)null);
            using var img = surface.Snapshot();
            using var data = img.Encode(SKEncodedImageFormat.Jpeg, 92);
            using var fs = File.Create(outputPath);
            data.SaveTo(fs);
        }

        /// <summary>The real-poster badge at rank 7 on the stand-in poster (the Real posters block's icon), width px wide.</summary>
        internal static string RealPosterPreview((string Label, string Size, string Corner)? opt, string standInPoster, string tempDir, int width = 400)
        {
            var (label, size, corner) = opt ?? ("top10", "m", "tr");
            Directory.CreateDirectory(tempDir);
            var outPath = Path.Combine(tempDir, "real-preview-" + Guid.NewGuid().ToString("N") + ".jpg");
            try
            {
                // Rank 7 as the example, like the Badge Style tiles.
                RealPosterBadge(standInPoster, outPath, 7, label == "top10rank", size, corner);
                using var full = SKBitmap.Decode(outPath);
                int w = width, h = full.Height * w / full.Width;
                using var surface = SKSurface.Create(new SKImageInfo(w, h));
                using (var paint = new SKPaint { FilterQuality = SKFilterQuality.High, IsAntialias = true })
                    surface.Canvas.DrawBitmap(full, SKRect.Create(0, 0, w, h), paint);
                using var img = surface.Snapshot();
                using var data = img.Encode(SKEncodedImageFormat.Jpeg, 88);
                return "data:image/jpeg;base64," + Convert.ToBase64String(data.ToArray());
            }
            finally { try { if (File.Exists(outPath)) File.Delete(outPath); } catch { } }
        }

        // A 16:9 stand-in landscape cut from the middle of a poster (preview of a Thumb row).
        private static string LandscapeFromPoster(string poster, string outPath)
        {
            using var src = SKBitmap.Decode(poster);
            int w = src.Width, h = w * 9 / 16, y = Math.Max(0, (src.Height - h) / 2);
            using var surface = SKSurface.Create(new SKImageInfo(w, h));
            using (var paint = new SKPaint { FilterQuality = SKFilterQuality.High })
                surface.Canvas.DrawBitmap(src, new SKRect(0, y, w, y + h), new SKRect(0, 0, w, h), paint);
            using var img = surface.Snapshot();
            using var data = img.Encode(SKEncodedImageFormat.Jpeg, 90);
            File.WriteAllBytes(outPath, data.ToArray());
            return outPath;
        }

        // ── popup preview ────────────────────────────────────────────────────────────────
        /// <summary>
        /// One card for the Customise popup: rank 7 drawn by the real code with the popup's look on
        /// a stand-in poster (no library posters, so it is quick). variant "thumb" = the landscape
        /// image a Thumb row uses; anything else = the Primary poster (card). JPEG data: URL, scaled
        /// to width px (0 = the preview's 400 / 600); the shape tiles are this drawing, small.
        /// </summary>
        internal static string Preview(string look, string standInPoster, string tempDir, string? variant, int width = 0)
        {
            var (style, json) = BadgeLook.Split(look);
            var o = BadgeOptions.Parse(json);
            bool tile = string.Equals(style, "top10", StringComparison.OrdinalIgnoreCase);
            bool thumb = variant == "thumb";
            const int rank = 7;   // the example rank, like the Badge Style tiles
            // Sample movement: "=" when the list shows unchanged ranks, so the tick box is visible in the preview.
            var extras = new RankExtras { Move = o.MoveEq == true ? "=" : "+2", Weeks = 5, Plays = 48, Sample = true };
            Directory.CreateDirectory(tempDir);
            var ob = Path.Combine(tempDir, "badge-preview-" + Guid.NewGuid().ToString("N"));
            var outPath = ob + (thumb ? "-thumb.jpg" : ".jpg");
            try
            {
                if (tile)
                {
                    bool custom = !o.IsDefault;
                    if (thumb) { if (custom) RenderTile(standInPoster, rank, outPath, o, extras); else TopTenTileRenderer.Render(standInPoster, rank, outPath); }
                    else { if (custom) RenderCard(standInPoster, rank, outPath, o, extras); else TopTenTileRenderer.RenderPoster(standInPoster, rank, outPath); }
                }
                else if (thumb)
                    RenderRanked(null, StandInLandscape(standInPoster, tempDir), rank, ob, look, extras, (w, ex) => throw ex);
                else
                    RenderRanked(standInPoster, null, rank, ob, look, extras, (w, ex) => throw ex);

                using var full = SKBitmap.Decode(outPath);
                // The Top 10 poster card shows in a 4:3 card (Emby crops its top and bottom): show that part.
                var src = SKRect.Create(0, 0, full.Width, full.Height);
                if (tile && !thumb) { float ch = full.Width * 3f / 4f; src = SKRect.Create(0, (full.Height - ch) / 2f, full.Width, ch); }
                int w = width > 0 ? width : thumb || tile ? 600 : 400, h = (int)(src.Height * w / src.Width);
                using var surface = SKSurface.Create(new SKImageInfo(w, h));
                using (var paint = new SKPaint { FilterQuality = SKFilterQuality.High, IsAntialias = true })
                    surface.Canvas.DrawBitmap(full, src, SKRect.Create(0, 0, w, h), paint);
                using var img = surface.Snapshot();
                using var data = img.Encode(SKEncodedImageFormat.Jpeg, 88);
                return "data:image/jpeg;base64," + Convert.ToBase64String(data.ToArray());
            }
            finally
            {
                foreach (var f in new[] { ob + ".jpg", ob + "-thumb.jpg" }) try { if (File.Exists(f)) File.Delete(f); } catch { }
            }
        }

        // The stand-in landscape (cut from the stand-in poster), made once per folder.
        private static readonly object LandLock = new object();
        private static string StandInLandscape(string standInPoster, string dir)
        {
            var path = Path.Combine(dir, "stand-in-badge-land.jpg");
            lock (LandLock) if (!File.Exists(path)) LandscapeFromPoster(standInPoster, path);
            return path;
        }
    }
}
