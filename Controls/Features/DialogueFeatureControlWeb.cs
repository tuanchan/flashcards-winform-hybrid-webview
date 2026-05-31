#nullable enable

using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TocflQuiz.Forms;
using TocflQuiz.Models;
using TocflQuiz.Services;

namespace TocflQuiz.Controls.Features
{
    public sealed class DialogueFeatureControlWeb : UserControl
    {
        public event Action? ExitRequested;

        private const string DialogueViewPath = "Webviews/dialogue-feature.html";
        private const string DialogueRootFolderName = "Dialogues";
        private const string DialogueFileName = "dialogue.json";
        private const string HiddenDialoguesFileName = ".dialogue-hidden.json";

        private readonly WebView2 _web = new();
        private readonly JsonSerializerOptions _json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true
        };

        private readonly List<CardSet> _courses = new();
        private CardSet? _selectedSet;
        private bool _ready;
        private bool _isDarkMode;
        private CancellationTokenSource? _playCts;

        public DialogueFeatureControlWeb()
        {
            Dock = DockStyle.Fill;
            Controls.Add(_web);
            _web.Dock = DockStyle.Fill;

            Load += async (_, __) => await InitAsync();
            Disposed += (_, __) =>
            {
                StopPlayback();
                try
                {
                    if (_web.CoreWebView2 != null)
                        _web.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
                }
                catch { }

                try { _web.Dispose(); } catch { }
            };
        }

        public void BindCourses(IEnumerable<CardSet> courses, CardSet? selectedSet)
        {
            _courses.Clear();
            _courses.AddRange(courses ?? Enumerable.Empty<CardSet>());
            _selectedSet = selectedSet;
            PostInit();
        }

        public void SetDarkMode(bool isDark)
        {
            _isDarkMode = isDark;
            Post("theme", new { dark = isDark });
        }

        private bool _initStarted;

        private async Task InitAsync()
        {
            if (_initStarted) return;
            _initStarted = true;

            try
            {
                string userData = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "TocflQuiz",
                    "WebView2");

                Directory.CreateDirectory(userData);

                var env = await CoreWebView2Environment.CreateAsync(null, userData);
                await _web.EnsureCoreWebView2Async(env);

                try
                {
                    _web.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
                    _web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                    _web.CoreWebView2.Settings.IsZoomControlEnabled = false;
                }
                catch { }

                _web.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
                _web.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

                WebViewAssetService.ConfigureLocalContent(_web);
                _ready = true;
                WebViewAssetService.NavigateToPage(_web, DialogueViewPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"WebView2 initialization failed: {ex.Message}", "Error");
            }
        }

        private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var raw = e.TryGetWebMessageAsString();
                if (string.IsNullOrWhiteSpace(raw))
                    raw = e.WebMessageAsJson;

                using var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;
                var action = root.TryGetProperty("action", out var a) ? a.GetString() : "";
                var data = root.TryGetProperty("data", out var d) ? d : default;

                switch (action)
                {
                    case "ready":
                        PostInit();
                        break;

                    case "saveDialogue":
                        _ = SaveFromWebAsync(data);
                        break;

                    case "playDialogue":
                        _ = PlayFromWebAsync(data);
                        break;

                    case "stopPlayback":
                        StopPlayback();
                        Post("activeMessage", new { id = "", index = -1 });
                        Post("playbackProgress", new { elapsedSeconds = 0, totalSeconds = 0 });
                        Post("playbackState", new { playing = false });
                        break;

                    case "generateGeminiDialogue":
                        _ = GenerateGeminiDialogueAsync(data);
                        break;

                    case "loadDialogue":
                        LoadDialogueFromWeb(data);
                        break;

                    case "renameDialogue":
                        RenameDialogueFromWeb(data);
                        break;

                    case "deleteDialogue":
                        DeleteDialogueFromWeb(data);
                        break;
                    case "goHome":
                        ExitRequested?.Invoke();
                        break;
                }
            }
            catch (Exception ex)
            {
                Post("toast", new { type = "warn", text = ex.Message });
            }
        }

        private void PostInit()
        {
            if (!_ready) return;

            var defaultLeft = EdgeTtsRunner.ResolveVoiceByLanguageCode(_selectedSet?.LanguageCode);
            var defaultRight = PickAlternateVoice(defaultLeft);

            Post("init", new
            {
                dark = _isDarkMode,
                courses = _courses.Select(s => new
                {
                    id = s.Id ?? "",
                    title = s.Title ?? "Untitled",
                    count = s.VocabCount > 0 ? s.VocabCount : (s.Items?.Count ?? 0)
                }).ToList(),
                selectedCourseId = _selectedSet?.Id ?? "",
                voices = EdgeTtsRunner.GetSupportedVoices().Select(v => new
                {
                    voice = v.Voice,
                    label = v.Label,
                    languageKey = v.LanguageKey,
                    languageName = v.LanguageName,
                    languageCode = v.LanguageCode,
                    country = v.Country,
                    gender = v.Gender
                }).ToList(),
                selectedLanguage = VoiceToLanguageKey(defaultLeft),
                dialogues = LoadDialogueSummaries(),
                defaults = new
                {
                    leftVoice = defaultLeft,
                    rightVoice = defaultRight
                }
            });
        }

        private async Task SaveFromWebAsync(JsonElement data)
        {
            try
            {
                Post("audioBusy", new { busy = true });
                var project = ReadProject(data);
                var forceRegenerateAudio = GetBool(data, "forceRegenerateAudio");
                var result = await SaveProjectAsync(project, generateAudio: true, forceRegenerateAudio, CancellationToken.None);
                Post("saveDone", new { path = result.FolderPath, project = result.Project, dialogues = LoadDialogueSummaries() });
                Post("toast", new { type = "ok", text = "Đã lưu hội thoại và audio." });
            }
            catch (Exception ex)
            {
                Post("toast", new { type = "warn", text = ex.Message });
            }
            finally
            {
                Post("audioBusy", new { busy = false });
            }
        }

        private async Task PlayFromWebAsync(JsonElement data)
        {
            StopPlayback();
            _playCts = new CancellationTokenSource();
            var token = _playCts.Token;

            try
            {
                var project = ReadProject(data);
                var startIndex = Math.Max(0, GetInt(data, "startIndex", 0));
                Post("audioBusy", new { busy = true });
                var result = await SaveProjectAsync(project, generateAudio: true, forceRegenerateAudio: false, token);
                Post("saveDone", new { path = result.FolderPath, project = result.Project, dialogues = LoadDialogueSummaries() });
                Post("audioBusy", new { busy = false });
                Post("playbackState", new { playing = true });

                var segments = BuildPlaybackSegments(result.Project, result.FolderPath)
                    .Where(x => x.Index >= startIndex)
                    .ToList();
                var totalDuration = TimeSpan.FromMilliseconds(Math.Max(1, segments.Sum(x => x.Duration.TotalMilliseconds + x.Pause.TotalMilliseconds)));
                var elapsed = TimeSpan.Zero;

                Post("playbackProgress", new { elapsedSeconds = 0, totalSeconds = totalDuration.TotalSeconds });

                foreach (var segment in segments)
                {
                    token.ThrowIfCancellationRequested();

                    Post("activeMessage", new { id = segment.Message.Id, index = segment.Index });
                    do
                    {
                        await PlayMp3FileAsync(segment.AudioPath, elapsed, totalDuration, token);
                    }
                    while (segment.Message.Loop && !token.IsCancellationRequested);

                    elapsed += segment.Duration;
                    PostPlaybackProgress(elapsed, totalDuration);

                    if (segment.Pause > TimeSpan.Zero)
                    {
                        await DelayWithProgressAsync(segment.Pause, elapsed, totalDuration, token);
                        elapsed += segment.Pause;
                    }
                }

                Post("activeMessage", new { id = "", index = -1 });
                PostPlaybackProgress(totalDuration, totalDuration);
                Post("playbackState", new { playing = false });
            }
            catch (OperationCanceledException)
            {
                Post("activeMessage", new { id = "", index = -1 });
                Post("audioBusy", new { busy = false });
                Post("playbackProgress", new { elapsedSeconds = 0, totalSeconds = 0 });
                Post("playbackState", new { playing = false });
            }
            catch (Exception ex)
            {
                Post("activeMessage", new { id = "", index = -1 });
                Post("audioBusy", new { busy = false });
                Post("playbackProgress", new { elapsedSeconds = 0, totalSeconds = 0 });
                Post("playbackState", new { playing = false });
                Post("toast", new { type = "warn", text = ex.Message });
            }
        }

        private void LoadDialogueFromWeb(JsonElement data)
        {
            try
            {
                var id = GetString(data, "id");
                if (string.IsNullOrWhiteSpace(id))
                    return;

                var folder = ResolveDialogueFolder(id);
                var file = Path.Combine(folder, DialogueFileName);

                if (!File.Exists(file))
                {
                    Post("toast", new { type = "warn", text = "Không tìm thấy đối thoại đã lưu." });
                    return;
                }

                var text = File.ReadAllText(file, CardSetStorage.Utf8NoBomEncoding);
                var project = JsonSerializer.Deserialize<DialogueProjectDto>(text, _json);
                if (project == null)
                {
                    Post("toast", new { type = "warn", text = "Không đọc được đối thoại." });
                    return;
                }

                NormalizeProject(project);
                Post("dialogueLoaded", new { project, path = folder });
            }
            catch (Exception ex)
            {
                Post("toast", new { type = "warn", text = ex.Message });
            }
        }

        private async Task GenerateGeminiDialogueAsync(JsonElement data)
        {
            try
            {
                if (!EnsureGeminiKeyOrPrompt())
                    return;

                var mode = GetString(data, "mode");
                var courseId = GetString(data, "courseId");
                var topic = GetString(data, "topic");
                var count = Math.Max(2, Math.Min(24, GetInt(data, "count", 8)));
                var targetLanguageKey = GetString(data, "targetLanguageKey");
                var targetLanguageName = GetString(data, "targetLanguageName");
                var targetLanguageCode = GetString(data, "targetLanguageCode");

                CardSet? sourceSet = null;
                List<CardItem> vocab = new();

                if (string.Equals(mode, "course", StringComparison.OrdinalIgnoreCase))
                {
                    sourceSet = _courses.FirstOrDefault(s => string.Equals(s.Id ?? "", courseId, StringComparison.Ordinal));
                    if (sourceSet == null)
                        throw new InvalidOperationException("Chọn học phần trước khi tạo bằng Gemini.");

                    vocab = CardSetStorage.LoadVocabularyItems(sourceSet);
                }
                else if (string.IsNullOrWhiteSpace(topic))
                {
                    throw new InvalidOperationException("Nhập chủ đề trước khi tạo bằng Gemini.");
                }

                Post("geminiBusy", new { busy = true });
                var generated = await GeminiService.GenerateDialogueAsync(
                    sourceSet,
                    vocab,
                    topic,
                    count,
                    targetLanguageName,
                    targetLanguageCode);

                var project = new DialogueProjectDto
                {
                    Id = BuildProjectId(generated.Title),
                    Title = string.IsNullOrWhiteSpace(generated.Title) ? "Gemini dialogue" : generated.Title.Trim(),
                    LanguageKey = string.IsNullOrWhiteSpace(targetLanguageKey) ? LanguageCodeToKey(targetLanguageCode) : targetLanguageKey.Trim(),
                    LanguageName = targetLanguageName.Trim(),
                    LanguageCode = targetLanguageCode.Trim(),
                    LeftVoice = GetString(data, "leftVoice"),
                    RightVoice = GetString(data, "rightVoice"),
                    Messages = generated.Messages.Select((m, i) => new DialogueMessageDto
                    {
                        Id = $"msg_{Guid.NewGuid():N}",
                        Side = i % 2 == 0 ? "left" : "right",
                        Text = m.Text,
                        PauseSeconds = m.PauseSeconds <= 0 ? 0.8 : m.PauseSeconds,
                        Hidden = false
                    }).ToList()
                };

                if (string.IsNullOrWhiteSpace(project.LeftVoice))
                    project.LeftVoice = EdgeTtsRunner.ResolveVoiceByLanguageCode(sourceSet?.LanguageCode);
                if (string.IsNullOrWhiteSpace(project.RightVoice))
                    project.RightVoice = PickAlternateVoice(project.LeftVoice);

                Post("geminiDialogue", new { project });
            }
            catch (Exception ex)
            {
                if (SettingsService.IsLikelyApiKeyError(ex.Message))
                    PromptApiKeys();
                Post("toast", new { type = "warn", text = ex.Message });
            }
            finally
            {
                Post("geminiBusy", new { busy = false });
            }
        }

        private bool EnsureGeminiKeyOrPrompt()
        {
            if (SettingsService.HasGeminiApiKey())
                return true;

            PromptApiKeys();
            return false;
        }

        private void PromptApiKeys()
        {
            if (FindForm() is CardFormWeb form)
                form.PromptForApiKeysFromFeature();
        }

        private void RenameDialogueFromWeb(JsonElement data)
        {
            try
            {
                var id = GetString(data, "id");
                var title = GetString(data, "title").Trim();
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(title))
                    return;

                var folder = ResolveDialogueFolder(id);
                var file = Path.Combine(folder, DialogueFileName);
                if (!File.Exists(file))
                {
                    Post("toast", new { type = "warn", text = "Không tìm thấy đối thoại đã lưu." });
                    return;
                }

                var text = File.ReadAllText(file, CardSetStorage.Utf8NoBomEncoding);
                var project = JsonSerializer.Deserialize<DialogueProjectDto>(text, _json);
                if (project == null)
                {
                    Post("toast", new { type = "warn", text = "Không đọc được đối thoại." });
                    return;
                }

                project.Title = title;
                project.UpdatedAt = DateTimeOffset.Now.ToString("O");
                var json = JsonSerializer.Serialize(project, _json);
                File.WriteAllText(file, json, CardSetStorage.Utf8NoBomEncoding);

                Post("dialogueRenamed", new
                {
                    id = MakeSafeFileName(id),
                    title,
                    dialogues = LoadDialogueSummaries()
                });
                Post("toast", new { type = "ok", text = "Đã sửa tên đối thoại." });
            }
            catch (Exception ex)
            {
                Post("toast", new { type = "warn", text = ex.Message });
            }
        }

        private void DeleteDialogueFromWeb(JsonElement data)
        {
            try
            {
                var id = GetString(data, "id");
                if (string.IsNullOrWhiteSpace(id))
                    return;

                var folder = ResolveDialogueFolder(id);
                if (!Directory.Exists(folder))
                {
                    AddHiddenDialogue(id);
                    Post("dialogueDeleted", new
                    {
                        id = MakeSafeFileName(id),
                        dialogues = LoadDialogueSummaries()
                    });
                    Post("toast", new { type = "ok", text = "Đã xóa đối thoại." });
                    return;
                }

                try
                {
                    Directory.Delete(folder, recursive: true);
                }
                catch (Exception ex) when (IsUnicodePathError(ex))
                {
                    AddHiddenDialogue(id);
                }

                Post("dialogueDeleted", new
                {
                    id = MakeSafeFileName(id),
                    dialogues = LoadDialogueSummaries()
                });
                Post("toast", new { type = "ok", text = "Đã xóa đối thoại." });
            }
            catch (Exception ex)
            {
                Post("toast", new { type = "warn", text = ex.Message });
            }
        }

        private async Task<DialogueSaveResult> SaveProjectAsync(
            DialogueProjectDto project,
            bool generateAudio,
            bool forceRegenerateAudio,
            CancellationToken token)
        {
            NormalizeProject(project);

            var folder = Path.Combine(GetDialogueRoot(), MakeSafeFileName(project.Id));
            Directory.CreateDirectory(folder);
            var expectedAudioFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < project.Messages.Count; i++)
            {
                token.ThrowIfCancellationRequested();

                var message = project.Messages[i];
                if (string.IsNullOrWhiteSpace(message.Text))
                {
                    message.AudioFile = "";
                    continue;
                }

                var voice = string.Equals(message.Side, "right", StringComparison.OrdinalIgnoreCase)
                    ? project.RightVoice
                    : project.LeftVoice;

                voice = string.IsNullOrWhiteSpace(voice) ? EdgeTtsRunner.DefaultVoice : voice.Trim();
                var fileName = $"{i + 1:000}_{message.Side}_{HashTextToFileName($"{voice}|{message.Text}")}.mp3";
                var path = Path.Combine(folder, fileName);
                message.AudioFile = fileName;
                expectedAudioFiles.Add(fileName);

                if (!generateAudio || (File.Exists(path) && !forceRegenerateAudio))
                    continue;

                var mp3 = await EdgeTtsRunner.SynthesizeMp3Async(message.Text, voice, token);
                await File.WriteAllBytesAsync(path, mp3, token);
            }

            if (generateAudio && forceRegenerateAudio)
            {
                foreach (var audioFile in Directory.EnumerateFiles(folder, "*.mp3", SearchOption.TopDirectoryOnly))
                {
                    var name = Path.GetFileName(audioFile);
                    if (!expectedAudioFiles.Contains(name))
                    {
                        try { File.Delete(audioFile); } catch { }
                    }
                }
            }

            project.UpdatedAt = DateTimeOffset.Now.ToString("O");
            var json = JsonSerializer.Serialize(project, _json);
            await File.WriteAllTextAsync(Path.Combine(folder, DialogueFileName), json, CardSetStorage.Utf8NoBomEncoding, token);

            return new DialogueSaveResult
            {
                FolderPath = folder,
                Project = project
            };
        }

        private List<PlaybackSegment> BuildPlaybackSegments(DialogueProjectDto project, string folderPath)
        {
            var result = new List<PlaybackSegment>();

            for (int i = 0; i < project.Messages.Count; i++)
            {
                var message = project.Messages[i];
                if (string.IsNullOrWhiteSpace(message.Text) || string.IsNullOrWhiteSpace(message.AudioFile))
                    continue;

                var audioPath = Path.Combine(folderPath, message.AudioFile);
                if (!File.Exists(audioPath))
                    continue;

                result.Add(new PlaybackSegment
                {
                    Index = i,
                    Message = message,
                    AudioPath = audioPath,
                    Duration = GetMp3Duration(audioPath),
                    Pause = TimeSpan.FromSeconds(Math.Max(0, message.PauseSeconds))
                });
            }

            return result;
        }

        private async Task PlayMp3FileAsync(string path, TimeSpan elapsedBefore, TimeSpan totalDuration, CancellationToken token)
        {
            var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

            using var reader = new Mp3FileReader(path);
            using var output = new WaveOutEvent();

            output.PlaybackStopped += (_, e) =>
            {
                if (e.Exception != null)
                    tcs.TrySetException(e.Exception);
                else
                    tcs.TrySetResult(null);
            };

            using var registration = token.Register(() =>
            {
                try { output.Stop(); } catch { }
                tcs.TrySetCanceled(token);
            });

            output.Init(reader);
            output.Play();

            while (!tcs.Task.IsCompleted)
            {
                token.ThrowIfCancellationRequested();
                PostPlaybackProgress(elapsedBefore + reader.CurrentTime, totalDuration);

                var delay = Task.Delay(140, token);
                var completed = await Task.WhenAny(tcs.Task, delay);
                if (completed == tcs.Task)
                    break;
            }

            await tcs.Task;
        }

        private async Task DelayWithProgressAsync(
            TimeSpan delay,
            TimeSpan elapsedBefore,
            TimeSpan totalDuration,
            CancellationToken token)
        {
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < delay)
            {
                token.ThrowIfCancellationRequested();
                var elapsed = elapsedBefore + TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds, sw.Elapsed.TotalMilliseconds));
                PostPlaybackProgress(elapsed, totalDuration);
                await Task.Delay(140, token);
            }
        }

        private void PostPlaybackProgress(TimeSpan elapsed, TimeSpan total)
        {
            var totalSeconds = Math.Max(0, total.TotalSeconds);
            var elapsedSeconds = Math.Max(0, Math.Min(totalSeconds, elapsed.TotalSeconds));
            Post("playbackProgress", new { elapsedSeconds, totalSeconds });
        }

        private static TimeSpan GetMp3Duration(string path)
        {
            try
            {
                using var reader = new Mp3FileReader(path);
                return reader.TotalTime;
            }
            catch
            {
                return TimeSpan.Zero;
            }
        }

        private void StopPlayback()
        {
            try { _playCts?.Cancel(); } catch { }
            try { _playCts?.Dispose(); } catch { }
            _playCts = null;
        }

        private DialogueProjectDto ReadProject(JsonElement data)
        {
            if (data.ValueKind == JsonValueKind.Undefined || data.ValueKind == JsonValueKind.Null)
                return new DialogueProjectDto();

            var project = JsonSerializer.Deserialize<DialogueProjectDto>(data.GetRawText(), _json);
            return project ?? new DialogueProjectDto();
        }

        private static void NormalizeProject(DialogueProjectDto project)
        {
            project.Title = string.IsNullOrWhiteSpace(project.Title) ? "Dialogue" : project.Title.Trim();
            project.Id = string.IsNullOrWhiteSpace(project.Id) ? BuildProjectId(project.Title) : MakeSafeFileName(project.Id);
            project.LanguageKey = (project.LanguageKey ?? "").Trim();
            project.LanguageName = (project.LanguageName ?? "").Trim();
            project.LanguageCode = (project.LanguageCode ?? "").Trim();
            var languageForVoice = string.IsNullOrWhiteSpace(project.LanguageCode) ? project.LanguageKey : project.LanguageCode;
            project.LeftVoice = string.IsNullOrWhiteSpace(project.LeftVoice)
                ? EdgeTtsRunner.ResolveVoiceByLanguageCode(languageForVoice)
                : project.LeftVoice.Trim();
            project.RightVoice = string.IsNullOrWhiteSpace(project.RightVoice) ? PickAlternateVoice(project.LeftVoice) : project.RightVoice.Trim();
            project.LanguageKey = string.IsNullOrWhiteSpace(project.LanguageKey) ? VoiceToLanguageKey(project.LeftVoice) : project.LanguageKey;
            project.LanguageCode = string.IsNullOrWhiteSpace(project.LanguageCode) ? project.LanguageKey : project.LanguageCode;
            project.CreatedAt = string.IsNullOrWhiteSpace(project.CreatedAt) ? DateTimeOffset.Now.ToString("O") : project.CreatedAt;
            project.Messages ??= new List<DialogueMessageDto>();

            foreach (var message in project.Messages)
            {
                message.Id = string.IsNullOrWhiteSpace(message.Id) ? $"msg_{Guid.NewGuid():N}" : message.Id.Trim();
                message.Side = string.Equals(message.Side, "right", StringComparison.OrdinalIgnoreCase) ? "right" : "left";
                message.Text = (message.Text ?? "").Trim();
                message.PauseSeconds = Math.Max(0, Math.Min(8, message.PauseSeconds));
            }
        }

        private static string GetDialogueRoot()
        {
            var root = Path.Combine(CardSetStorage.BaseDir, DialogueRootFolderName);
            Directory.CreateDirectory(root);
            return root;
        }

        private static string ResolveDialogueFolder(string id)
        {
            var safeId = MakeSafeFileName(id);
            if (string.IsNullOrWhiteSpace(safeId))
                throw new InvalidOperationException("Tên đối thoại không hợp lệ.");

            var root = Path.GetFullPath(GetDialogueRoot()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var folder = Path.GetFullPath(Path.Combine(root, safeId));
            var rootWithSeparator = root + Path.DirectorySeparatorChar;

            if (!folder.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Đường dẫn đối thoại không hợp lệ.");

            if (Directory.Exists(folder))
                return folder;

            foreach (var file in Directory.EnumerateFiles(root, DialogueFileName, SearchOption.AllDirectories))
            {
                try
                {
                    var candidateFolder = Path.GetDirectoryName(file) ?? "";
                    var text = File.ReadAllText(file, CardSetStorage.Utf8NoBomEncoding);
                    var project = JsonSerializer.Deserialize<DialogueProjectDto>(text, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        PropertyNameCaseInsensitive = true
                    });

                    var candidateId = MakeSafeFileName(project?.Id);
                    if (string.IsNullOrWhiteSpace(candidateId))
                        candidateId = MakeSafeFileName(new DirectoryInfo(candidateFolder).Name);

                    if (string.Equals(candidateId, safeId, StringComparison.OrdinalIgnoreCase))
                        return candidateFolder;
                }
                catch { }
            }

            return folder;
        }

        private List<DialogueSummaryDto> LoadDialogueSummaries()
        {
            var result = new List<DialogueSummaryDto>();
            var root = GetDialogueRoot();
            var hidden = LoadHiddenDialogueIds();

            foreach (var file in Directory.EnumerateFiles(root, DialogueFileName, SearchOption.AllDirectories))
            {
                try
                {
                    var text = File.ReadAllText(file, CardSetStorage.Utf8NoBomEncoding);
                    var project = JsonSerializer.Deserialize<DialogueProjectDto>(text, _json);
                    if (project == null) continue;

                    var folder = Path.GetDirectoryName(file) ?? "";
                    var folderName = new DirectoryInfo(folder).Name;
                    var summaryId = string.IsNullOrWhiteSpace(project.Id)
                        ? MakeSafeFileName(folderName)
                        : MakeSafeFileName(project.Id);

                    if (hidden.Contains(summaryId))
                        continue;

                    if (result.Any(x => string.Equals(x.Id, summaryId, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    result.Add(new DialogueSummaryDto
                    {
                        Id = summaryId,
                        Title = string.IsNullOrWhiteSpace(project.Title) ? "Dialogue" : project.Title.Trim(),
                        MessageCount = project.Messages?.Count ?? 0,
                        UpdatedAt = string.IsNullOrWhiteSpace(project.UpdatedAt) ? project.CreatedAt : project.UpdatedAt
                    });
                }
                catch { }
            }

            return result
                .OrderByDescending(x => x.UpdatedAt ?? "")
                .ThenBy(x => x.Title)
                .ToList();
        }

        private static HashSet<string> LoadHiddenDialogueIds()
        {
            try
            {
                var path = Path.Combine(GetDialogueRoot(), HiddenDialoguesFileName);
                if (!File.Exists(path))
                    return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                var text = File.ReadAllText(path, CardSetStorage.Utf8NoBomEncoding);
                var ids = JsonSerializer.Deserialize<List<string>>(text) ?? new List<string>();
                return ids
                    .Select(MakeSafeFileName)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static void AddHiddenDialogue(string id)
        {
            var safeId = MakeSafeFileName(id);
            if (string.IsNullOrWhiteSpace(safeId))
                return;

            var hidden = LoadHiddenDialogueIds();
            hidden.Add(safeId);
            var path = Path.Combine(GetDialogueRoot(), HiddenDialoguesFileName);
            var json = JsonSerializer.Serialize(hidden.OrderBy(x => x).ToList(), new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json, CardSetStorage.Utf8NoBomEncoding);
        }

        private static string PickAlternateVoice(string? voice)
        {
            var current = (voice ?? "").Trim();
            var voices = EdgeTtsRunner.GetSupportedVoices();
            var source = voices.FirstOrDefault(v => string.Equals(v.Voice, current, StringComparison.OrdinalIgnoreCase));
            if (source != null)
            {
                var alternateGender = string.Equals(source.Gender, "male", StringComparison.OrdinalIgnoreCase)
                    ? "female"
                    : "male";

                var sameLanguage = voices.FirstOrDefault(v =>
                    string.Equals(v.LanguageKey, source.LanguageKey, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(v.Gender, alternateGender, StringComparison.OrdinalIgnoreCase));

                if (sameLanguage != null)
                    return sameLanguage.Voice;
            }

            return string.IsNullOrWhiteSpace(current) ? "en-US-GuyNeural" : current;
        }

        private static string VoiceToLanguageKey(string? voice)
        {
            var found = EdgeTtsRunner.GetSupportedVoices()
                .FirstOrDefault(v => string.Equals(v.Voice, voice, StringComparison.OrdinalIgnoreCase));

            if (found != null && !string.IsNullOrWhiteSpace(found.LanguageKey))
                return found.LanguageKey;

            var value = (voice ?? "").Trim();
            var dash = value.IndexOf('-', StringComparison.Ordinal);
            return dash > 0 ? value.Substring(0, dash).ToLowerInvariant() : "en";
        }

        private static string LanguageCodeToKey(string? languageCode)
        {
            var value = (languageCode ?? "").Trim();
            if (string.IsNullOrWhiteSpace(value))
                return "";

            var dash = value.IndexOf('-', StringComparison.Ordinal);
            return dash > 0 ? value.Substring(0, dash).ToLowerInvariant() : value.ToLowerInvariant();
        }

        private static string BuildProjectId(string? title)
        {
            var safe = MakeSafeFileName(string.IsNullOrWhiteSpace(title) ? "dialogue" : title);
            if (string.IsNullOrWhiteSpace(safe))
                safe = "dialogue";

            return $"{safe}_{DateTime.Now:yyyyMMdd_HHmmss}";
        }

        private static string MakeSafeFileName(string? value)
        {
            var text = (value ?? "").Trim();
            if (string.IsNullOrWhiteSpace(text))
                return "";

            var normalized = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(normalized.Length);
            foreach (var ch in normalized)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (category == UnicodeCategory.NonSpacingMark)
                    continue;

                if (ch <= 127 && char.IsLetterOrDigit(ch))
                {
                    sb.Append(ch);
                }
                else if (char.IsWhiteSpace(ch) || ch == '-' || ch == '_')
                {
                    sb.Append('_');
                }
                else if (ch == 'đ' || ch == 'Đ')
                {
                    sb.Append(ch == 'Đ' ? 'D' : 'd');
                }
                else if (!Path.GetInvalidFileNameChars().Contains(ch))
                {
                    sb.Append('_');
                }
            }

            var result = sb.ToString().Trim('_');
            while (result.Contains("__", StringComparison.Ordinal))
                result = result.Replace("__", "_");

            return string.IsNullOrWhiteSpace(result) ? "dialogue" : result;
        }

        private static bool IsUnicodePathError(Exception ex)
        {
            return ex.ToString().Contains("No mapping for the Unicode character", StringComparison.OrdinalIgnoreCase) ||
                   ex.ToString().Contains("target multi-byte code page", StringComparison.OrdinalIgnoreCase);
        }

        private static string HashTextToFileName(string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text ?? "");
            var hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash, 0, 12).ToLowerInvariant();
        }

        private static string GetString(JsonElement data, string name)
        {
            try
            {
                if (data.ValueKind == JsonValueKind.Object &&
                    data.TryGetProperty(name, out var prop))
                {
                    return prop.GetString() ?? "";
                }
            }
            catch { }

            return "";
        }

        private static int GetInt(JsonElement data, string name, int fallback)
        {
            try
            {
                if (data.ValueKind == JsonValueKind.Object &&
                    data.TryGetProperty(name, out var prop))
                {
                    if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var value))
                        return value;

                    if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out var parsed))
                        return parsed;
                }
            }
            catch { }

            return fallback;
        }

        private static bool GetBool(JsonElement data, string name)
        {
            try
            {
                if (data.ValueKind == JsonValueKind.Object &&
                    data.TryGetProperty(name, out var prop))
                {
                    if (prop.ValueKind == JsonValueKind.True)
                        return true;

                    if (prop.ValueKind == JsonValueKind.False)
                        return false;

                    if (prop.ValueKind == JsonValueKind.String && bool.TryParse(prop.GetString(), out var parsed))
                        return parsed;
                }
            }
            catch { }

            return false;
        }

        private void Post(string action, object data)
        {
            try
            {
                if (!_ready || _web.CoreWebView2 == null) return;

                var json = JsonSerializer.Serialize(new { action, data }, _json);
                _web.CoreWebView2.PostWebMessageAsJson(json);
            }
            catch { }
        }

        private sealed class DialogueSaveResult
        {
            public string FolderPath { get; set; } = "";
            public DialogueProjectDto Project { get; set; } = new();
        }

        private sealed class DialogueProjectDto
        {
            public string Id { get; set; } = "";
            public string Title { get; set; } = "";
            public string LanguageKey { get; set; } = "";
            public string LanguageName { get; set; } = "";
            public string LanguageCode { get; set; } = "";
            public string LeftVoice { get; set; } = "";
            public string RightVoice { get; set; } = "";
            public string CreatedAt { get; set; } = "";
            public string UpdatedAt { get; set; } = "";
            public List<DialogueMessageDto> Messages { get; set; } = new();
        }

        private sealed class DialogueMessageDto
        {
            public string Id { get; set; } = "";
            public string Side { get; set; } = "left";
            public string Text { get; set; } = "";
            public double PauseSeconds { get; set; } = 0.8;
            public bool Hidden { get; set; }
            public bool Loop { get; set; }
            public string AudioFile { get; set; } = "";
        }

        private sealed class DialogueSummaryDto
        {
            public string Id { get; set; } = "";
            public string Title { get; set; } = "";
            public int MessageCount { get; set; }
            public string UpdatedAt { get; set; } = "";
        }

        private sealed class PlaybackSegment
        {
            public int Index { get; set; }
            public DialogueMessageDto Message { get; set; } = new();
            public string AudioPath { get; set; } = "";
            public TimeSpan Duration { get; set; }
            public TimeSpan Pause { get; set; }
        }
    }
}
