using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace HomeScreenCompanion
{
    /// <summary>
    /// Top-lists for TV shows ("Shows" content type).
    ///
    /// Movie top-lists use ranked .strm copies in their own library, but a series cannot be
    /// copied that way. Instead every ranked series gets a tag of its own, and the tag's item
    /// carries the ranked art (portrait Primary + landscape Thumb), the rank as sort name and
    /// the show's synopsis/tagline/rating. Clicking it in the row opens the tag page: Play,
    /// the synopsis and the real series.
    ///
    /// The row lists the user's favourite tags, so the tags are marked favourite for every
    /// target user. That limits each user to one show top-list (a second row would show both
    /// lists' tags). Tag items never appear in the sidebar or on the Favourites tab.
    ///
    /// Tag names: Emby matches tag names ignoring punctuation and invisible characters, so a
    /// tag is shared by every list that uses the same name. The first show top-list uses the
    /// plain series name; later lists add their own title ("Show · List"). A plain name that is
    /// already a real tag on other items is never taken over.
    /// </summary>
    internal sealed class ShowTopList
    {
        public const int MaxRanks = 10;

        private readonly ILibraryManager _libraryManager;
        private readonly IUserManager _userManager;
        private readonly IUserDataManager _userDataManager;
        private readonly IHttpClient _httpClient;
        private readonly IJsonSerializer _jsonSerializer;
        private readonly ILogger _logger;
        private readonly IProviderManager _providerManager;
        private readonly IFileSystem _fileSystem;

        public ShowTopList(ILibraryManager libraryManager, IUserManager userManager, IUserDataManager userDataManager,
            IHttpClient httpClient, IJsonSerializer jsonSerializer, ILogger logger,
            IProviderManager providerManager, IFileSystem fileSystem)
        {
            _providerManager = providerManager;
            _fileSystem = fileSystem;
            _libraryManager = libraryManager;
            _userManager = userManager;
            _userDataManager = userDataManager;
            _httpClient = httpClient;
            _jsonSerializer = jsonSerializer;
            _logger = logger;
        }

        // Rows only (EnsureRows / RememberRowPositions): no tags, art or favourites are touched.
        public ShowTopList(IUserManager userManager, IJsonSerializer jsonSerializer, ILogger logger)
        {
            _userManager = userManager;
            _jsonSerializer = jsonSerializer;
            _logger = logger;
            _libraryManager = null!;
            _userDataManager = null!;
            _httpClient = null!;
            _providerManager = null!;
            _fileSystem = null!;
        }

        /// <summary>
        /// Puts the list's row back for every target user who lost it (Copy &amp; Sync replaces a
        /// user's rows with the master's), at its remembered place when known. Tags, art and
        /// favourites are left as they are. Only for a built list.
        /// </summary>
        public void EnsureRows(TopListHomeSection tl)
        {
            if ((tl.ShowEntries?.Count ?? 0) == 0) return;
            SyncSections(tl, ReadSettings(tl));
        }

        /// <summary>Remembers where this list's row is for <paramref name="userId"/> (before it is removed).</summary>
        public void RememberRowPosition(TopListHomeSection tl, string userId, long userInternalId, string sectionId)
            => HomeScreenCompanionTask.RememberRowPosition(_userManager, _jsonSerializer, RowPositionKey(tl), userId, userInternalId, sectionId, CancellationToken.None);

        /// <summary>
        /// The series carrying <paramref name="tag"/>, in the order the tag's source listed them
        /// (tag_ranks/&lt;tag&gt;.json, written by the tag sync), then by name. Top <see cref="MaxRanks"/>.
        /// </summary>
        public static List<BaseItem> RankedSeriesFromTag(ILibraryManager libraryManager, IJsonSerializer jsonSerializer, string tag)
        {
            var series = libraryManager.GetItemList(new InternalItemsQuery
            {
                Tags = new[] { tag },
                IncludeItemTypes = new[] { "Series" },
                Recursive = true,
                IsVirtualItem = false
            }).ToList();

            var rank = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var invalid = Path.GetInvalidFileNameChars();
                var safe = new string((tag ?? "unknown").Select(c => Array.IndexOf(invalid, c) >= 0 ? '_' : c).ToArray()).Trim('.');
                var rankFile = Path.Combine(Plugin.Instance!.DataFolderPath, "tag_ranks", (string.IsNullOrWhiteSpace(safe) ? "unknown" : safe) + ".json");
                if (File.Exists(rankFile))
                {
                    var ids = jsonSerializer.DeserializeFromFile<List<string>>(rankFile) ?? new List<string>();
                    for (int i = 0; i < ids.Count; i++)
                        if (!string.IsNullOrEmpty(ids[i]) && !rank.ContainsKey(ids[i])) rank[ids[i]] = i;
                }
            }
            catch { }

            return series
                .OrderBy(s => rank.TryGetValue(s.GetProviderId("Imdb") ?? "", out var r) ? r
                            : rank.TryGetValue(s.Id.ToString("N"), out var r2) ? r2 : int.MaxValue)
                .ThenBy(s => s.SortName, StringComparer.OrdinalIgnoreCase)
                .GroupBy(s => s.Id).Select(g => g.First())
                .Take(MaxRanks)
                .ToList();
        }

        public static bool IsShowList(TopListHomeSection tl)
            => string.Equals(tl?.ContentType, "Shows", StringComparison.OrdinalIgnoreCase);

        // Settings > Features: "Top lists for TV shows (experimental)". Configs from before the
        // toggle (null) count as on when they already hold a show top-list, so no list is lost.
        public static bool Enabled(PluginConfiguration? config)
            => config != null && (config.ShowTopListsEnabled
                ?? (config.TopLists ?? new List<TopListHomeSection>()).Any(IsShowList));

        // One-time migration: fixes an undecided (null) toggle to its effective value.
        // Returns true when the configuration changed and needs saving.
        public static bool ResolveToggle(PluginConfiguration? config)
        {
            if (config == null || config.ShowTopListsEnabled.HasValue) return false;
            config.ShowTopListsEnabled = Enabled(config);
            return true;
        }

        public static string FolderFor(string listName)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var safe = new string((listName ?? "unknown").Select(c => Array.IndexOf(invalid, c) >= 0 ? '_' : c).ToArray()).Trim('.');
            return Path.Combine(Plugin.Instance!.DataFolderPath, "showtoplists", string.IsNullOrWhiteSpace(safe) ? "unknown" : safe);
        }

        // Users that are already the target of another show top-list (one show row per user).
        public static List<string> UsersInOtherShowLists(PluginConfiguration config, string listName, IEnumerable<string> userIds)
        {
            var taken = new HashSet<string>(
                (config.TopLists ?? new List<TopListHomeSection>())
                    .Where(t => IsShowList(t) && !string.Equals(t.TagName, listName, StringComparison.OrdinalIgnoreCase))
                    .SelectMany(t => t.HomeSectionUserIds ?? new List<string>()),
                StringComparer.OrdinalIgnoreCase);
            return userIds.Where(taken.Contains).ToList();
        }

        /// <summary>
        /// Makes the show list match <paramref name="rankedSeries"/> (in rank order): tags, art,
        /// favourites and one home row per target user. Entries and users that are no longer in
        /// the list are cleaned up. Returns a short log line per series.
        /// </summary>
        public List<string> Apply(PluginConfiguration config, TopListHomeSection tl, IList<BaseItem> rankedSeries)
        {
            var log = new List<string>();
            var badgeStyle = BadgeStyleOf(tl);
            var folder = FolderFor(tl.TagName);
            var tempDir = Path.Combine(folder, "tmp");
            Directory.CreateDirectory(tempDir);

            var settings = ReadSettings(tl);
            var listTitle = settings.TryGetValue("CustomName", out var cn) && !string.IsNullOrWhiteSpace(cn) ? cn : tl.TagName;
            bool isMainList = (config.TopLists ?? new List<TopListHomeSection>()).FirstOrDefault(IsShowList) == tl;
            var users = ResolveUsers(tl.HomeSectionUserIds);

            var previous = tl.ShowEntries ?? new List<ShowTopListEntry>();
            var entries = new List<ShowTopListEntry>();
            int rank = 0;

            var ranked = rankedSeries.Take(tl.MaxItems > 0 ? Math.Min(tl.MaxItems, MaxRanks) : MaxRanks).ToList();
            var rankExtras = TopListHistory.Update(tl.TagName, ranked, badgeStyle, _jsonSerializer, _libraryManager, _userManager, _userDataManager, m => _logger.Warn(m));
            foreach (var series in ranked)
            {
                rank++;
                try
                {
                    var tagName = ChooseTagName(series, isMainList, listTitle, previous);
                    if (!series.ContainsTag(tagName))
                    {
                        series.AddTag(tagName);
                        _libraryManager.UpdateItem(series, series.GetParent(), ItemUpdateType.MetadataEdit);
                    }

                    var tagItem = FindTagItem(tagName);
                    if (tagItem == null)
                    {
                        log.Add($"✖ #{rank} '{series.Name}': tag '{tagName}' was not created");
                        continue;
                    }

                    ApplyArtAndDetails(tagItem, series, rank, folder, tempDir, badgeStyle, rankExtras.TryGetValue(series.Id, out var rx) ? rx : null);
                    SetFavourite(users, tagItem, true);
                    entries.Add(new ShowTopListEntry { SeriesId = series.Id.ToString("N"), TagName = tagName });
                    log.Add($"✔ #{rank} {series.Name}");
                }
                catch (Exception ex)
                {
                    log.Add($"✖ #{rank} '{series.Name}': {ex.Message}");
                    _logger.ErrorException($"Show top-list '{tl.TagName}': failed for '{series.Name}'", ex);
                }
            }

            // Entries that dropped out of the list: take the tag off the series and unfavourite it.
            foreach (var old in previous.Where(p => !entries.Any(e => SameEntry(e, p))))
                RemoveEntry(old, ResolveUsers(AllKnownUserIds(tl)));

            // Users no longer targeted lose the favourites too, or a later show list of theirs
            // would show this list's tags in its row.
            var targets = new HashSet<string>(tl.HomeSectionUserIds ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            var droppedUsers = ResolveUsers(AllKnownUserIds(tl).Where(id => !targets.Contains(id)));
            if (droppedUsers.Count > 0)
                foreach (var entry in entries)
                {
                    var tagItem = FindTagItem(entry.TagName);
                    if (tagItem != null) SetFavourite(droppedUsers, tagItem, false);
                }

            tl.ShowEntries = entries;
            tl.ShowParkedSeriesIds = new List<string>();
            SyncSections(tl, settings);

            try { Directory.Delete(tempDir, true); } catch { }
            return log;
        }

        /// <summary>
        /// "Top lists for TV shows" turned off: the same clean-up as deleting the list, but the
        /// list stays in the configuration and remembers its ranked series so turning the
        /// feature on again rebuilds it (<see cref="ParkedSeries"/>).
        /// </summary>
        public void Suspend(TopListHomeSection tl)
        {
            var ids = (tl.ShowEntries ?? new List<ShowTopListEntry>()).Select(e => e.SeriesId).Where(id => !string.IsNullOrEmpty(id)).ToList();
            if (ids.Count > 0) tl.ShowParkedSeriesIds = ids;
            // Remember where each user's row was (as for a row switched off by its schedule), so
            // the rebuilt row goes back to that place instead of the bottom.
            foreach (var tracking in tl.HomeSectionTracked ?? new List<HomeSectionTracking>())
            {
                if (string.IsNullOrEmpty(tracking.UserId)) continue;
                try
                {
                    HomeScreenCompanionTask.RememberRowPosition(_userManager, _jsonSerializer, RowPositionKey(tl), tracking.UserId,
                        _userManager.GetInternalId(tracking.UserId), tracking.SectionId, CancellationToken.None);
                }
                catch { }
            }
            Remove(tl);
        }

        private static string RowPositionKey(TopListHomeSection tl) => "showtoplist:" + (tl.TagName ?? "");

        /// <summary>The series a suspended list held, in rank order (still in the library).</summary>
        public List<BaseItem> ParkedSeries(TopListHomeSection tl)
            => (tl.ShowParkedSeriesIds ?? new List<string>())
                .Select(id => Guid.TryParse(id, out var g) ? _libraryManager.GetItemById(g) : null)
                .Where(i => i is MediaBrowser.Controller.Entities.TV.Series)
                .Select(i => i!)
                .ToList();

        /// <summary>Removes everything a show list created: tags on the series, favourites and rows.</summary>
        public void Remove(TopListHomeSection tl)
        {
            var users = ResolveUsers(AllKnownUserIds(tl));
            foreach (var entry in tl.ShowEntries ?? new List<ShowTopListEntry>())
                RemoveEntry(entry, users);
            tl.ShowEntries = new List<ShowTopListEntry>();

            foreach (var tracking in tl.HomeSectionTracked ?? new List<HomeSectionTracking>())
                DeleteSection(tracking);
            tl.HomeSectionTracked = new List<HomeSectionTracking>();

            try { Directory.Delete(FolderFor(tl.TagName), true); } catch { }
        }

        // ── tags ────────────────────────────────────────────────────────────────────────────

        private string ChooseTagName(BaseItem series, bool isMainList, string listTitle, List<ShowTopListEntry> previous)
        {
            var seriesId = series.Id.ToString("N");
            var plain = series.Name ?? "";
            var labelled = $"{plain} · {listTitle}";
            if (!isMainList) return labelled;

            // Keep a name this list already owns for the series.
            if (previous.Any(p => p.SeriesId == seriesId && string.Equals(p.TagName, plain, StringComparison.OrdinalIgnoreCase)))
                return plain;

            // Never take over a real tag that other items already use.
            var users = _libraryManager.GetItemList(new InternalItemsQuery { Tags = new[] { plain }, Recursive = true });
            return users.Any(i => i.Id != series.Id) ? labelled : plain;
        }

        private BaseItem? FindTagItem(string tagName)
        {
            // The tag item is created when the series is saved; give the database a moment.
            for (int attempt = 0; attempt < 5; attempt++)
            {
                var found = _libraryManager.GetItemList(new InternalItemsQuery { IncludeItemTypes = new[] { "Tag" }, Name = tagName })
                    .FirstOrDefault();
                if (found != null) return found;
                Thread.Sleep(300);
            }
            return null;
        }

        // The list's image style, set like a movie top-list's (HomeSectionSettings.BadgeStyle).
        private string BadgeStyleOf(TopListHomeSection tl)
        {
            try
            {
                var settings = _jsonSerializer.DeserializeFromString<Dictionary<string, string>>(tl.HomeSectionSettings ?? "{}");
                if (settings != null) return BadgeLook.FromSettings(settings, "top10");
            }
            catch { }
            return "top10";
        }

        // Ranked art is drawn exactly as for movie top-lists (same styles, same code); the tag
        // item shows it as its Primary and Thumb image.
        private void ApplyArtAndDetails(BaseItem tagItem, BaseItem series, int rank, string folder, string tempDir, string badgeStyle, RankExtras? extras)
        {
            var outputBase = Path.Combine(folder, $"{rank:00}");
            var posterPath = outputBase + ".jpg";
            var thumbPath = outputBase + "-thumb.jpg";
            foreach (var old in new[] { posterPath, thumbPath })
                try { if (File.Exists(old)) File.Delete(old); } catch { }
            HomeScreenCompanionService.WriteRankedImages(series, rank, outputBase, badgeStyle, tempDir,
                _httpClient, _providerManager, _libraryManager, _fileSystem, m => _logger.Warn(m), extras);

            var images = (tagItem.ImageInfos ?? Array.Empty<ItemImageInfo>()).ToList();
            if (File.Exists(posterPath))
            {
                images.RemoveAll(i => i.Type == ImageType.Primary);
                images.Add(new ItemImageInfo { Path = posterPath, Type = ImageType.Primary, DateModified = File.GetLastWriteTimeUtc(posterPath) });
            }
            else
            {
                _logger.Warn($"Show top-list: no poster for '{series.Name}', tag keeps its previous image");
            }
            if (File.Exists(thumbPath))
            {
                images.RemoveAll(i => i.Type == ImageType.Thumb);
                images.Add(new ItemImageInfo { Path = thumbPath, Type = ImageType.Thumb, DateModified = File.GetLastWriteTimeUtc(thumbPath) });
            }
            tagItem.ImageInfos = images.ToArray();

            tagItem.SortName = rank.ToString("00");
            tagItem.Overview = series.Overview;
            tagItem.Tagline = series.Tagline;
            tagItem.OfficialRating = series.OfficialRating;
            tagItem.CommunityRating = series.CommunityRating;
            tagItem.ProductionYear = series.ProductionYear;
            var locked = (tagItem.LockedFields ?? Array.Empty<MetadataFields>()).ToList();
            if (!locked.Contains(MetadataFields.SortName)) locked.Add(MetadataFields.SortName);
            tagItem.LockedFields = locked.ToArray();

            _libraryManager.UpdateItem(tagItem, tagItem.GetParent(), ItemUpdateType.ImageUpdate);
        }

        private void RemoveEntry(ShowTopListEntry entry, List<User> users)
        {
            try
            {
                var tagItem = FindTagItem(entry.TagName);
                if (tagItem != null) SetFavourite(users, tagItem, false);

                if (Guid.TryParse(entry.SeriesId, out var seriesGuid))
                {
                    var series = _libraryManager.GetItemById(seriesGuid);
                    if (series != null && series.ContainsTag(entry.TagName))
                    {
                        series.SetTags(series.Tags.Where(t => !string.Equals(t, entry.TagName, StringComparison.OrdinalIgnoreCase)));
                        _libraryManager.UpdateItem(series, series.GetParent(), ItemUpdateType.MetadataEdit);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.ErrorException($"Show top-list: could not remove tag '{entry.TagName}'", ex);
            }
        }

        // ── users and favourites ───────────────────────────────────────────────────────────

        private List<User> ResolveUsers(IEnumerable<string> userIds)
            => userIds
                .Select(id => Guid.TryParse(id, out var g) ? _userManager.GetUserById(g) : null)
                .Where(u => u != null)
                .Select(u => u!)
                .ToList();

        // Target users plus anyone who still has a row from an earlier version of the list.
        private static IEnumerable<string> AllKnownUserIds(TopListHomeSection tl)
            => (tl.HomeSectionUserIds ?? new List<string>())
                .Concat((tl.HomeSectionTracked ?? new List<HomeSectionTracking>()).Select(t => t.UserId))
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct(StringComparer.OrdinalIgnoreCase);

        private void SetFavourite(List<User> users, BaseItem item, bool favourite)
        {
            foreach (var user in users)
            {
                var data = _userDataManager.GetUserData(user, item);
                if (data == null || data.IsFavorite == favourite) continue;
                data.IsFavorite = favourite;
                _userDataManager.SaveUserData(user, item, data, UserDataSaveReason.UpdateUserRating, CancellationToken.None);
            }
        }

        // ── home rows ──────────────────────────────────────────────────────────────────────

        private Dictionary<string, string> ReadSettings(TopListHomeSection tl)
        {
            var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (!string.IsNullOrEmpty(tl.HomeSectionSettings) && tl.HomeSectionSettings != "{}")
                    settings = _jsonSerializer.DeserializeFromString<Dictionary<string, string>>(tl.HomeSectionSettings) ?? settings;
            }
            catch { }
            return settings;
        }

        // The row: this user's favourite tags, in rank order.
        private void SyncSections(TopListHomeSection tl, Dictionary<string, string> settings)
        {
            settings["SectionType"] = "items";
            settings["ItemTypes"] = "[\"Tag\"]";
            settings["SortBy"] = "SortName";
            settings["SortOrder"] = "Ascending";
            settings["_queryIsFavorite"] = "true";
            settings.Remove("_queryExcludeViewIds");
            settings.Remove("ExcludedFolders");
            tl.HomeSectionSettings = _jsonSerializer.SerializeToString(settings);

            var targets = new HashSet<string>(tl.HomeSectionUserIds ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            var tracked = tl.HomeSectionTracked ?? new List<HomeSectionTracking>();

            foreach (var gone in tracked.Where(t => !targets.Contains(t.UserId)).ToList())
            {
                DeleteSection(gone);
                tracked.Remove(gone);
            }

            foreach (var userId in targets)
            {
                try
                {
                    var internalId = _userManager.GetInternalId(userId);
                    var sections = _userManager.GetHomeSections(internalId, CancellationToken.None)?.Sections ?? Array.Empty<ContentSection>();
                    var track = tracked.FirstOrDefault(t => string.Equals(t.UserId, userId, StringComparison.OrdinalIgnoreCase));
                    var owned = track == null ? null : sections.FirstOrDefault(s => s.Id == track.SectionId);
                    // Copy & Sync replaces a target's rows with copies of the master's, so the tracked
                    // row can be gone while a copy of this list's row is there: take that one over
                    // instead of adding a second row.
                    if (owned == null && settings.TryGetValue("CustomName", out var rowName) && !string.IsNullOrWhiteSpace(rowName))
                    {
                        owned = sections.FirstOrDefault(s => string.Equals(s.CustomName, rowName, StringComparison.OrdinalIgnoreCase)
                            && (s.ItemTypes ?? Array.Empty<string>()).Contains("Tag", StringComparer.OrdinalIgnoreCase));
                        if (owned != null)
                        {
                            if (track != null) track.SectionId = owned.Id;
                            else tracked.Add(new HomeSectionTracking { UserId = userId, SectionId = owned.Id });
                            // The copy sits where the master has it; that place wins.
                            HomeScreenCompanionTask.ForgetRowPosition(_jsonSerializer, RowPositionKey(tl), userId);
                        }
                    }

                    if (owned != null)
                    {
                        var updated = HomeScreenCompanionTask.BuildContentSection(_jsonSerializer, settings, null!, owned);
                        updated.Id = owned.Id;
                        _userManager.UpdateHomeSection(internalId, updated, CancellationToken.None);
                        continue;
                    }

                    var before = new HashSet<string>(sections.Select(s => s.Id ?? ""));
                    _userManager.AddHomeSection(internalId, HomeScreenCompanionTask.BuildContentSection(_jsonSerializer, settings, null!), CancellationToken.None);
                    var newId = (_userManager.GetHomeSections(internalId, CancellationToken.None)?.Sections ?? Array.Empty<ContentSection>())
                        .Select(s => s.Id).FirstOrDefault(id => !string.IsNullOrEmpty(id) && !before.Contains(id)) ?? "";

                    if (track != null) track.SectionId = newId;
                    else tracked.Add(new HomeSectionTracking { UserId = userId, SectionId = newId });
                    // Back to where it was before "Top lists for TV shows" was switched off (if known).
                    if (!string.IsNullOrEmpty(newId))
                        HomeScreenCompanionTask.RestoreRowPosition(_userManager, _jsonSerializer, RowPositionKey(tl), userId, internalId, newId, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.ErrorException($"Show top-list '{tl.TagName}': home row for user {userId} failed", ex);
                }
            }
            tl.HomeSectionTracked = tracked;
        }

        private void DeleteSection(HomeSectionTracking tracking)
        {
            if (string.IsNullOrEmpty(tracking.UserId) || string.IsNullOrEmpty(tracking.SectionId)) return;
            try
            {
                _userManager.DeleteHomeSections(_userManager.GetInternalId(tracking.UserId), new[] { tracking.SectionId }, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.ErrorException($"Show top-list: could not delete row {tracking.SectionId}", ex);
            }
        }

        private static bool SameEntry(ShowTopListEntry a, ShowTopListEntry b)
            => a.SeriesId == b.SeriesId && string.Equals(a.TagName, b.TagName, StringComparison.OrdinalIgnoreCase);
    }
}
