using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HomeScreenCompanion
{
    /// <summary>
    /// Counts unique viewers per title for the "Popular on this server" rule: the number of
    /// different users who watched it (Emby marks it played) in the last N days, 0 = all time.
    /// A show counts once per viewer, however many episodes they watched. Emby keeps only each
    /// user's last play per item, so plays within a window cannot be counted, and titles marked
    /// watched without a date only count for all time. Each user's records are read once per
    /// run (one call per user) and matched to titles by user-data key.
    /// </summary>
    internal sealed class PopularityCounter
    {
        private readonly ILibraryManager _libraryManager;
        private readonly IUserManager _userManager;
        private readonly IUserDataManager _userDataManager;
        private readonly Dictionary<long, List<UserItemData>> _recordsByUser = new Dictionary<long, List<UserItemData>>();


        public PopularityCounter(ILibraryManager libraryManager, IUserManager userManager, IUserDataManager userDataManager)
        {
            _libraryManager = libraryManager;
            _userManager = userManager;
            _userDataManager = userDataManager;
        }

        // agoDays > 0 counts as if today were that many days earlier (rank movement's "a week ago").
        // lastPlayed (user, item, date → date): the date a play counts with; null drops it (see
        // PlaybackHistory: "Ignore titles only marked as watched"). Only used for a time window.
        public Dictionary<Guid, int> CountViewers(List<BaseItem> titles, int days, Action<string> log, int agoDays = 0,
            Func<long, long, DateTimeOffset?, DateTimeOffset?>? lastPlayed = null)
        {
            // user-data key → the film / show it counts for. Shows are counted through their episodes;
            // a film's versions (separate items) count as one film: IMDb id, else TMDb id, else the item.
            var target = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var itemOfKey = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            void Map(BaseItem item, string group)
            {
                var key = KeyOf(item);
                if (!string.IsNullOrEmpty(key) && !target.ContainsKey(key)) { target[key] = group; itemOfKey[key] = item.InternalId; }
            }
            var filmOf = FilmKeys(titles);

            var seriesByInternalId = new Dictionary<long, string>();
            foreach (var title in titles)
            {
                if (title is Series) seriesByInternalId[title.InternalId] = filmOf(title);
                else Map(title, filmOf(title));
            }
            if (seriesByInternalId.Count > 0)
            {
                var episodes = _libraryManager.GetItemList(new InternalItemsQuery
                {
                    IncludeItemTypes = new[] { "Episode" },
                    SeriesIds = seriesByInternalId.Keys.ToArray(),
                    Recursive = true,
                    IsVirtualItem = false
                });
                foreach (var ep in episodes.OfType<Episode>())
                    if (seriesByInternalId.TryGetValue(ep.SeriesId, out var seriesId)) Map(ep, seriesId);
            }

            var until = agoDays > 0 ? DateTimeOffset.UtcNow.AddDays(-agoDays) : (DateTimeOffset?)null;
            var cutoff = days > 0 ? (until ?? DateTimeOffset.UtcNow).AddDays(-days) : (DateTimeOffset?)null;
            var viewers = new Dictionary<string, HashSet<long>>(StringComparer.OrdinalIgnoreCase);
            int users = 0, hits = 0;

            foreach (var user in _userManager.GetUserList(new UserQuery { IsDisabled = false }))
            {
                users++;
                foreach (var record in RecordsOf(user.InternalId))
                {
                    if (record.Key == null || !target.TryGetValue(record.Key, out var group)) continue;
                    hits++;
                    var date = record.LastPlayedDate;
                    if (cutoff.HasValue && lastPlayed != null && itemOfKey.TryGetValue(record.Key, out var itemId))
                        date = lastPlayed(user.InternalId, itemId, date);
                    if (!Counts(record.Played, date, cutoff)) continue;
                    if (until.HasValue && date.HasValue && date.Value >= until.Value) continue;
                    if (!viewers.TryGetValue(group, out var set)) viewers[group] = set = new HashSet<long>();
                    set.Add(user.InternalId);
                }
            }

            log($"Popularity: {titles.Count} title(s), {target.Count} watch key(s), {users} user(s), {hits} viewing record(s) matched, {viewers.Count} title(s) watched "
                + (cutoff.HasValue ? $"in the last {days} day(s)" : "ever") + (until.HasValue ? $" (as of {agoDays} days ago)" : ""));
            var result = new Dictionary<Guid, int>();
            foreach (var title in titles)
                if (viewers.TryGetValue(filmOf(title), out var set)) result[title.Id] = set.Count;
            return result;
        }

        /// <summary>
        /// Plays per title for the top-list "N plays" badge extra: every user's play count added
        /// up (a show: its episodes' plays). Emby keeps one record per user and item, so this is
        /// all-time.
        /// </summary>
        public Dictionary<Guid, int> CountPlays(List<BaseItem> titles)
        {
            var target = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            var seriesByInternalId = new Dictionary<long, Guid>();
            foreach (var title in titles)
            {
                if (title is Series) seriesByInternalId[title.InternalId] = title.Id;
                else { var k = KeyOf(title); if (k.Length > 0 && !target.ContainsKey(k)) target[k] = title.Id; }
            }
            if (seriesByInternalId.Count > 0)
                foreach (var ep in _libraryManager.GetItemList(new InternalItemsQuery { IncludeItemTypes = new[] { "Episode" }, SeriesIds = seriesByInternalId.Keys.ToArray(), Recursive = true, IsVirtualItem = false }).OfType<Episode>())
                    if (seriesByInternalId.TryGetValue(ep.SeriesId, out var sid)) { var k = KeyOf(ep); if (k.Length > 0 && !target.ContainsKey(k)) target[k] = sid; }

            var plays = new Dictionary<Guid, int>();
            foreach (var user in _userManager.GetUserList(new UserQuery { IsDisabled = false }))
                foreach (var record in RecordsOf(user.InternalId))
                {
                    if (record.Key == null || !target.TryGetValue(record.Key, out var id)) continue;
                    int n = record.Played ? Math.Max(record.PlayCount, 1) : 0;
                    if (n > 0) plays[id] = (plays.TryGetValue(id, out var v) ? v : 0) + n;
                }
            return plays;
        }

        /// <summary>
        /// The film a movie item belongs to: a film's versions are separate items with the same IMDb
        /// id (else TMDb id; a version with only a TMDb id joins the film another version links to
        /// that TMDb id). Anything that is not a movie is its own item.
        /// </summary>
        internal static Func<BaseItem, string> FilmKeys(IEnumerable<BaseItem> scope)
        {
            var imdbByTmdb = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in scope.OfType<Movie>())
            {
                string imdb = m.GetProviderId("Imdb"), tmdb = m.GetProviderId("Tmdb");
                if (!string.IsNullOrEmpty(imdb) && !string.IsNullOrEmpty(tmdb) && !imdbByTmdb.ContainsKey(tmdb)) imdbByTmdb[tmdb] = imdb;
            }
            return item =>
            {
                if (!(item is Movie)) return item.Id.ToString("N");
                string imdb = item.GetProviderId("Imdb"), tmdb = item.GetProviderId("Tmdb");
                if (string.IsNullOrEmpty(imdb) && !string.IsNullOrEmpty(tmdb)) imdbByTmdb.TryGetValue(tmdb, out imdb);
                return !string.IsNullOrEmpty(imdb) ? "imdb:" + imdb
                    : !string.IsNullOrEmpty(tmdb) ? "tmdb:" + tmdb
                    : item.Id.ToString("N");
            };
        }

        // Also used by NextWatchRecommender, so "popular" means the same in both.
        internal static bool Counts(UserItemData d, DateTimeOffset? cutoff) => Counts(d.Played, d.LastPlayedDate, cutoff);

        private static bool Counts(bool played, DateTimeOffset? lastPlayed, DateTimeOffset? cutoff)
        {
            // Played only: Emby also raises PlayCount for a play stopped straight away.
            if (!played) return false;
            if (!cutoff.HasValue) return true;
            return lastPlayed.HasValue && lastPlayed.Value >= cutoff.Value;
        }

        // One GetAllUserData call per user per run, shared with NextWatchRecommender.
        internal List<UserItemData> RecordsOf(long userInternalId)
        {
            if (!_recordsByUser.TryGetValue(userInternalId, out var records))
                _recordsByUser[userInternalId] = records = _userDataManager.GetAllUserData(userInternalId) ?? new List<UserItemData>();
            return records;
        }

        internal static string KeyOf(BaseItem item)
        {
            var key = item.UserDataKey;
            if (string.IsNullOrEmpty(key))
            {
                try { key = item.GetUserDataKey(null); } catch { }
            }
            return key ?? "";
        }
    }

    /// <summary>Result of HomeScreenCompanionTask.PreviewEntryAsync.</summary>
    internal sealed class SourcePreview
    {
        public SourcePreview(TagConfig source, List<TagConfig>? group = null)
        {
            Source = source;
            Group = group != null && group.Count > 0 ? group : new List<TagConfig> { source };
        }

        public TagConfig Source { get; }
        // Every URL / collection / playlist of the source card (one TagConfig each, as saved).
        public List<TagConfig> Group { get; }
        // Titles on the list that are not in the library, in list order (External / AI).
        public List<PreviewMissingTitle> Missing { get; } = new List<PreviewMissingTitle>();
        public List<string> Warnings { get; private set; } = new List<string>();
        public string Note { get; set; } = "";
        public bool Done { get; private set; }
        public string Message { get; set; } = "";
        public int Scanned { get; private set; }
        public List<BaseItem> Items { get; private set; } = new List<BaseItem>();
        public Dictionary<Guid, int> Viewers { get; private set; } = new Dictionary<Guid, int>();
        // "Your Next Watch": the user the preview is for, and a short reason per title.
        public string UserId { get; set; } = "";
        public Dictionary<Guid, string> Reasons { get; } = new Dictionary<Guid, string>();

        public void Complete(List<BaseItem> items, int scanned, Dictionary<Guid, int>? viewers, List<string>? warnings = null)
        {
            Items = items.ToList();
            Scanned = scanned;
            Viewers = viewers ?? new Dictionary<Guid, int>();
            Warnings = warnings?.ToList() ?? new List<string>();
            Done = true;
        }
    }

    internal sealed class PreviewMissingTitle
    {
        public PreviewMissingTitle(string title, int? year, string imdb) { Title = title ?? ""; Year = year; Imdb = imdb ?? ""; }
        public string Title { get; }
        public int? Year { get; }
        public string Imdb { get; }
    }
}
