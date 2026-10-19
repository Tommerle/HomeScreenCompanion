using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace HomeScreenCompanion
{
    /// <summary>
    /// The "Customise" settings of one generated poster or background (TagConfig.*PosterOptions /
    /// *BackgroundOptions): a small flat JSON object such as {"tilt":"left","font":"creepster"}.
    /// A missing key (or "" for the whole thing) means the style's own look, so art without
    /// options is drawn exactly as before. Keys:
    ///   tilt    straight | left | right
    ///   rows    1..10 (popup: 2-5 or custom) (missing = auto)
    ///   posters 1..100 titles               (missing = auto)
    ///   title   true | false               show the title
    ///   font    a font id (ArtFonts)
    ///   pos     tl tc tr ml mc mr bl bc br  title position
    ///   size    s | m | l
    ///   case    upper | typed
    ///   colour  #RRGGBB
    ///   darken  0..80 (percent)
    /// </summary>
    internal sealed class ArtOptions
    {
        public string? Tilt { get; set; }
        public int? Rows { get; set; }
        public int? Posters { get; set; }
        public bool? ShowTitle { get; set; }
        public string? Font { get; set; }
        public string? Pos { get; set; }
        public string? Size { get; set; }
        public string? Case { get; set; }
        public string? Colour { get; set; }
        public int? Darken { get; set; }

        public static readonly ArtOptions None = new ArtOptions();

        private static readonly string[] Positions = { "tl", "tc", "tr", "ml", "mc", "mr", "bl", "bc", "br" };

        public bool IsDefault => Tilt == null && Rows == null && Posters == null && ShowTitle == null && Font == null
            && Pos == null && Size == null && Case == null && Colour == null && Darken == null;

        // A tiny reader for the flat JSON object the config page writes; anything unknown or
        // out of range is ignored (= the style's own value).
        public static ArtOptions Parse(string? json)
        {
            var o = new ArtOptions();
            if (string.IsNullOrWhiteSpace(json)) return o;
            foreach (Match m in Regex.Matches(json, "\"(\\w+)\"\\s*:\\s*(\"(?:[^\"\\\\]|\\\\.)*\"|-?\\d+(?:\\.\\d+)?|true|false|null)"))
            {
                string key = m.Groups[1].Value.ToLowerInvariant(), raw = m.Groups[2].Value;
                string? str = raw.StartsWith("\"") ? raw.Substring(1, raw.Length - 2).Trim().ToLowerInvariant() : null;
                int? num = int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n
                         : str != null && int.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n2) ? n2 : (int?)null;
                switch (key)
                {
                    case "tilt": if (str == "straight" || str == "left" || str == "right") o.Tilt = str; break;
                    case "rows": if (num >= 1 && num <= CollectionArtRenderer.MaxRows) o.Rows = num; break;
                    case "posters": if (num >= 1 && num <= CollectionArtRenderer.MaxPosters) o.Posters = num; break;
                    case "title": if (raw == "true" || raw == "false") o.ShowTitle = raw == "true"; break;
                    case "font": if (str != null && ArtFonts.Exists(str)) o.Font = str; break;
                    case "pos": if (str != null && Positions.Contains(str)) o.Pos = str; break;
                    case "size": if (str == "s" || str == "m" || str == "l") o.Size = str; break;
                    case "case": if (str == "upper" || str == "typed") o.Case = str; break;
                    case "colour":
                    case "color": if (str != null && Regex.IsMatch(str, "^#[0-9a-f]{6}$")) o.Colour = str; break;
                    case "darken": if (num >= 0 && num <= 80) o.Darken = num; break;
                }
            }
            return o;
        }

        /// <summary>Stable text of the set fields (for the art cache key); "" when nothing is set.</summary>
        public string Canonical()
        {
            if (IsDefault) return "";
            var parts = new List<string>();
            void Add(string k, object? v) { if (v != null) parts.Add(k + "=" + Convert.ToString(v, CultureInfo.InvariantCulture)!.ToLowerInvariant()); }
            Add("tilt", Tilt); Add("rows", Rows); Add("posters", Posters); Add("title", ShowTitle); Add("font", Font);
            Add("pos", Pos); Add("size", Size); Add("case", Case); Add("colour", Colour); Add("darken", Darken);
            return string.Join(";", parts);
        }

        public SKColor? ColourValue => Colour != null && SKColor.TryParse(Colour, out var c) ? c : (SKColor?)null;

        public float SizeFactor => Size == "s" ? 0.72f : Size == "l" ? 1.3f : 1f;
    }

    /// <summary>What each Customise field is for one style when nothing is set (today's look),
    /// and a short note for the fields the style does not use. Shown as the popup's starting values.</summary>
    public class ArtOptionDefaults
    {
        public string Tilt { get; set; } = "straight";
        public int Rows { get; set; }      // 0 = auto
        public int Posters { get; set; }   // 0 = auto
        public int AutoPosters { get; set; }
        public bool ShowTitle { get; set; } = true;
        public string Font { get; set; } = "bebas";
        public string Pos { get; set; } = "bc";
        public string Size { get; set; } = "m";
        public string Case { get; set; } = "upper";
        public string Colour { get; set; } = "#ffffff";
        public int Darken { get; set; }
        public Dictionary<string, string> Hints { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>The fonts the art can use: embedded under HomeScreenCompanion.Fonts (licences next to them).</summary>
    internal static class ArtFonts
    {
        public sealed class Entry
        {
            public Entry(string id, string name, string group, string file) { Id = id; Name = name; Group = group; File = file; Face = new Lazy<SKTypeface>(() => ArtCanvas.LoadFont(file)); }
            public string Id { get; }
            public string Name { get; }
            public string Group { get; }
            public string File { get; }
            public Lazy<SKTypeface> Face { get; }
        }

        public static readonly Entry[] All =
        {
            new Entry("bebas", "Bebas Neue", "General", "BebasNeue.ttf"),
            new Entry("anton", "Anton", "General", "Anton.ttf"),
            new Entry("roboto", "Roboto", "General", "Roboto-Bold.ttf"),
            new Entry("dmserif", "DM Serif", "General", "DMSerifDisplay.ttf"),
            new Entry("alfaslab", "Alfa Slab", "General", "AlfaSlabOne.ttf"),
            new Entry("yesteryear", "Yesteryear", "General", "Yesteryear.ttf"),
            new Entry("creepster", "Creepster", "Horror", "Creepster.ttf"),
            new Entry("christmas", "Mountains of Christmas", "Christmas", "MountainsOfChristmas.ttf"),
            new Entry("orbitron", "Orbitron", "Sci-fi", "Orbitron.ttf"),
            new Entry("rye", "Rye", "Western", "Rye.ttf"),
            new Entry("luckiestguy", "Luckiest Guy", "Kids", "LuckiestGuy.ttf"),
            new Entry("greatvibes", "Great Vibes", "Romance", "GreatVibes.ttf"),
        };

        public static bool Exists(string id) => All.Any(f => f.Id == id);

        public static SKTypeface Face(string id) => (All.FirstOrDefault(f => f.Id == id) ?? All[0]).Face.Value;

        private static readonly Dictionary<string, string> SampleCache = new Dictionary<string, string>();

        /// <summary>The font's name drawn in the font itself (PNG data: URL), for the popup's font list.</summary>
        public static string Sample(Entry f)
        {
            lock (SampleCache) if (SampleCache.TryGetValue(f.Id, out var cached)) return cached;
            const int w = 360, h = 64;
            using var surface = SKSurface.Create(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul));
            var c = surface.Canvas;
            c.Clear(SKColors.Transparent);
            using var paint = new SKPaint { Typeface = f.Face.Value, TextSize = 40, IsAntialias = true, Color = SKColors.White, TextAlign = SKTextAlign.Center };
            float tw = paint.MeasureText(f.Name);
            if (tw > w - 16) paint.TextSize = 40 * (w - 16) / tw;
            var b = new SKRect(); paint.MeasureText(f.Name, ref b);
            c.DrawText(f.Name, w / 2f, h / 2f - b.MidY, paint);
            using var img = surface.Snapshot();
            using var data = img.Encode(SKEncodedImageFormat.Png, 100);
            var url = "data:image/png;base64," + Convert.ToBase64String(data.ToArray());
            lock (SampleCache) SampleCache[f.Id] = url;
            return url;
        }
    }
}
