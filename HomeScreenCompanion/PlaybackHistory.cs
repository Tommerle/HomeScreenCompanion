using MediaBrowser.Model.Activity;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HomeScreenCompanion
{
    /// <summary>
    /// Real plays from Emby's activity log, for the setting "Ignore titles only marked as watched".
    /// Marking a title watched sets the same last-played date and play count as playing it, so the
    /// user data cannot tell them apart; only a real play leaves "playback.start" / "playback.stop"
    /// entries (user and item by internal id) in the activity log.
    ///
    /// A last-played date counts when the log has a play of that item by that user (the date of the
    /// latest play is used), or when it is older than what the log covers (the log keeps a limited
    /// time, and plays before that cannot be checked). Otherwise it was only marked: no date.
    /// </summary>
    internal sealed class PlaybackHistory
    {
        private const int PageSize = 5000;

        private readonly Dictionary<(long User, long Item), DateTimeOffset> _lastPlay = new Dictionary<(long, long), DateTimeOffset>();
        private DateTimeOffset _trustBefore;

        public int Plays => _lastPlay.Count;

        /// <summary>Reads the plays of the last <paramref name="days"/> days.</summary>
        public static PlaybackHistory Load(IActivityManager activityManager, int days)
        {
            var h = new PlaybackHistory();
            var windowStart = DateTimeOffset.UtcNow.AddDays(-Math.Max(1, Math.Min(3650, days)));
            h._trustBefore = windowStart;

            // Where the log begins (entries come newest first): plays before that cannot be checked.
            var first = activityManager.GetActivityLogEntries(null, null, 0, 1, true);
            if (first.TotalRecordCount > 0)
            {
                var oldest = activityManager.GetActivityLogEntries(null, null, first.TotalRecordCount - 1, 1, false).Items?.FirstOrDefault();
                if (oldest != null && oldest.Date > h._trustBefore) h._trustBefore = oldest.Date;
            }

            for (int start = 0; ; start += PageSize)
            {
                var page = activityManager.GetActivityLogEntries(windowStart, true, start, PageSize, false).Items ?? Array.Empty<ActivityLogEntry>();
                foreach (var e in page)
                {
                    if (e.Type == null || !e.Type.StartsWith("playback.", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!long.TryParse(e.UserId, out var user) || !long.TryParse(e.ItemId, out var item)) continue;
                    var key = (user, item);
                    if (!h._lastPlay.TryGetValue(key, out var d) || e.Date > d) h._lastPlay[key] = e.Date;
                }
                if (page.Length < PageSize) break;
            }
            return h;
        }

        /// <summary>The last-played date to use for a user and item: null when it was only marked watched.</summary>
        public DateTimeOffset? Effective(long userInternalId, long itemInternalId, DateTimeOffset? lastPlayed)
        {
            if (!lastPlayed.HasValue) return null;
            if (_lastPlay.TryGetValue((userInternalId, itemInternalId), out var played)) return played;
            return lastPlayed.Value < _trustBefore ? lastPlayed : null;
        }
    }
}
