using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Serialization;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace HomeScreenCompanion
{
    /// <summary>
    /// What a top list looked like before, for the badge extras: the ranks of the last two
    /// different rankings (for the movement chip) and the ISO weeks each title has been in the
    /// list without dropping out (for "N wks"). One small JSON file per list in
    /// &lt;plugin data&gt;/toplist_history. Updated every time the list's art is drawn; a title that
    /// leaves the list starts again at week 1 when it comes back. Until a list has been ranked
    /// twice there is no movement to show, so none is drawn. A list fed by a Popular rule instead
    /// compares with where each title stood one week ago (&lt;list&gt;.weekago.json, written by the run).
    /// </summary>
    internal static class TopListHistory
    {
        public sealed class HistoryFile
        {
            public Dictionary<string, int> Current { get; set; } = new Dictionary<string, int>();
            public Dictionary<string, int>? Previous { get; set; }
            public Dictionary<string, List<string>> Weeks { get; set; } = new Dictionary<string, List<string>>();
        }

        private static readonly object Gate = new object();

        public static Dictionary<Guid, RankExtras> Update(string listName, IList<BaseItem> ranked, string look,
            IJsonSerializer json, ILibraryManager libraryManager, IUserManager userManager, IUserDataManager userDataManager, Action<string>? log = null)
        {
            var result = new Dictionary<Guid, RankExtras>();
            try
            {
                var path = FileFor(listName, ".json");
                var ids = ranked.Select(i => i.Id.ToString("N")).ToList();
                var now = new Dictionary<string, int>();
                for (int i = 0; i < ids.Count; i++) if (!now.ContainsKey(ids[i])) now[ids[i]] = i + 1;

                HistoryFile? h = null;
                Dictionary<string, int>? weekAgo = null;
                lock (Gate)
                {
                    var weekAgoPath = FileFor(listName, ".weekago.json");
                    if (File.Exists(weekAgoPath))
                        try
                        {
                            var keys = json.DeserializeFromFile<List<string>>(weekAgoPath) ?? new List<string>();
                            weekAgo = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                            foreach (var k in keys) if (!string.IsNullOrEmpty(k) && !weekAgo.ContainsKey(k)) weekAgo[k] = weekAgo.Count + 1;
                        }
                        catch { weekAgo = null; }
                    if (File.Exists(path)) try { h = json.DeserializeFromFile<HistoryFile>(path); } catch { h = null; }
                    if (h == null) h = new HistoryFile { Current = now };
                    else if (!Same(h.Current, now)) { h.Previous = h.Current; h.Current = now; }

                    var week = IsoWeek(DateTime.UtcNow);
                    var weeks = new Dictionary<string, List<string>>();
                    foreach (var id in now.Keys)
                    {
                        var list = h.Weeks != null && h.Weeks.TryGetValue(id, out var w) && w != null ? w : new List<string>();
                        if (!list.Contains(week)) list.Add(week);
                        weeks[id] = list;
                    }
                    h.Weeks = weeks;
                    json.SerializeToFile(h, path);
                }

                Dictionary<Guid, int>? plays = null;
                if (BadgeLook.WantsPlays(look))
                    plays = new PopularityCounter(libraryManager, userManager, userDataManager).CountPlays(ranked.ToList());

                foreach (var item in ranked)
                {
                    var id = item.Id.ToString("N");
                    if (!now.TryGetValue(id, out var rank) || result.ContainsKey(item.Id)) continue;
                    var ex = new RankExtras { Weeks = h.Weeks.TryGetValue(id, out var wl) ? wl.Count : 1 };
                    if (weekAgo != null)
                        ex.Move = Move(weekAgo, HomeScreenCompanionTask.RankKey(item), rank);
                    else if (h.Previous != null)
                        ex.Move = Move(h.Previous, id, rank);
                    if (plays != null) ex.Plays = plays.TryGetValue(item.Id, out var p) ? p : 0;
                    result[item.Id] = ex;
                }
            }
            catch (Exception ex)
            {
                log?.Invoke($"Top-list '{listName}': rank history could not be updated — {ex.Message}");
            }
            return result;
        }

        // The ranking a week ago of a list fed by a Popular rule (rank keys, best first); null removes it.
        public static void SaveWeekAgo(string listName, List<string>? keys, IJsonSerializer json)
        {
            var path = FileFor(listName, ".weekago.json");
            lock (Gate)
            {
                if (keys != null) json.SerializeToFile(keys, path);
                else if (File.Exists(path)) File.Delete(path);
            }
        }

        private static string FileFor(string? listName, string suffix)
        {
            var dir = Path.Combine(Plugin.Instance!.DataFolderPath, "toplist_history");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, string.Concat((listName ?? "list").Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch)) + suffix);
        }

        private static string Move(Dictionary<string, int> before, string key, int rank)
            => !before.TryGetValue(key, out var was) ? "NEW" : was == rank ? "=" : was > rank ? "+" + (was - rank) : "-" + (rank - was);

        private static bool Same(Dictionary<string, int>? a, Dictionary<string, int> b)
            => a != null && a.Count == b.Count && a.All(kv => b.TryGetValue(kv.Key, out var v) && v == kv.Value);

        private static string IsoWeek(DateTime d)
        {
            var cal = CultureInfo.InvariantCulture.Calendar;
            var day = cal.GetDayOfWeek(d);
            if (day >= DayOfWeek.Monday && day <= DayOfWeek.Wednesday) d = d.AddDays(3);
            int w = cal.GetWeekOfYear(d, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
            return d.Year.ToString(CultureInfo.InvariantCulture) + "-W" + w.ToString("00", CultureInfo.InvariantCulture);
        }
    }
}
