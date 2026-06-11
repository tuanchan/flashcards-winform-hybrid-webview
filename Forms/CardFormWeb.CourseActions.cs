#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using TocflQuiz.Models;
using TocflQuiz.Services;

namespace TocflQuiz.Forms
{
    public sealed partial class CardFormWeb
    {
        private void LoadAllSets()
        {
            _allSets.Clear();

            try
            {
                var sets = CardSetStorage.LoadAllSetsSafe();
                _allSets.AddRange(sets);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Load sets error: {ex.Message}");
            }
        }

        private void ReloadAndSendCourses()
        {
            LoadAllSets();
            SendCoursesToWeb();
        }

        private void SendCoursesToWeb()
        {
            if (!_isWebReady) return;

            var courses = _allSets.Select(ToCourseDto).ToList();

            var json = JsonSerializer.Serialize(courses);
            ExecuteScript($"if(window.updateCourses) window.updateCourses({json});");
        }

        private void SendDashboardStatsToWeb()
        {
            if (!_isWebReady) return;

            try
            {
                var today = DateTime.Today;
                var allCards = _allSets.SelectMany(s => s.Items ?? new List<CardItem>()).ToList();

                var totalCards = allCards.Count;

                var totalLanguages = _allSets
                    .Select(s => NormalizeCourseLanguageCode(s.LanguageCode, s.Language))
                    .Where(c => !string.IsNullOrEmpty(c))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count();

                var totalDue = _allSets.Sum(s => SpacedRepetitionService.CountDue(s, today));

                var totalHard = allCards.Count(item => item.SrsLapseCount > 5);
                var memorizedCount = allCards.Count(item => item.SrsLevel >= 5);
                var unlearnedCount = Math.Max(0, totalCards - memorizedCount);

                // SRS level distribution (0 to 8)
                var srsDist = new int[9];
                foreach (var item in allCards)
                {
                    var lvl = Math.Clamp(item.SrsLevel, 0, 8);
                    srsDist[lvl]++;
                }

                // Language distribution
                var langDist = _allSets
                    .GroupBy(s => NormalizeCourseLanguageCode(s.LanguageCode, s.Language))
                    .Select(g => new {
                        lang = LanguageLabelFromCode(g.Key, g.FirstOrDefault()?.Language),
                        count = g.Sum(s => s.Items?.Count ?? 0)
                    })
                    .Where(x => !string.IsNullOrEmpty(x.lang))
                    .OrderByDescending(x => x.count)
                    .ToList();

                // Due timeline (Next 7 days: Day 0 to Day 6)
                var timeline = new int[7];
                foreach (var item in allCards)
                {
                    var due = ParseDate(item.SrsDueDate);
                    if (due == null)
                    {
                        timeline[0]++; // due is null means brand new, so due today
                    }
                    else
                    {
                        var diff = (due.Value.Date - today).Days;
                        if (diff <= 0)
                        {
                            timeline[0]++;
                        }
                        else if (diff < 7)
                        {
                            timeline[diff]++;
                        }
                    }
                }

                // Top hard courses
                var topHardCourses = _allSets
                    .Select(s => new {
                        title = s.Title ?? "Untitled",
                        hardCount = (s.Items ?? new List<CardItem>()).Count(item => item.SrsLapseCount > 5),
                        totalCount = s.Items?.Count ?? 0
                    })
                    .Where(x => x.hardCount > 0)
                    .OrderByDescending(x => x.hardCount)
                    .Take(5)
                    .ToList();

                // Card Status Rates
                var studyingCount = allCards.Count(item => item.SrsLevel > 0 && item.SrsLevel < 5);

                var stats = new
                {
                    totalCards,
                    totalLanguages,
                    totalDue,
                    totalHard,
                    memorizedCount,
                    unlearnedCount,
                    srsDistribution = srsDist,
                    languageDistribution = langDist,
                    dueTimeline = timeline,
                    topHardCourses,
                    rates = new {
                        memorized = totalCards > 0 ? (double)memorizedCount / totalCards * 100 : 0,
                        studying = totalCards > 0 ? (double)studyingCount / totalCards * 100 : 0,
                        hard = totalCards > 0 ? (double)totalHard / totalCards * 100 : 0
                    }
                };

                var json = JsonSerializer.Serialize(stats);
                ExecuteScript($"if(window.applyDashboardStats) window.applyDashboardStats({json});");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Dashboard stats error: {ex.Message}");
            }
        }

        private static DateTime? ParseDate(string? value)
        {
            if (DateTime.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeLocal, out var parsed))
                return parsed.Date;
            return null;
        }

        private static string NormalizeCourseLanguageCode(string? languageCode, string? language)
        {
            var raw = !string.IsNullOrEmpty(languageCode) ? languageCode : (!string.IsNullOrEmpty(language) ? language : "");
            var value = raw.Trim();
            if (string.IsNullOrEmpty(value)) return "";

            var lower = value.ToLower();
            if (lower == "zh" || lower == "zh-tw" || lower == "zh-hant" || lower == "zh-hk" || lower == "zh-mo") return "zh-TW";
            if (lower == "zh-cn" || lower == "zh-hans" || lower == "zh-sg") return "zh-CN";

            return lower.Length <= 3 ? lower : value;
        }

        private static string LanguageLabelFromCode(string key, string? originalLanguage)
        {
            if (!string.IsNullOrEmpty(originalLanguage)) return originalLanguage.Trim();

            switch (key)
            {
                case "zh-TW": return "Tiếng Trung phồn thể";
                case "zh-CN": return "Tiếng Trung giản thể";
                case "en": return "Tiếng Anh";
                case "vi": return "Tiếng Việt";
                case "ja": return "Tiếng Nhật";
                case "ko": return "Tiếng Hàn";
                case "de": return "Tiếng Đức";
                case "fr": return "Tiếng Pháp";
                case "es": return "Tiếng Tây Ban Nha";
                case "ru": return "Tiếng Nga";
                default: return key;
            }
        }

        private void RefreshSelectedCourseCoverIfNeeded()
        {
            if (!_isWebReady || _selectedSet == null)
                return;

            if (!string.IsNullOrWhiteSpace(CourseCoverImageService.ToWebUri(_selectedSet.CoverImagePath)))
                return;

            try
            {
                var latest = CardSetStorage.LoadAllSetsSafe()
                    .FirstOrDefault(s =>
                        string.Equals(s.Id, _selectedSet.Id, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(s.FolderName, _selectedSet.FolderName, StringComparison.OrdinalIgnoreCase));

                if (latest == null)
                    return;

                var latestCover = ResolveCourseCoverUri(latest);
                if (string.IsNullOrWhiteSpace(latestCover))
                    return;

                var index = _allSets.FindIndex(s =>
                    string.Equals(s.Id, latest.Id, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(s.FolderName, latest.FolderName, StringComparison.OrdinalIgnoreCase));
                if (index >= 0)
                    _allSets[index] = latest;

                _selectedSet = latest;
                _toastScheduler?.NotifySelectedSetChanged();

                var payload = JsonSerializer.Serialize(ToCourseDto(latest));
                ExecuteScript($"if(window.courseUpdateDone) window.courseUpdateDone({payload});");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Refresh selected course cover failed: {ex.Message}");
            }
        }

        private void HandleDeleteCourse(string data)
        {
            try
            {
                var doc = JsonDocument.Parse(data);
                if (!doc.RootElement.TryGetProperty("id", out var idProp))
                    return;

                string courseId = idProp.GetString() ?? "";
                if (string.IsNullOrWhiteSpace(courseId))
                {
                    SendAlert("Id học phần không hợp lệ.");
                    return;
                }

                bool ok = CardSetStorage.DeleteSetById(courseId);
                if (!ok)
                {
                    SendAlert("Xóa thất bại. Có thể học phần không tồn tại hoặc đang bị khóa.");
                    return;
                }

                if (_selectedSet != null && (_selectedSet.Id ?? "") == courseId)
                {
                    _selectedSet = null;
                    _toastScheduler?.NotifySelectedSetChanged();
                }

                ReloadAndSendCourses();
                BackToWebHome();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Delete course error: {ex.Message}");
                SendAlert("Có lỗi khi xóa học phần.");
            }
        }

        private void HandleSelectCourse(string data)
        {
            try
            {
                var doc = JsonDocument.Parse(data);
                if (!doc.RootElement.TryGetProperty("id", out var idProp))
                    return;

                string courseId = idProp.GetString() ?? "";
                _selectedSet = _allSets.FirstOrDefault(s => (s.Id ?? "") == courseId);
                _toastScheduler?.NotifySelectedSetChanged();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Select course error: {ex.Message}");
            }
        }

        private async Task HandleUpdateCourseAsync(string data)
        {
            try
            {
                using var doc = JsonDocument.Parse(data);
                var root = doc.RootElement;

                var courseId = GetJsonString(root, "id");
                if (string.IsNullOrWhiteSpace(courseId))
                {
                    SendAlert("Id học phần không hợp lệ.");
                    return;
                }

                var oldSet = _allSets.FirstOrDefault(s =>
                    string.Equals(s.Id, courseId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(s.FolderName, courseId, StringComparison.OrdinalIgnoreCase));
                var wasSelected = _selectedSet != null &&
                    (string.Equals(_selectedSet.Id, courseId, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(_selectedSet.FolderName, courseId, StringComparison.OrdinalIgnoreCase));

                var oldLanguageCode = NormalizeCourseLanguageCode(oldSet?.LanguageCode);
                var title = GetJsonString(root, "title");
                var languageCode = NormalizeCourseLanguageCode(GetJsonString(root, "languageCode"));
                var language = GetJsonString(root, "language");
                var coverImageSource = GetJsonString(root, "coverImageSource");
                var topicId = root.TryGetProperty("topicId", out var topicProp) ? topicProp.GetString() : oldSet?.TopicId;

                if (string.IsNullOrWhiteSpace(title))
                    title = oldSet?.Title ?? "";

                if (string.IsNullOrWhiteSpace(languageCode))
                    languageCode = oldLanguageCode;

                if (string.IsNullOrWhiteSpace(language))
                    language = LanguageLabelFromCode(languageCode);

                var languageChanged = !string.Equals(oldLanguageCode, languageCode, StringComparison.OrdinalIgnoreCase);
                var coverImageFailed = false;

                var ok = CardSetStorage.UpdateSetMetadata(courseId, title, language, languageCode, topicId, out var updated);
                if (!ok || updated == null)
                {
                    SendAlert("Không lưu được thông tin học phần.");
                    return;
                }

                if (languageChanged)
                {
                    ClearLanguageRelatedCaches(updated);
                    updated.Items = CardSetStorage.LoadVocabularyItems(updated);
                }

                if (!string.IsNullOrWhiteSpace(coverImageSource))
                {
                    var coverPath = await CourseCoverImageService.SaveCoverAsync(updated, coverImageSource);

                    if (!string.IsNullOrWhiteSpace(coverPath))
                    {
                        updated.CoverImagePath = coverPath;
                        CardSetStorage.SaveSetJson(updated);
                    }
                    else
                    {
                        coverImageFailed = true;
                    }
                }

                ReloadAndSendCourses();

                if (wasSelected)
                {
                    _selectedSet = _allSets.FirstOrDefault(s =>
                        string.Equals(s.Id, updated.Id, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(s.FolderName, updated.FolderName, StringComparison.OrdinalIgnoreCase));

                    _toastScheduler?.NotifySelectedSetChanged();
                }

                var updatedItems = LoadWritingVocabularyItems(updated);
                var updatedTotal = updated.VocabCount > 0 ? updated.VocabCount : updatedItems.Count;
                var updatedMemorized = updatedItems.Count(item => item.SrsLevel >= 5);

                var payload = JsonSerializer.Serialize(new
                {
                    id = updated.Id ?? "",
                    title = updated.Title ?? "",
                    count = updatedTotal,
                    unlearnedCount = Math.Max(0, updatedTotal - updatedMemorized),
                    dueCount = SpacedRepetitionService.CountDue(updated),
                    language = updated.Language ?? "",
                    languageCode = updated.LanguageCode ?? "",
                    coverImagePath = updated.CoverImagePath ?? "",
                    coverImageUrl = ResolveCourseCoverUri(updated),
                    languageChanged,
                    coverImageFailed
                });

                ExecuteScript($"if(window.courseUpdateDone) window.courseUpdateDone({payload});");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Update course error: {ex.Message}");
                SendAlert("Có lỗi khi sửa học phần.");
            }
        }

        private void HandleSearchCourses(string data)
        {
            try
            {
                var doc = JsonDocument.Parse(data);
                if (!doc.RootElement.TryGetProperty("query", out var queryProp))
                    return;

                string query = queryProp.GetString()?.ToLower() ?? "";

                var filtered = string.IsNullOrWhiteSpace(query)
                    ? _allSets
                    : _allSets.Where(s => (s.Title ?? "").ToLower().Contains(query)).ToList();

                var courses = filtered.Select(ToCourseDto).ToList();

                var json = JsonSerializer.Serialize(courses);
                ExecuteScript($"if(window.updateCourses) window.updateCourses({json});");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Search error: {ex.Message}");
            }
        }

        private static string GetJsonString(JsonElement root, string name)
            => root.TryGetProperty(name, out var prop) ? (prop.GetString() ?? "").Trim() : "";

        private static string NormalizeCourseLanguageCode(string? languageCode)
        {
            var code = (languageCode ?? "").Trim();
            var lower = code.ToLowerInvariant();

            return lower switch
            {
                "zh" or "zh-tw" or "zh-hant" or "zh-hk" or "zh-mo" => "zh-TW",
                "zh-cn" or "zh-hans" or "zh-sg" => "zh-CN",
                "en" => "en",
                "vi" => "vi",
                "ja" => "ja",
                "ko" => "ko",
                "de" => "de",
                "fr" => "fr",
                "es" => "es",
                "ru" => "ru",
                _ => code
            };
        }

        private static string LanguageLabelFromCode(string? languageCode)
        {
            return NormalizeCourseLanguageCode(languageCode) switch
            {
                "zh-TW" => "Tiếng Trung phồn thể (TOCFL / Taiwan)",
                "zh-CN" => "Tiếng Trung giản thể (Mainland)",
                "en" => "Tiếng Anh (English)",
                "vi" => "Tiếng Việt (Vietnamese)",
                "ja" => "Tiếng Nhật (Japanese)",
                "ko" => "Tiếng Hàn (Korean)",
                "de" => "Tiếng Đức (German)",
                "fr" => "Tiếng Pháp (French)",
                "es" => "Tiếng Tây Ban Nha (Spanish)",
                "ru" => "Tiếng Nga (Russian)",
                var other when !string.IsNullOrWhiteSpace(other) => other,
                _ => "Ngôn ngữ học phần"
            };
        }

        private static void ClearLanguageRelatedCaches(CardSet set)
        {
            CourseAudioService.ClearAudioCache(set);

            var baseFolder = ResolveSetBaseFolder(set);
            if (string.IsNullOrWhiteSpace(baseFolder))
                return;

            DeleteFileQuietly(Path.Combine(baseFolder, "GeminiExamplesCache.txt"));
            DeleteFileQuietly(Path.Combine(baseFolder, "GeminiExamples.js"));
            DeleteFileQuietly(Path.Combine(baseFolder, "GeminiSentenceCache.txt"));

            var imageDir = Path.Combine(baseFolder, CardSetStorage.VocabsFolderNameValue, "images");
            if (Directory.Exists(imageDir))
            {
                try { Directory.Delete(imageDir, recursive: true); } catch { }
            }
        }

        private static string ResolveSetBaseFolder(CardSet set)
        {
            if (!string.IsNullOrWhiteSpace(set.BaseFolder))
                return set.BaseFolder;

            if (!string.IsNullOrWhiteSpace(set.ConfigFilePath))
                return Path.GetDirectoryName(set.ConfigFilePath) ?? "";

            if (!string.IsNullOrWhiteSpace(set.VocabsFilePath))
            {
                var vocabsDir = Path.GetDirectoryName(set.VocabsFilePath);
                return Directory.GetParent(vocabsDir ?? "")?.FullName ?? "";
            }

            return "";
        }

        private static void DeleteFileQuietly(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
            }
        }

        private void HandlePickCourseCoverImage()
        {
            using var dialog = new System.Windows.Forms.OpenFileDialog
            {
                Title = "Chọn ảnh nền học phần",
                Filter = "Image files|*.jpg;*.jpeg;*.png;*.webp;*.bmp|All files|*.*",
                CheckFileExists = true,
                Multiselect = false
            };

            if (dialog.ShowDialog(this) != System.Windows.Forms.DialogResult.OK)
                return;

            var path = WebViewAssetService.EscapeJavaScriptString(dialog.FileName);
            ExecuteScript($"if(window.handleCourseCoverPicked) window.handleCourseCoverPicked('{path}');");
        }

        private void SendTopicsToWeb()
        {
            if (!_isWebReady) return;

            try
            {
                var topics = TopicStorage.LoadAllTopics();
                var sets = _allSets;

                var topicIdsSet = new HashSet<string>(topics.Select(t => t.Id), StringComparer.OrdinalIgnoreCase);

                var topicDtos = topics.Select(t => {
                    int count = sets.Count(s => string.Equals(s.TopicId, t.Id, StringComparison.OrdinalIgnoreCase));

                    return new
                    {
                        id = t.Id,
                        title = t.Title,
                        coverImagePath = t.CoverImagePath ?? "",
                        coverImageUrl = ResolveTopicCoverUri(t),
                        count = count
                    };
                }).ToList();

                var json = JsonSerializer.Serialize(topicDtos);
                ExecuteScript($"if(window.updateTopics) window.updateTopics({json});");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Send topics error: {ex.Message}");
            }
        }

        private static string ResolveTopicCoverUri(Topic t)
        {
            var cover = CourseCoverImageService.ToWebUri(t.CoverImagePath);
            return string.IsNullOrWhiteSpace(cover)
                ? "https://app/Webviews/icon/bg-card.png"
                : cover;
        }

        private void HandleCreateTopic(string data)
        {
            try
            {
                using var doc = JsonDocument.Parse(data);
                var root = doc.RootElement;
                var title = GetJsonString(root, "title");
                var coverImagePath = GetJsonString(root, "coverImagePath");

                if (string.IsNullOrWhiteSpace(title))
                {
                    SendAlert("Tên chủ đề không hợp lệ.");
                    return;
                }

                var topic = new Topic
                {
                    Title = title,
                    CoverImagePath = string.IsNullOrWhiteSpace(coverImagePath) ? null : coverImagePath
                };

                TopicStorage.SaveTopic(topic);
                SendTopicsToWeb();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Create topic error: {ex.Message}");
                SendAlert("Có lỗi khi tạo chủ đề.");
            }
        }

        private void HandleUpdateTopic(string data)
        {
            try
            {
                using var doc = JsonDocument.Parse(data);
                var root = doc.RootElement;
                var id = GetJsonString(root, "id");
                var title = GetJsonString(root, "title");
                var coverImagePath = GetJsonString(root, "coverImagePath");

                if (string.IsNullOrWhiteSpace(id) || string.Equals(id, "default_topic", StringComparison.OrdinalIgnoreCase))
                {
                    SendAlert("Không thể chỉnh sửa chủ đề này.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(title))
                {
                    SendAlert("Tên chủ đề không hợp lệ.");
                    return;
                }

                var topics = TopicStorage.LoadAllTopics();
                var topic = topics.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));
                if (topic == null)
                {
                    SendAlert("Chủ đề không tồn tại.");
                    return;
                }

                topic.Title = title;
                topic.CoverImagePath = string.IsNullOrWhiteSpace(coverImagePath) ? null : coverImagePath;

                TopicStorage.SaveTopic(topic);
                SendTopicsToWeb();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Update topic error: {ex.Message}");
                SendAlert("Có lỗi khi chỉnh sửa chủ đề.");
            }
        }

        private void HandleDeleteTopic(string data)
        {
            try
            {
                using var doc = JsonDocument.Parse(data);
                var root = doc.RootElement;
                var id = GetJsonString(root, "id");

                if (string.IsNullOrWhiteSpace(id) || string.Equals(id, "default_topic", StringComparison.OrdinalIgnoreCase))
                {
                    SendAlert("Không thể xóa chủ đề này.");
                    return;
                }

                bool ok = TopicStorage.DeleteTopic(id);
                if (!ok)
                {
                    SendAlert("Xóa chủ đề thất bại.");
                    return;
                }

                SendTopicsToWeb();
                ReloadAndSendCourses(); // because some study sets' TopicId might have been reset
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Delete topic error: {ex.Message}");
                SendAlert("Có lỗi khi xóa chủ đề.");
            }
        }

        private void HandlePickTopicCoverImage()
        {
            using var dialog = new System.Windows.Forms.OpenFileDialog
            {
                Title = "Chọn ảnh nền chủ đề",
                Filter = "Image files|*.jpg;*.jpeg;*.png;*.webp;*.bmp|All files|*.*",
                CheckFileExists = true,
                Multiselect = false
            };

            if (dialog.ShowDialog(this) != System.Windows.Forms.DialogResult.OK)
                return;

            var path = WebViewAssetService.EscapeJavaScriptString(dialog.FileName);
            ExecuteScript($"if(window.handleTopicCoverPicked) window.handleTopicCoverPicked('{path}');");
        }
    }
}
