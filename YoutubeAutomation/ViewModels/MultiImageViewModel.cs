using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using NAudio.Wave;
using Newtonsoft.Json.Linq;
using YoutubeAutomation.Models;
using YoutubeAutomation.Prompts;
using YoutubeAutomation.Services;
using YoutubeAutomation.Services.Interfaces;

namespace YoutubeAutomation.ViewModels;

public partial class MultiImageViewModel : ObservableObject
{
    private readonly IOpenRouterService _openRouterService;
    private readonly IGoogleTtsService _googleTtsService;
    private readonly IFfmpegService _ffmpegService;
    private readonly IStableDiffusionService _sdService;
    private readonly IDocumentService _documentService;
    private readonly ProjectSettings _settings;
    private readonly IVideoHistoryService _historyService;

    private CancellationTokenSource? _processingCts;
    private readonly SemaphoreSlim _sdSemaphore = new(1, 1);
    private bool _isRunAll; // true when RunAll controls the pipeline

    // Pipeline timer infrastructure
    private DateTime _pipelineStartTime;
    private CancellationTokenSource? _pipelineTimerCts;
    private Task? _pipelineTimerTask;
    private readonly List<(string Name, TimeSpan Duration)> _pipelineStepTimings = new();
    private DateTime _currentStepStartTime;
    private int _pipelineTotalSteps;
    private int _pipelineCurrentStep;

    // Audio playback (narration preview)
    private WaveOutEvent? _waveOut;
    private AudioFileReader? _audioFileReader;

    // BGM preview playback (separate from narration)
    private WaveOutEvent? _bgmPreviewPlayer;
    private AudioFileReader? _bgmPreviewReader;
    private bool _isSyncingBgmSelection;

    private const string ChainingConsistencyPrefix = "(consistent style:1.2), same character design, same color palette, continuation of scene, ";

    private string StylePrefix => IsRealisticStyle ? SelectedCategory.SdRealisticStylePrefix : SelectedCategory.SdCartoonStylePrefix;
    private string NegativePrompt => IsRealisticStyle ? SelectedCategory.SdRealisticNegativePrompt : SelectedCategory.SdCartoonNegativePrompt;

    [ObservableProperty] private int currentStepIndex;
    [ObservableProperty] private bool isProcessing;
    [ObservableProperty] private string statusMessage = "พร้อมทำงาน";
    [ObservableProperty] private int currentProgress;
    [ObservableProperty] private string topicText = "";
    [ObservableProperty] private int episodeNumber = 1;
    [ObservableProperty] private string finalVideoPath = "";
    [ObservableProperty] private int playingAudioIndex = -1;
    [ObservableProperty] private bool isSdConnected;
    [ObservableProperty] private bool isSdStarting;
    [ObservableProperty] private string sdStatusText = "SD: ยังไม่ได้ตั้งค่า";
    [ObservableProperty] private int imagesGenerated;
    [ObservableProperty] private int totalImages;
    [ObservableProperty] private string? referenceImagePath;
    [ObservableProperty] private double denoisingStrength = 0.6;
    [ObservableProperty] private bool isRealisticStyle;
    [ObservableProperty] private bool useSceneChaining;
    [ObservableProperty] private string selectedModel = "";
    [ObservableProperty] private bool isModelLoading;
    [ObservableProperty] private bool useCloudImageGen;
    [ObservableProperty] private string selectedCloudModel = "";
    [ObservableProperty] private bool bgmEnabled;
    [ObservableProperty] private string bgmFilePath = "";
    [ObservableProperty] private double bgmVolume = 0.25;
    [ObservableProperty] private bool isBgmPreviewPlaying;
    [ObservableProperty] private BgmTrackDisplayItem? selectedBgmTrack;
    [ObservableProperty] private ContentCategory selectedCategory = ContentCategoryRegistry.Animal;
    private ContentCategory _previousCategory = ContentCategoryRegistry.Animal;
    [ObservableProperty] private bool isCategoryLocked;
    [ObservableProperty] private string coverImagePath = "";
    [ObservableProperty] private string coverImageForYouTubePath = "";
    [ObservableProperty] private string coverImagePrompt = "";

    // Pipeline timer (visible during RunAll / AutoFromImages only)
    [ObservableProperty] private bool isPipelineRunning;
    [ObservableProperty] private string pipelineElapsedText = "";
    [ObservableProperty] private string pipelineStepLabel = "";
    [ObservableProperty] private string pipelineCompletedSteps = "";
    [ObservableProperty] private string pipelineSummary = "";
    [ObservableProperty] private bool isPipelineComplete;

    public ObservableCollection<ContentCategory> AvailableCategories { get; } = new(ContentCategoryRegistry.All);

    public string BgmFileName => string.IsNullOrWhiteSpace(BgmFilePath)
        ? "(ไม่ได้เลือก)"
        : Path.GetFileName(BgmFilePath);

    public SnackbarMessageQueue SnackbarQueue { get; } = new(TimeSpan.FromSeconds(2));
    public ObservableCollection<ScenePart> Parts { get; } = new();
    public ObservableCollection<BgmTrackDisplayItem> BgmTrackItems { get; } = new();
    public ObservableCollection<string> AvailableModels { get; } = new();
    public ObservableCollection<string> AvailableCloudModels { get; } = new()
    {
        "gemini-2.5-flash-image",
        "gemini-3-pro-image-preview"
    };
    public ObservableCollection<bool> StepCompleted { get; } = new() { false, false, false, false, false, false };

    /// <summary>Detect SDXL/XL model by name keywords.</summary>
    public bool IsXlModel => !string.IsNullOrWhiteSpace(SelectedModel) &&
        (SelectedModel.Contains("xl", StringComparison.OrdinalIgnoreCase) ||
         SelectedModel.Contains("juggernaut", StringComparison.OrdinalIgnoreCase) ||
         SelectedModel.Contains("pony", StringComparison.OrdinalIgnoreCase));

    private int ImageWidth => IsXlModel ? 1152 : 768;
    private int ImageHeight => IsXlModel ? 648 : 432;

    public int TotalSceneCount => Parts.Sum(p => p.Scenes.Count);
    public List<SceneData> AllScenes => Parts.SelectMany(p => p.Scenes).ToList();

    public bool ShowSdControls => !UseCloudImageGen;
    public bool ShowCloudControls => UseCloudImageGen;

    partial void OnCoverImagePathChanged(string value)
    {
        // Auto-create YouTube-ready cover (≤ 2MB) whenever cover image changes
        if (!string.IsNullOrWhiteSpace(value) && File.Exists(value))
        {
            try
            {
                var outputFolder = GetOutputFolder();
                CoverImageForYouTubePath = CompressImageIfNeeded(value, outputFolder, "cover_youtube.jpg");
                AppLogger.Log($"YouTube cover ready: {CoverImageForYouTubePath} (original: {new FileInfo(value).Length / 1024}KB)");
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "Failed to create YouTube cover");
                CoverImageForYouTubePath = value; // fallback to original
            }
        }
        else
        {
            CoverImageForYouTubePath = "";
        }
    }

    partial void OnUseCloudImageGenChanged(bool value)
    {
        if (!_isRestoringState)
        {
            _settings.UseCloudImageGen = value;
            _settings.Save();
        }
        OnPropertyChanged(nameof(ShowSdControls));
        OnPropertyChanged(nameof(ShowCloudControls));
    }

    partial void OnSelectedCloudModelChanged(string value)
    {
        if (!_isRestoringState && !string.IsNullOrWhiteSpace(value))
        {
            _settings.CloudImageModel = value;
            _settings.Save();
        }
    }

    partial void OnSelectedCategoryChanged(ContentCategory value)
    {
        if (_isRestoringState)
        {
            _previousCategory = value;
            return;
        }

        IsRealisticStyle = value.DefaultRealisticStyle;

        // Warn if changing category when content exists (style mismatch)
        bool hasContent = Parts.Count > 0 && Parts.Any(p => p.Scenes.Count > 0);
        bool hasCover = !string.IsNullOrWhiteSpace(CoverImagePath) && File.Exists(CoverImagePath);

        if (hasContent || hasCover)
        {
            var details = new System.Text.StringBuilder("คุณมี");
            if (hasContent) details.Append("บทและภาพซีน");
            if (hasContent && hasCover) details.Append("และ");
            if (hasCover) details.Append("ภาพปก");
            details.Append("อยู่แล้ว\n\nต้องการเปลี่ยนหมวดหมู่หรือไม่?\n(ภาพปกจะถูกล้างเพื่อสร้างใหม่ตามหมวดหมู่)");

            var result = MessageBox.Show(details.ToString(), "ยืนยันเปลี่ยนหมวดหมู่",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                // Revert to previous category
                _isRestoringState = true;
                SelectedCategory = _previousCategory;
                IsRealisticStyle = _previousCategory.DefaultRealisticStyle;
                _isRestoringState = false;
                AppLogger.Log($"Category change cancelled, reverted to '{_previousCategory.DisplayName}'");
                return;
            }

            AppLogger.Log($"Category changed to '{value.DisplayName}' with existing content - user confirmed");

            // Clear cover so RunAll will regenerate with new category style
            if (hasCover)
            {
                CoverImagePath = "";
                CoverImagePrompt = "";
                ReferenceImagePath = "";
                AppLogger.Log("Category changed: cleared cover image for regeneration");
            }
        }

        _previousCategory = value;

        // Auto-set BGM to category default
        var defaultTrack = BgmLibrary.GetTrackPath(value.DefaultBgmMood);
        if (defaultTrack != null)
        {
            BgmFilePath = defaultTrack;
            BgmEnabled = true;
            SyncBgmSelection();
        }
    }

    partial void OnBgmEnabledChanged(bool value)
    {
        if (!_isRestoringState)
        {
            _settings.BgmEnabled = value;
            _settings.Save();
        }
    }

    partial void OnBgmFilePathChanged(string value)
    {
        if (!_isRestoringState)
        {
            _settings.BgmFilePath = value ?? "";
            _settings.Save();
        }
        OnPropertyChanged(nameof(BgmFileName));
    }

    partial void OnBgmVolumeChanged(double value)
    {
        if (!_isRestoringState)
        {
            _settings.BgmVolume = value;
            _settings.Save();
        }
    }

    partial void OnSelectedBgmTrackChanged(BgmTrackDisplayItem? value)
    {
        if (_isSyncingBgmSelection || _isRestoringState || value == null) return;

        if (value.IsCustomBrowse)
        {
            // User selected "เลือกไฟล์เอง..." — open file dialog
            _isSyncingBgmSelection = true;
            BrowseBgmFile();
            SyncBgmSelection();
            _isSyncingBgmSelection = false;
            return;
        }

        if (value.Track != null)
        {
            var trackPath = BgmLibrary.GetFullPath(value.Track);
            if (File.Exists(trackPath))
            {
                BgmFilePath = trackPath;
                BgmEnabled = true;
            }
            else
            {
                SnackbarQueue.Enqueue($"ไม่พบไฟล์: {value.Track.FileName}");
            }
        }
        else if (value.CustomFilePath != null)
        {
            BgmFilePath = value.CustomFilePath;
            BgmEnabled = true;
        }
    }

    public MultiImageViewModel(
        IOpenRouterService openRouterService,
        IGoogleTtsService googleTtsService,
        IFfmpegService ffmpegService,
        IStableDiffusionService sdService,
        IDocumentService documentService,
        ProjectSettings settings,
        IVideoHistoryService historyService)
    {
        _openRouterService = openRouterService;
        _googleTtsService = googleTtsService;
        _ffmpegService = ffmpegService;
        _sdService = sdService;
        _documentService = documentService;
        _settings = settings;
        _historyService = historyService;
    }

    public void Initialize(string topic, int epNumber, string? coverImagePath = null, ContentCategory? category = null)
    {
        TopicText = topic;
        EpisodeNumber = epNumber;
        if (category != null)
        {
            _categorySetByMainWindow = true;
            _isRestoringState = true;
            SelectedCategory = category;
            _isRestoringState = false;
        }

        // Try load saved state for this episode
        var stateLoaded = TryLoadState();
        if (stateLoaded)
        {
            UpdateStepCompletion();
            SnackbarQueue.Enqueue("โหลดโปรเจกต์ Multi-Image สำเร็จ");
        }
        else if (epNumber != _settings.LastEpisodeNumber && _settings.LastEpisodeNumber > 0)
        {
            // Fallback: try the last EP number used in MultiImage
            var originalEp = EpisodeNumber;
            EpisodeNumber = _settings.LastEpisodeNumber;

            AppLogger.Log($"Initialize: No state for EP{originalEp}, trying fallback to LastEpisodeNumber={EpisodeNumber}");

            stateLoaded = TryLoadState();
            if (stateLoaded)
            {
                UpdateStepCompletion();
                SnackbarQueue.Enqueue($"โหลดโปรเจกต์ EP{EpisodeNumber} สำเร็จ (จาก session ก่อนหน้า)");
                AppLogger.Log($"Initialize: Fallback successful, loaded EP{EpisodeNumber} with topic '{TopicText}'");
            }
            else
            {
                AppLogger.Log($"Initialize: Fallback failed, no state found for EP{EpisodeNumber}");
            }
        }

        // Auto-set reference image from cover if available and no reference set yet
        if (string.IsNullOrWhiteSpace(ReferenceImagePath)
            && !string.IsNullOrWhiteSpace(coverImagePath) && File.Exists(coverImagePath))
        {
            ReferenceImagePath = CompressImageIfNeeded(coverImagePath, GetScenesFolder());
        }

        // Initialize cloud image gen settings
        UseCloudImageGen = _settings.UseCloudImageGen;
        SelectedCloudModel = !string.IsNullOrWhiteSpace(_settings.CloudImageModel)
            ? _settings.CloudImageModel
            : "gemini-2.5-flash-image";

        // Initialize BGM from global settings ONLY if no per-project state was loaded
        if (!stateLoaded)
        {
            BgmEnabled = _settings.BgmEnabled;
            BgmFilePath = _settings.BgmFilePath ?? "";
            BgmVolume = _settings.BgmVolume > 0.001 ? _settings.BgmVolume : 0.25;
        }

        PopulateBgmTrackItems();
        UpdateSdStatus();
    }

    private void PopulateBgmTrackItems()
    {
        _isSyncingBgmSelection = true;
        BgmTrackItems.Clear();
        foreach (var track in BgmLibrary.Tracks)
            BgmTrackItems.Add(BgmTrackDisplayItem.FromTrack(track));
        BgmTrackItems.Add(BgmTrackDisplayItem.BrowseCustom);
        _isSyncingBgmSelection = false;

        // If BGM is enabled but file no longer exists (e.g. old track names after update), reset
        if (BgmEnabled && !string.IsNullOrWhiteSpace(BgmFilePath) && !File.Exists(BgmFilePath))
        {
            BgmFilePath = "";
            BgmEnabled = false;
        }

        // Auto-set default BGM if none selected yet
        if (string.IsNullOrWhiteSpace(BgmFilePath))
        {
            var defaultTrack = BgmLibrary.GetTrackPath(SelectedCategory.DefaultBgmMood);
            if (defaultTrack != null)
            {
                BgmFilePath = defaultTrack;
                BgmEnabled = true;
                if (BgmVolume < 0.01) BgmVolume = 0.25;
            }
        }

        SyncBgmSelection();
    }

    private void SyncBgmSelection()
    {
        _isSyncingBgmSelection = true;
        if (string.IsNullOrWhiteSpace(BgmFilePath))
        {
            SelectedBgmTrack = null;
        }
        else
        {
            // Check if it's a built-in track
            var builtInTrack = BgmLibrary.FindByPath(BgmFilePath);
            if (builtInTrack != null)
            {
                SelectedBgmTrack = BgmTrackItems.FirstOrDefault(i =>
                    i.Track?.FileName == builtInTrack.FileName);
            }
            else if (File.Exists(BgmFilePath))
            {
                // Custom file — insert before "เลือกไฟล์เอง..."
                var existing = BgmTrackItems.FirstOrDefault(i => i.CustomFilePath != null);
                if (existing != null) BgmTrackItems.Remove(existing);

                var customItem = BgmTrackDisplayItem.FromCustomFile(BgmFilePath);
                BgmTrackItems.Insert(BgmTrackItems.Count - 1, customItem);
                SelectedBgmTrack = customItem;
            }
        }
        _isSyncingBgmSelection = false;
    }

    // === New Project ===
    [RelayCommand]
    private void StartNewProject()
    {
        if (IsProcessing) return;

        var result = MessageBox.Show(
            $"ต้องการเริ่มโปรเจคใหม่หรือไม่?\n\n" +
            $"EP{EpisodeNumber} จะถูกบันทึกไว้ แล้วเริ่ม EP{EpisodeNumber + 1} ใหม่",
            "โปรเจคใหม่", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        // Save current state before clearing
        SaveState();

        // Stop audio playback
        StopAudioPlayback();

        // Increment EP
        EpisodeNumber++;

        // Clear data
        TopicText = "";
        Parts.Clear();
        FinalVideoPath = "";
        ImagesGenerated = 0;
        TotalImages = 0;
        PlayingAudioIndex = -1;
        CurrentProgress = 0;
        StatusMessage = "พร้อมทำงาน";

        // Reset step completion
        for (int i = 0; i < StepCompleted.Count; i++)
            StepCompleted[i] = false;

        // Reset to step 0
        CurrentStepIndex = 0;

        // Unlock category for new project
        IsCategoryLocked = false;

        // Keep: SelectedModel, ReferenceImagePath, DenoisingStrength, IsRealisticStyle, UseSceneChaining

        // Update settings
        _settings.LastEpisodeNumber = EpisodeNumber;
        _settings.Save();

        SnackbarQueue.Enqueue($"เริ่มโปรเจคใหม่ EP{EpisodeNumber}");
    }

    // === Load Episode Folder ===
    [RelayCommand]
    private void LoadEpisodeFolder()
    {
        if (IsProcessing) return;

        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "เลือกโฟลเดอร์ Episode ที่ต้องการโหลด"
        };

        if (!string.IsNullOrWhiteSpace(_settings.OutputBasePath) && Directory.Exists(_settings.OutputBasePath))
            dialog.InitialDirectory = _settings.OutputBasePath;

        if (dialog.ShowDialog() != true) return;

        var folder = dialog.FolderName;
        var folderName = Path.GetFileName(folder);

        // Parse EP number from folder name (e.g. "EP39 ทำไม...")
        if (folderName.StartsWith("EP", StringComparison.OrdinalIgnoreCase))
        {
            var numStr = new string(folderName.Skip(2).TakeWhile(char.IsDigit).ToArray());
            if (int.TryParse(numStr, out var epNum))
            {
                // Save current before switching
                SaveState();
                StopAudioPlayback();

                // Reset flag since user is browsing manually (not coming from MainWindow)
                _categorySetByMainWindow = false;

                EpisodeNumber = epNum;

                // Extract topic from folder name (after "EPxx ")
                var epPrefix = $"EP{epNum} ";
                TopicText = folderName.StartsWith(epPrefix)
                    ? folderName.Substring(epPrefix.Length)
                    : "";

                // Try load state from selected folder
                try
                {
                    var state = MultiImageState.Load(folder);
                    if (state != null && state.HasData())
                    {
                        RestoreFromState(state, folder);
                        UpdateStepCompletion();
                        SnackbarQueue.Enqueue($"โหลด EP{epNum} สำเร็จ");
                    }
                    else
                    {
                        // No state file — reset all generation settings
                        _isRestoringState = true;
                        Parts.Clear();
                        IsCategoryLocked = false;
                        FinalVideoPath = "";
                        ImagesGenerated = 0;
                        TotalImages = 0;
                        PlayingAudioIndex = -1;
                        CurrentProgress = 0;
                        StatusMessage = "พร้อมทำงาน";
                        for (int i = 0; i < StepCompleted.Count; i++)
                            StepCompleted[i] = false;
                        CurrentStepIndex = 0;
                        _isRestoringState = false;
                        SnackbarQueue.Enqueue($"เปิดโฟลเดอร์ EP{epNum} (ไม่มีข้อมูลบันทึก)");
                    }
                }
                catch (Exception ex)
                {
                    _isRestoringState = false;
                    SnackbarQueue.Enqueue($"โหลดข้อมูลล้มเหลว: {ex.Message}");
                }

                // Update settings
                _settings.LastEpisodeNumber = EpisodeNumber;
                _settings.Save();
            }
            else
            {
                MessageBox.Show("ไม่สามารถอ่านหมายเลข EP จากชื่อโฟลเดอร์ได้", "แจ้งเตือน",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        else
        {
            MessageBox.Show("กรุณาเลือกโฟลเดอร์ที่ขึ้นต้นด้วย \"EP\" (เช่น EP39 ทำไม...)",
                "แจ้งเตือน", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // === AI BGM: Analyze script mood and auto-select BGM ===
    private async Task AnalyzeMoodAndSetBgmAsync(CancellationToken cancellationToken)
    {
        if (!BgmLibrary.HasAnyTracks()) return;

        // Skip if user has manually set a non-built-in BGM file
        if (!string.IsNullOrWhiteSpace(BgmFilePath) && File.Exists(BgmFilePath)
            && !BgmLibrary.IsBuiltInTrack(BgmFilePath))
            return;

        var scriptSummary = string.Join("\n---\n",
            Parts.Select(p => p.GetFullNarrationText())
                 .Select(t => t.Length > 500 ? t[..500] : t));

        var prompt = PromptTemplates.GetMoodAnalysisPrompt(scriptSummary, SelectedCategory);

        try
        {
            var mood = await _openRouterService.GenerateTextAsync(
                prompt, _settings.ScriptGenerationModel, null, cancellationToken);

            var rawMood = mood.Trim().ToLowerInvariant();
            // Extract keyword dynamically from category's mood list
            var matchedMood = SelectedCategory.MoodDescriptions.Keys
                .FirstOrDefault(m => rawMood.Contains(m))
                ?? SelectedCategory.DefaultBgmMood;
            mood = matchedMood;

            var trackPath = BgmLibrary.GetTrackPath(mood);
            if (trackPath != null)
            {
                BgmFilePath = trackPath;
                BgmEnabled = true;
                if (BgmVolume < 0.01) BgmVolume = 0.25;
                SyncBgmSelection();
                AppLogger.Log($"AI BGM: mood={mood}, track={Path.GetFileName(trackPath)}");
                StatusMessage = $"AI เลือก BGM: {mood}";
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            AppLogger.LogError(ex, "AI BGM mood analysis failed");
            var fallback = BgmLibrary.GetTrackPath("curious");
            if (fallback != null)
            {
                BgmFilePath = fallback;
                BgmEnabled = true;
                if (BgmVolume < 0.01) BgmVolume = 0.25;
                SyncBgmSelection();
            }
        }
    }

    // === Step 0: Generate Scene-Based Script ===
    [RelayCommand]
    private async Task GenerateSceneScriptAsync()
    {
        if (string.IsNullOrWhiteSpace(TopicText))
        {
            MessageBox.Show("กรุณาใส่หัวข้อเรื่อง", "แจ้งเตือน", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var ownsCts = !_isRunAll;
        try
        {
            if (ownsCts)
            {
                IsPipelineRunning = false;
                IsPipelineComplete = false;
                IsProcessing = true;
                CurrentProgress = 0;
                _processingCts?.Cancel();
                _processingCts?.Dispose();
                _processingCts = new CancellationTokenSource();
            }

            Parts.Clear();
            string? previousPartsJson = null;

            for (int part = 1; part <= 3; part++)
            {
                _processingCts!.Token.ThrowIfCancellationRequested();
                StatusMessage = $"กำลังสร้างบท Part {part}/3...";
                CurrentProgress = (part - 1) * 33;

                var prompt = PromptTemplates.GetSceneBasedScriptPrompt(TopicText, part, previousPartsJson, SelectedCategory);
                var response = await _openRouterService.GenerateTextAsync(
                    prompt,
                    _settings.ScriptGenerationModel,
                    null,
                    _processingCts.Token);

                var scenePart = ParseSceneJson(response, part);
                Parts.Add(scenePart);

                // Build context for next part
                var partJson = new JObject(
                    new JProperty("part", part),
                    new JProperty("scenes", new JArray(
                        scenePart.Scenes.Select(s => new JObject(
                            new JProperty("text", s.Text),
                            new JProperty("image_prompt", s.ImagePrompt)
                        ))
                    ))
                );
                previousPartsJson = (previousPartsJson ?? "") + partJson.ToString() + "\n";
            }

            // Assign sequential scene indices
            int idx = 0;
            foreach (var scene in AllScenes)
                scene.SceneIndex = idx++;

            TotalImages = TotalSceneCount;
            OnPropertyChanged(nameof(TotalSceneCount));
            OnPropertyChanged(nameof(AllScenes));

            // Save scripts to .txt and .docx files (like Main Flow)
            await SaveScriptsToFilesAsync();

            // AI auto-select BGM based on script mood
            StatusMessage = "AI กำลังวิเคราะห์ mood เพื่อเลือก BGM...";
            await AnalyzeMoodAndSetBgmAsync(_processingCts!.Token);

            StatusMessage = $"สร้างบทสำเร็จ: {TotalSceneCount} scenes (3 Parts)";
            CurrentProgress = 100;
            UpdateStepCompletion();
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            StatusMessage = $"เกิดข้อผิดพลาด: {ex.Message}";
            if (!_isRunAll)
                MessageBox.Show(ex.Message, "ข้อผิดพลาด", MessageBoxButton.OK, MessageBoxImage.Error);
            throw;
        }
        finally
        {
            if (ownsCts) IsProcessing = false;
        }
    }

    private ScenePart ParseSceneJson(string response, int partNumber)
    {
        // Extract JSON from response (LLM may wrap in ```json ... ```)
        var jsonMatch = Regex.Match(response, @"\{[\s\S]*\}");
        if (!jsonMatch.Success)
            throw new Exception($"Part {partNumber}: ไม่พบ JSON ใน response\n\nResponse:\n{response}");

        var json = jsonMatch.Value;
        var obj = JObject.Parse(json);
        var scenesArray = obj["scenes"] as JArray
            ?? throw new Exception($"Part {partNumber}: ไม่พบ 'scenes' array ใน JSON");

        var part = new ScenePart { PartNumber = partNumber };
        foreach (var sceneToken in scenesArray)
        {
            part.Scenes.Add(new SceneData
            {
                Text = sceneToken["text"]?.ToString() ?? "",
                ImagePrompt = sceneToken["image_prompt"]?.ToString() ?? ""
            });
        }

        if (part.Scenes.Count == 0)
            throw new Exception($"Part {partNumber}: ไม่มี scenes ใน JSON");

        return part;
    }

    // === Step 2: Generate Images ===
    private const string CloudRealisticPrefix = "Generate a photorealistic image. IMPORTANT: This must look like a real photograph taken by a camera, NOT a cartoon, NOT an illustration, NOT a drawing, NOT an animation. Use realistic lighting, real textures, and natural colors.\n\n";

    private async Task<byte[]> GenerateSceneImageAsync(string scenePrompt, string? refImagePath, CancellationToken token)
    {
        if (UseCloudImageGen)
        {
            var cloudPrompt = CloudRealisticPrefix + scenePrompt;
            return await _googleTtsService.GenerateImageAsync(
                cloudPrompt, SelectedCloudModel, null, "16:9", token);
        }

        // SD Local
        var fullPrompt = StylePrefix + scenePrompt;
        if (!string.IsNullOrWhiteSpace(refImagePath) && File.Exists(refImagePath))
        {
            return await _sdService.GenerateImageWithReferenceAsync(
                fullPrompt, refImagePath, DenoisingStrength,
                NegativePrompt, ImageWidth, ImageHeight, token);
        }
        return await _sdService.GenerateImageAsync(
            fullPrompt, NegativePrompt, ImageWidth, ImageHeight, token);
    }

    [RelayCommand]
    private async Task GenerateAllImagesAsync()
    {
        if (AllScenes.Count == 0)
        {
            if (!_isRunAll)
                MessageBox.Show("กรุณาสร้างบทก่อน", "แจ้งเตือน", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var ownsCts = !_isRunAll;
        try
        {
            if (ownsCts)
            {
                IsPipelineRunning = false;
                IsPipelineComplete = false;
                IsProcessing = true;
                CurrentProgress = 0;
                _processingCts?.Cancel();
                _processingCts?.Dispose();
                _processingCts = new CancellationTokenSource();
            }

            // Pre-check: cloud requires API key
            if (UseCloudImageGen && string.IsNullOrWhiteSpace(_settings.GoogleApiKey))
            {
                if (!_isRunAll)
                    MessageBox.Show("กรุณาตั้งค่า Google API Key ก่อนใช้ Cloud Image Gen",
                        "แจ้งเตือน", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Ensure SD is running (auto-launch if needed) — skip for cloud
            if (!UseCloudImageGen && !await EnsureSdRunningAsync(_processingCts!.Token)) return;

            var outputFolder = GetScenesFolder();
            Directory.CreateDirectory(outputFolder);

            var scenes = AllScenes;
            TotalImages = scenes.Count;
            ImagesGenerated = 0;

            var provider = UseCloudImageGen ? $"Cloud({SelectedCloudModel})" : $"SD({SelectedModel})";
            AppLogger.Log($"GenerateAllImages: {scenes.Count} scenes, provider={provider}, chaining={UseSceneChaining}, ref={ReferenceImagePath ?? "none"}");

            // Track previous scene's image for chaining mode
            string? previousSceneImagePath = null;
            int consecutiveFailures = 0;
            const int maxConsecutiveFailures = 3;

            for (int i = 0; i < scenes.Count; i++)
            {
                _processingCts!.Token.ThrowIfCancellationRequested();
                var scene = scenes[i];

                // Skip if already generated
                if (!string.IsNullOrWhiteSpace(scene.ImagePath) && File.Exists(scene.ImagePath))
                {
                    previousSceneImagePath = scene.ImagePath;
                    ImagesGenerated++;
                    continue;
                }

                // === BOOKEND: Scene แรก & สุดท้าย ใช้ภาพปก ===
                if ((i == 0 || i == scenes.Count - 1)
                    && !string.IsNullOrWhiteSpace(ReferenceImagePath) && File.Exists(ReferenceImagePath))
                {
                    var label = i == 0 ? "แรก" : "สุดท้าย (bookend)";
                    StatusMessage = $"ใช้ภาพปกเป็น scene {label}...";
                    var imagePath = Path.Combine(outputFolder, $"scene_{i:D3}.png");
                    File.Copy(ReferenceImagePath, imagePath, overwrite: true);
                    scene.ImagePath = null; // Force PropertyChanged
                    scene.ImagePath = imagePath;
                    previousSceneImagePath = imagePath;
                    ImagesGenerated++;
                    AppLogger.Log($"Scene {i + 1}/{scenes.Count} = cover image ({label})");
                    CurrentProgress = (int)(100.0 * (i + 1) / scenes.Count);
                    OnPropertyChanged(nameof(AllScenes));
                    continue;
                }

                StatusMessage = UseSceneChaining
                    ? $"กำลังสร้างภาพ {i + 1}/{scenes.Count} (ต่อเนื่องจาก scene ก่อนหน้า)..."
                    : $"กำลังสร้างภาพ {i + 1}/{scenes.Count}...";

                const int maxRetries = 3;
                await _sdSemaphore.WaitAsync(_processingCts.Token);
                try
                {
                    for (int attempt = 1; attempt <= maxRetries; attempt++)
                    {
                        try
                        {
                            byte[] imageBytes;

                            if (UseSceneChaining && !string.IsNullOrWhiteSpace(previousSceneImagePath) && File.Exists(previousSceneImagePath))
                            {
                                // Scene Chaining: use previous scene as reference
                                var chainPrompt = UseCloudImageGen
                                    ? CloudRealisticPrefix + scene.ImagePrompt
                                    : ChainingConsistencyPrefix + scene.ImagePrompt;
                                imageBytes = await GenerateSceneImageAsync(chainPrompt, previousSceneImagePath, _processingCts.Token);
                            }
                            else
                            {
                                // Normal mode: txt2img (no reference - each scene independent)
                                imageBytes = await GenerateSceneImageAsync(scene.ImagePrompt, null, _processingCts.Token);
                            }

                            var imagePath = Path.Combine(outputFolder, $"scene_{i:D3}.png");
                            await File.WriteAllBytesAsync(imagePath, imageBytes, _processingCts.Token);
                            scene.ImagePath = null; // Force PropertyChanged even if path is same
                            scene.ImagePath = imagePath;
                            previousSceneImagePath = imagePath;
                            ImagesGenerated++;
                            consecutiveFailures = 0; // Reset on success
                            AppLogger.Log($"Scene {i + 1}/{scenes.Count} OK ({imageBytes.Length / 1024}KB)");
                            break; // Success → exit retry loop
                        }
                        catch (OperationCanceledException) { throw; }
                        catch (Exception ex) when (attempt < maxRetries)
                        {
                            AppLogger.Log($"Scene {i + 1}/{scenes.Count} retry {attempt}/{maxRetries}: {ex.Message}");
                            StatusMessage = $"ภาพ {i + 1} retry ({attempt}/{maxRetries}): {ex.Message}";
                            await Task.Delay(2000 * attempt, _processingCts.Token); // 2s, 4s backoff
                        }
                        catch (Exception ex)
                        {
                            // Final attempt failed
                            consecutiveFailures++;
                            AppLogger.LogError(ex, $"Scene {i + 1}/{scenes.Count} failed (consecutive={consecutiveFailures})");
                            StatusMessage = $"ภาพ {i + 1} ล้มเหลว ({consecutiveFailures}/{maxConsecutiveFailures}): {ex.Message}";

                            if (consecutiveFailures >= maxConsecutiveFailures)
                            {
                                throw new Exception(
                                    $"สร้างภาพล้มเหลวติดต่อกัน {maxConsecutiveFailures} ครั้ง — หยุดการทำงาน\n\n" +
                                    $"สร้างได้: {ImagesGenerated}/{TotalImages} ภาพ\n" +
                                    $"Error ล่าสุด: {ex.Message}");
                            }
                        }
                    }
                }
                catch (OperationCanceledException) { throw; }
                finally
                {
                    _sdSemaphore.Release();
                }

                CurrentProgress = (int)(100.0 * (i + 1) / scenes.Count);
                OnPropertyChanged(nameof(AllScenes));
            }

            // Verify all images were generated
            var missingCount = scenes.Count(s => string.IsNullOrWhiteSpace(s.ImagePath) || !File.Exists(s.ImagePath));
            if (missingCount > 0 && _isRunAll)
            {
                throw new Exception(
                    $"ภาพยังไม่ครบ: ขาด {missingCount}/{TotalImages} ภาพ\n\n" +
                    $"กรุณาสร้างภาพที่เหลือก่อนดำเนินการต่อ");
            }

            AppLogger.Log($"GenerateAllImages DONE: {ImagesGenerated}/{TotalImages}");
            StatusMessage = $"สร้างภาพสำเร็จ {ImagesGenerated}/{TotalImages} ภาพ";
            CurrentProgress = 100;
            UpdateStepCompletion();
        }
        catch (OperationCanceledException ex)
        {
            AppLogger.Log($"GenerateAllImages CANCELLED: {ex.GetType().Name} - {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            AppLogger.LogError(ex, "GenerateAllImages FAILED");
            StatusMessage = $"เกิดข้อผิดพลาด: {ex.Message}";
            if (!_isRunAll)
                MessageBox.Show(ex.Message, "ข้อผิดพลาด", MessageBoxButton.OK, MessageBoxImage.Error);
            throw;
        }
        finally
        {
            if (ownsCts) IsProcessing = false;
        }
    }

    [RelayCommand]
    private async Task GenerateAllImagesNoChainingAsync()
    {
        if (AllScenes.Count == 0)
        {
            MessageBox.Show("กรุณาสร้างบทก่อน", "แจ้งเตือน", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            IsPipelineRunning = false;
            IsPipelineComplete = false;
            IsProcessing = true;
            CurrentProgress = 0;
            _processingCts?.Cancel();
            _processingCts?.Dispose();
            _processingCts = new CancellationTokenSource();

            // Pre-check: cloud requires API key
            if (UseCloudImageGen && string.IsNullOrWhiteSpace(_settings.GoogleApiKey))
            {
                MessageBox.Show("กรุณาตั้งค่า Google API Key ก่อนใช้ Cloud Image Gen",
                    "แจ้งเตือน", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Ensure SD is running — skip for cloud
            if (!UseCloudImageGen && !await EnsureSdRunningAsync(_processingCts.Token)) return;

            var outputFolder = GetScenesFolder();
            Directory.CreateDirectory(outputFolder);

            var scenes = AllScenes;
            TotalImages = scenes.Count;

            // Count already-generated images
            var alreadyDone = scenes.Count(s => !string.IsNullOrWhiteSpace(s.ImagePath) && File.Exists(s.ImagePath));
            ImagesGenerated = alreadyDone;

            // Collect pending scenes
            var pending = scenes
                .Select((s, i) => (scene: s, index: i))
                .Where(x => string.IsNullOrWhiteSpace(x.scene.ImagePath) || !File.Exists(x.scene.ImagePath))
                .ToList();

            // === BOOKEND: Handle Scene แรก & สุดท้ายด้วยภาพปก ===
            if (!string.IsNullOrWhiteSpace(ReferenceImagePath) && File.Exists(ReferenceImagePath))
            {
                var bookendIndices = new HashSet<int> { 0, scenes.Count - 1 };
                var bookends = pending.Where(x => bookendIndices.Contains(x.index)).ToList();

                foreach (var x in bookends)
                {
                    var label = x.index == 0 ? "แรก" : "สุดท้าย (bookend)";
                    var imagePath = Path.Combine(outputFolder, $"scene_{x.index:D3}.png");
                    File.Copy(ReferenceImagePath, imagePath, overwrite: true);
                    x.scene.ImagePath = null; // Force PropertyChanged
                    x.scene.ImagePath = imagePath;
                    ImagesGenerated++;
                    AppLogger.Log($"Scene {x.index + 1}/{scenes.Count} = cover image ({label})");
                }

                // Remove bookends from pending so they don't get regenerated
                pending = pending.Where(x => !bookendIndices.Contains(x.index)).ToList();
                CurrentProgress = (int)(100.0 * ImagesGenerated / TotalImages);
                OnPropertyChanged(nameof(AllScenes));
            }

            if (pending.Count == 0)
            {
                StatusMessage = "ภาพครบทุก scene แล้ว";
                CurrentProgress = 100;
                return;
            }

            var token = _processingCts.Token;
            var dispatcher = Application.Current.Dispatcher;
            // Cloud: 2 concurrent (API rate limits), SD: 3 concurrent (1 GPU + 2 queued)
            var maxConcurrency = UseCloudImageGen ? 2 : 3;
            var concurrency = new SemaphoreSlim(maxConcurrency, maxConcurrency);
            int failureCount = 0;

            StatusMessage = $"กำลังสร้างภาพ {pending.Count} ภาพ (อิสระ, {maxConcurrency} พร้อมกัน)...";

            const int maxRetries = 3;
            var tasks = pending.Select(x => Task.Run(async () =>
            {
                await concurrency.WaitAsync(token);
                try
                {
                    // Abort early if too many failures already
                    if (Volatile.Read(ref failureCount) >= 3)
                        return;

                    for (int attempt = 1; attempt <= maxRetries; attempt++)
                    {
                        try
                        {
                            byte[] imageBytes;
                            if (UseCloudImageGen)
                            {
                                var cloudPrompt = CloudRealisticPrefix + x.scene.ImagePrompt;
                                imageBytes = await _googleTtsService.GenerateImageAsync(
                                    cloudPrompt, SelectedCloudModel, null, "16:9", token);
                            }
                            else
                            {
                                var fullPrompt = StylePrefix + x.scene.ImagePrompt;
                                imageBytes = await _sdService.GenerateImageAsync(
                                    fullPrompt, NegativePrompt, ImageWidth, ImageHeight, token);
                            }

                            var imagePath = Path.Combine(outputFolder, $"scene_{x.index:D3}.png");
                            await File.WriteAllBytesAsync(imagePath, imageBytes, token);

                            dispatcher.Invoke(() =>
                            {
                                x.scene.ImagePath = null; // Force PropertyChanged even if path is same
                                x.scene.ImagePath = imagePath;
                                ImagesGenerated++;
                                CurrentProgress = (int)(100.0 * ImagesGenerated / TotalImages);
                                StatusMessage = $"สร้างภาพแล้ว {ImagesGenerated}/{TotalImages} (อิสระ)...";
                                OnPropertyChanged(nameof(AllScenes));
                            });
                            Interlocked.Exchange(ref failureCount, 0); // Reset on success
                            break; // Success → exit retry loop
                        }
                        catch (OperationCanceledException) { throw; }
                        catch (Exception ex) when (attempt < maxRetries)
                        {
                            dispatcher.Invoke(() =>
                            {
                                StatusMessage = $"ภาพ {x.index + 1} retry ({attempt}/{maxRetries}): {ex.Message}";
                            });
                            AppLogger.Log($"Scene {x.index + 1} retry {attempt}/{maxRetries}: {ex.Message}");
                            await Task.Delay(2000 * attempt, token); // 2s, 4s backoff
                        }
                        catch (Exception ex)
                        {
                            // Final attempt failed
                            var failures = Interlocked.Increment(ref failureCount);
                            dispatcher.Invoke(() =>
                            {
                                StatusMessage = $"ภาพ {x.index + 1} ล้มเหลว ({failures}/3): {ex.Message}";
                            });
                        }
                    }
                }
                catch (OperationCanceledException) { throw; }
                finally
                {
                    concurrency.Release();
                }
            }, token)).ToArray();

            await Task.WhenAll(tasks);

            var finalMissing = scenes.Count(s => string.IsNullOrWhiteSpace(s.ImagePath) || !File.Exists(s.ImagePath));
            if (finalMissing > 0)
            {
                StatusMessage = $"สร้างภาพได้ {ImagesGenerated}/{TotalImages} ภาพ (ขาด {finalMissing} ภาพ)";
                if (failureCount >= 3)
                    MessageBox.Show(
                        $"สร้างภาพล้มเหลวหลายครั้ง — ได้ {ImagesGenerated}/{TotalImages} ภาพ\n\n" +
                        $"กรุณาตรวจสอบ SD Forge แล้วลองอีกครั้ง",
                        "สร้างภาพไม่ครบ", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else
            {
                StatusMessage = $"สร้างภาพสำเร็จ {ImagesGenerated}/{TotalImages} ภาพ (อิสระ)";
            }
            CurrentProgress = 100;
            UpdateStepCompletion();
        }
        catch (OperationCanceledException) { }
        catch (AggregateException ae) when (ae.InnerExceptions.All(e => e is OperationCanceledException)) { }
        catch (Exception ex)
        {
            StatusMessage = $"เกิดข้อผิดพลาด: {ex.Message}";
            MessageBox.Show(ex.Message, "ข้อผิดพลาด", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private async Task RegenerateImageAsync(SceneData scene)
    {
        if (scene == null) return;

        try
        {
            IsProcessing = true;
            _processingCts?.Cancel();
            _processingCts?.Dispose();
            _processingCts = new CancellationTokenSource();

            var outputFolder = GetScenesFolder();
            Directory.CreateDirectory(outputFolder);

            // Scene 0 always uses cover/reference image (no generation needed)
            if (scene.SceneIndex == 0 &&
                !string.IsNullOrWhiteSpace(ReferenceImagePath) && File.Exists(ReferenceImagePath))
            {
                StatusMessage = "ใช้ภาพปกเป็น scene แรก...";
                var imagePath = Path.Combine(outputFolder, "scene_000.png");
                File.Copy(ReferenceImagePath, imagePath, overwrite: true);
                scene.ImagePath = imagePath;
                OnPropertyChanged(nameof(AllScenes));
                StatusMessage = "ใช้ภาพปกเป็น Scene 1 แล้ว";
                SnackbarQueue.Enqueue("ใช้ภาพปกเป็น Scene 1 แล้ว");
                return;
            }

            // Last scene uses cover/reference image as bookend
            var allScenesForBookend = AllScenes;
            if (allScenesForBookend.Count > 1
                && scene.SceneIndex == allScenesForBookend.Count - 1
                && !string.IsNullOrWhiteSpace(ReferenceImagePath) && File.Exists(ReferenceImagePath))
            {
                StatusMessage = "ใช้ภาพปกเป็น scene สุดท้าย (bookend)...";
                var imagePath = Path.Combine(outputFolder, $"scene_{scene.SceneIndex:D3}.png");
                File.Copy(ReferenceImagePath, imagePath, overwrite: true);
                scene.ImagePath = imagePath;
                OnPropertyChanged(nameof(AllScenes));
                StatusMessage = "ใช้ภาพปกเป็น Scene สุดท้าย (bookend) แล้ว";
                SnackbarQueue.Enqueue("ใช้ภาพปกเป็น Scene สุดท้าย (bookend) แล้ว");
                return;
            }

            // Ensure SD is running — skip for cloud
            if (!UseCloudImageGen && !await EnsureSdRunningAsync(_processingCts.Token))
            {
                IsProcessing = false;
                return;
            }

            StatusMessage = $"กำลังสร้างภาพใหม่ Scene {scene.SceneIndex + 1}...";

            byte[] imageBytes;

            // Scene Chaining: use previous scene's image as reference
            if (UseSceneChaining && scene.SceneIndex > 0)
            {
                var allScenes = AllScenes;
                var prevScene = scene.SceneIndex <= allScenes.Count ? allScenes[scene.SceneIndex - 1] : null;
                if (prevScene != null && !string.IsNullOrWhiteSpace(prevScene.ImagePath) && File.Exists(prevScene.ImagePath))
                {
                    var chainPrompt = UseCloudImageGen
                        ? $"Based on the reference image style, create the next scene: {scene.ImagePrompt}"
                        : ChainingConsistencyPrefix + scene.ImagePrompt;
                    imageBytes = await GenerateSceneImageAsync(chainPrompt, prevScene.ImagePath, _processingCts.Token);
                }
                else
                {
                    // Normal mode: txt2img (no reference - independent generation)
                    imageBytes = await GenerateSceneImageAsync(scene.ImagePrompt, null, _processingCts.Token);
                }
            }
            else
            {
                // Manual mode: txt2img (no reference - independent generation)
                imageBytes = await GenerateSceneImageAsync(scene.ImagePrompt, null, _processingCts.Token);
            }

            var imgPath = Path.Combine(outputFolder, $"scene_{scene.SceneIndex:D3}.png");
            await File.WriteAllBytesAsync(imgPath, imageBytes, _processingCts.Token);
            scene.ImagePath = null; // Force PropertyChanged even if path is same
            scene.ImagePath = imgPath;

            OnPropertyChanged(nameof(AllScenes));
            StatusMessage = $"สร้างภาพใหม่ Scene {scene.SceneIndex + 1} สำเร็จ";
            SnackbarQueue.Enqueue($"สร้างภาพ Scene {scene.SceneIndex + 1} ใหม่แล้ว");
        }
        catch (OperationCanceledException) { return; }
        catch (Exception ex)
        {
            StatusMessage = $"เกิดข้อผิดพลาด: {ex.Message}";
            MessageBox.Show(ex.Message, "ข้อผิดพลาด", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private void ClearImage(SceneData scene)
    {
        if (scene == null || string.IsNullOrWhiteSpace(scene.ImagePath)) return;

        try
        {
            if (File.Exists(scene.ImagePath))
                File.Delete(scene.ImagePath);
        }
        catch { }

        scene.ImagePath = null;
        OnPropertyChanged(nameof(AllScenes));
        UpdateStepCompletion();
        SnackbarQueue.Enqueue($"ล้างภาพ Scene {scene.SceneIndex + 1} แล้ว — กด 'สร้างภาพทั้งหมด' เพื่อสร้างใหม่");
    }

    [RelayCommand]
    private void ClearAllImages()
    {
        var scenes = AllScenes;
        if (scenes.Count == 0) return;

        int cleared = 0;
        foreach (var scene in scenes)
        {
            if (string.IsNullOrWhiteSpace(scene.ImagePath)) continue;
            try
            {
                if (File.Exists(scene.ImagePath))
                    File.Delete(scene.ImagePath);
            }
            catch { }
            scene.ImagePath = null;
            cleared++;
        }

        OnPropertyChanged(nameof(AllScenes));
        UpdateStepCompletion();
        SnackbarQueue.Enqueue($"ล้างภาพทั้งหมด {cleared} ภาพแล้ว — กด 'สร้างภาพทั้งหมด' เพื่อ gen ใหม่");
    }

    [RelayCommand]
    private void BrowseReferenceImage()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Image files (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp|All files (*.*)|*.*",
            Title = "เลือกภาพอ้างอิง (Reference Image)"
        };
        if (dlg.ShowDialog() == true)
        {
            ReferenceImagePath = CompressImageIfNeeded(dlg.FileName, GetScenesFolder());
            SnackbarQueue.Enqueue("ตั้งค่าภาพอ้างอิงแล้ว — ภาพใหม่จะถูกสร้างให้มีสไตล์ใกล้เคียง");
        }
    }

    [RelayCommand]
    private void ClearReferenceImage()
    {
        ReferenceImagePath = null;
        SnackbarQueue.Enqueue("ล้างภาพอ้างอิงแล้ว — จะใช้ txt2img ปกติ");
    }

    [RelayCommand]
    private void BrowseBgmFile()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Audio files (*.mp3;*.wav;*.ogg;*.flac)|*.mp3;*.wav;*.ogg;*.flac|All files (*.*)|*.*",
            Title = "เลือกเพลงพื้นหลัง (BGM)"
        };
        if (dlg.ShowDialog() == true)
        {
            BgmFilePath = dlg.FileName;
            BgmEnabled = true;
            // SyncBgmSelection is called by the caller (OnSelectedBgmTrackChanged)
            SnackbarQueue.Enqueue($"ตั้งค่า BGM: {Path.GetFileName(dlg.FileName)}");
        }
    }

    [RelayCommand]
    private void ClearBgm()
    {
        StopBgmPreview();
        BgmFilePath = "";
        BgmEnabled = false;

        // Remove custom file items from dropdown
        var customItems = BgmTrackItems.Where(i => i.CustomFilePath != null).ToList();
        foreach (var ci in customItems) BgmTrackItems.Remove(ci);

        _isSyncingBgmSelection = true;
        SelectedBgmTrack = null;
        _isSyncingBgmSelection = false;

        SnackbarQueue.Enqueue("ล้าง BGM แล้ว");
    }

    [RelayCommand]
    private void CopyScenePrompt(SceneData scene)
    {
        if (scene == null || string.IsNullOrWhiteSpace(scene.ImagePrompt)) return;

        // Build a prompt suitable for Nano Banana / Gemini image generation
        var prompt = UseSceneChaining
            ? $"Based on this image, please help create the next scene with the following details: {scene.ImagePrompt}"
            : scene.ImagePrompt;

        Clipboard.SetText(prompt);
        SnackbarQueue.Enqueue($"คัดลอก prompt Scene {scene.SceneIndex + 1} แล้ว");
    }

    [RelayCommand]
    private void BrowseSceneImage(SceneData scene)
    {
        if (scene == null) return;

        var dlg = new OpenFileDialog
        {
            Filter = "Image files (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp|All files (*.*)|*.*",
            Title = $"เลือกภาพสำหรับ Scene {scene.SceneIndex + 1}"
        };
        if (dlg.ShowDialog() == true)
        {
            try
            {
                var outputFolder = GetScenesFolder();
                Directory.CreateDirectory(outputFolder);

                // Copy imported image to scenes folder with proper naming
                var destPath = Path.Combine(outputFolder, $"scene_{scene.SceneIndex:D3}.png");
                File.Copy(dlg.FileName, destPath, overwrite: true);
                scene.ImagePath = destPath;

                OnPropertyChanged(nameof(AllScenes));
                UpdateStepCompletion();

                // Update counters
                ImagesGenerated = AllScenes.Count(s => !string.IsNullOrWhiteSpace(s.ImagePath) && File.Exists(s.ImagePath));

                SnackbarQueue.Enqueue($"นำเข้าภาพ Scene {scene.SceneIndex + 1} แล้ว");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"ไม่สามารถนำเข้าภาพได้: {ex.Message}", "ข้อผิดพลาด",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // === Step 3: Generate Audio ===
    [RelayCommand]
    private async Task GenerateAudioAsync(int partNumber)
    {
        if (partNumber < 1 || partNumber > Parts.Count) return;
        var part = Parts[partNumber - 1];
        var narration = part.GetFullNarrationText();

        if (string.IsNullOrWhiteSpace(narration))
        {
            MessageBox.Show($"Part {partNumber} ไม่มีบท", "แจ้งเตือน", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _processingCts?.Cancel();
        _processingCts?.Dispose();
        _processingCts = new CancellationTokenSource();
        var cts = CancellationTokenSource.CreateLinkedTokenSource(_processingCts.Token);
        Task? timerTask = null;
        try
        {
            IsProcessing = true;
            CurrentProgress = 0;

            var startTime = DateTime.Now;
            timerTask = Task.Run(async () =>
            {
                try
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        var elapsed = DateTime.Now - startTime;
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            StatusMessage = $"กำลังสร้างเสียง Part {partNumber}... ({elapsed:mm\\:ss})";
                        });
                        await Task.Delay(1000, cts.Token).ConfigureAwait(false);
                    }
                }
                catch { }
            }, cts.Token);

            var progress = new Progress<int>(p => CurrentProgress = p);
            var audioBytes = await _googleTtsService.GenerateAudioAsync(
                narration, _settings.TtsVoice, progress, _processingCts.Token,
                SelectedCategory.TtsVoiceInstruction);

            cts.Cancel();

            var outputFolder = GetOutputFolder();
            Directory.CreateDirectory(outputFolder);
            var audioPath = Path.Combine(outputFolder, $"ep{EpisodeNumber}_{partNumber}.wav");
            await File.WriteAllBytesAsync(audioPath, audioBytes);

            part.AudioPath = audioPath;

            // Calculate duration and scene durations
            part.AudioDurationSeconds = _ffmpegService.GetAudioDuration(audioPath).TotalSeconds;
            part.CalculateSceneDurations();

            OnPropertyChanged(nameof(Parts));
            StatusMessage = $"สร้างเสียง Part {partNumber} สำเร็จ ({TimeSpan.FromSeconds(part.AudioDurationSeconds):mm\\:ss})";
            CurrentProgress = 100;
            UpdateStepCompletion();
        }
        catch (OperationCanceledException) { return; }
        catch (Exception ex)
        {
            StatusMessage = $"เกิดข้อผิดพลาด: {ex.Message}";
            MessageBox.Show(ex.Message, "ข้อผิดพลาด", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            cts.Cancel();
            if (timerTask != null)
                try { await timerTask.ConfigureAwait(false); } catch { }
            cts.Dispose();
            IsProcessing = false;
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }
    }

    [RelayCommand]
    private async Task GenerateAllAudioAsync()
    {
        if (Parts.Count == 0)
        {
            if (!_isRunAll)
                MessageBox.Show("กรุณาสร้างบทก่อน", "แจ้งเตือน", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var ownsCts = !_isRunAll;
        try
        {
            if (ownsCts)
            {
                IsPipelineRunning = false;
                IsPipelineComplete = false;
                IsProcessing = true;
                _processingCts?.Cancel();
                _processingCts?.Dispose();
                _processingCts = new CancellationTokenSource();
            }
            var outputFolder = GetOutputFolder();
            Directory.CreateDirectory(outputFolder);
            var token = _processingCts!.Token;
            var dispatcher = Application.Current.Dispatcher;

            // Collect pending parts
            var pendingParts = new List<(ScenePart part, int index)>();
            for (int i = 0; i < Parts.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(Parts[i].AudioPath) && File.Exists(Parts[i].AudioPath))
                {
                    AppLogger.Log($"Audio Part {i + 1} already exists — skip");
                    continue;
                }
                var narration = Parts[i].GetFullNarrationText();
                if (string.IsNullOrWhiteSpace(narration)) continue;
                pendingParts.Add((Parts[i], i));
            }

            if (pendingParts.Count == 0)
            {
                StatusMessage = "เสียงครบทุก part แล้ว";
                CurrentProgress = 100;
                UpdateStepCompletion();
                return;
            }

            // Shared timer for all parallel TTS calls
            var startTime = DateTime.Now;
            var timerCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            var timerTask = Task.Run(async () =>
            {
                try
                {
                    while (!timerCts.Token.IsCancellationRequested)
                    {
                        var elapsed = DateTime.Now - startTime;
                        dispatcher.Invoke(() =>
                        {
                            var timerLabel = pendingParts.Count == 1
                                ? $"Part {pendingParts[0].index + 1}"
                                : $"{pendingParts.Count} parts พร้อมกัน";
                            StatusMessage = $"กำลังสร้างเสียง {timerLabel}... ({elapsed:mm\\:ss})";
                        });
                        await Task.Delay(1000, timerCts.Token).ConfigureAwait(false);
                    }
                }
                catch { }
            }, timerCts.Token);

            // Fire all parts in parallel
            int completedCount = 0;
            var audioTasks = pendingParts.Select(x => Task.Run(async () =>
            {
                var narration = x.part.GetFullNarrationText();
                var audioBytes = await _googleTtsService.GenerateAudioAsync(
                    narration, _settings.TtsVoice, null, token,
                    SelectedCategory.TtsVoiceInstruction);

                var audioPath = Path.Combine(outputFolder, $"ep{EpisodeNumber}_{x.index + 1}.wav");
                await File.WriteAllBytesAsync(audioPath, audioBytes, token);

                dispatcher.Invoke(() =>
                {
                    x.part.AudioPath = audioPath;
                    x.part.AudioDurationSeconds = _ffmpegService.GetAudioDuration(audioPath).TotalSeconds;
                    x.part.CalculateSceneDurations();
                    var done = Interlocked.Increment(ref completedCount);
                    CurrentProgress = (int)(100.0 * done / pendingParts.Count);
                });

                AppLogger.Log($"Audio Part {x.index + 1} OK ({audioBytes.Length / 1024}KB)");
            }, token)).ToArray();

            try
            {
                await Task.WhenAll(audioTasks);
            }
            finally
            {
                // Always cleanup timer (even on failure/cancel)
                timerCts.Cancel();
                try { await timerTask; } catch { }
                timerCts.Dispose();
            }

            AppLogger.Log("GenerateAllAudio DONE");
            OnPropertyChanged(nameof(Parts));
            StatusMessage = "สร้างเสียงทั้งหมดสำเร็จ";
            CurrentProgress = 100;
            UpdateStepCompletion();
        }
        catch (OperationCanceledException ex)
        {
            AppLogger.Log($"GenerateAllAudio CANCELLED: {ex.GetType().Name}");
            throw;
        }
        catch (Exception ex)
        {
            AppLogger.LogError(ex, "GenerateAllAudio FAILED");
            StatusMessage = $"เกิดข้อผิดพลาด: {ex.Message}";
            if (!_isRunAll)
                MessageBox.Show(ex.Message, "ข้อผิดพลาด", MessageBoxButton.OK, MessageBoxImage.Error);
            throw;
        }
        finally
        {
            if (ownsCts)
            {
                IsProcessing = false;
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    [RelayCommand]
    private void BrowseAudioFile(int partNumber)
    {
        if (partNumber < 1 || partNumber > Parts.Count) return;

        var dialog = new OpenFileDialog
        {
            Filter = "Audio files (*.wav;*.mp3)|*.wav;*.mp3|All files (*.*)|*.*",
            Title = $"เลือกไฟล์เสียง Part {partNumber}"
        };

        if (dialog.ShowDialog() == true)
        {
            var part = Parts[partNumber - 1];
            part.AudioPath = dialog.FileName;
            part.AudioDurationSeconds = _ffmpegService.GetAudioDuration(dialog.FileName).TotalSeconds;
            part.CalculateSceneDurations();
            OnPropertyChanged(nameof(Parts));
            StatusMessage = $"โหลดเสียง Part {partNumber} สำเร็จ";
            UpdateStepCompletion();
        }
    }

    // === Step 4: Create Video ===
    [RelayCommand]
    private async Task CreateVideoAsync()
    {
        var scenes = AllScenes;
        var scenesWithImages = scenes.Where(s => !string.IsNullOrWhiteSpace(s.ImagePath) && File.Exists(s.ImagePath)).ToList();
        var audioFiles = Parts.Where(p => !string.IsNullOrWhiteSpace(p.AudioPath) && File.Exists(p.AudioPath))
            .Select(p => p.AudioPath!).ToList();

        if (scenesWithImages.Count == 0)
        {
            if (!_isRunAll)
                MessageBox.Show("ไม่มีภาพสำหรับสร้างวิดีโอ", "แจ้งเตือน", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (audioFiles.Count == 0)
        {
            if (!_isRunAll)
                MessageBox.Show("ไม่มีเสียงสำหรับสร้างวิดีโอ", "แจ้งเตือน", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Ensure durations are calculated
        foreach (var part in Parts)
        {
            if (part.AudioDurationSeconds <= 0 && !string.IsNullOrWhiteSpace(part.AudioPath) && File.Exists(part.AudioPath))
            {
                part.AudioDurationSeconds = _ffmpegService.GetAudioDuration(part.AudioPath).TotalSeconds;
                part.CalculateSceneDurations();
            }
        }

        // Build scene list with durations
        var sceneImages = scenesWithImages
            .Select(s => (s.ImagePath!, Math.Max(3.0, s.DurationSeconds)))
            .ToList();

        var ownsCts = !_isRunAll;
        try
        {
            if (ownsCts)
            {
                IsPipelineRunning = false;
                IsPipelineComplete = false;
                IsProcessing = true;
                StatusMessage = "กำลังสร้างวิดีโอ...";
                CurrentProgress = 0;
                _processingCts?.Cancel();
                _processingCts?.Dispose();
                _processingCts = new CancellationTokenSource();
            }
            else
            {
                StatusMessage = "กำลังสร้างวิดีโอ...";
            }

            var outputFolder = GetOutputFolder();
            Directory.CreateDirectory(outputFolder);
            var videoPath = Path.Combine(outputFolder, $"EP{EpisodeNumber}_MultiImage.mp4");
            var progress = new Progress<int>(p => CurrentProgress = p);

            // Build BGM options if enabled
            BgmOptions? bgmOptions = null;
            if (BgmEnabled && !string.IsNullOrWhiteSpace(BgmFilePath) && File.Exists(BgmFilePath))
            {
                bgmOptions = new BgmOptions(BgmFilePath, BgmVolume);
            }

            FinalVideoPath = await _ffmpegService.CreateMultiImageVideoAsync(
                sceneImages, audioFiles, videoPath,
                _settings.UseGpuEncoding, progress, _processingCts!.Token, bgmOptions);

            StatusMessage = "สร้างวิดีโอสำเร็จ!";
            CurrentProgress = 100;
            UpdateStepCompletion();
            SaveTopicToHistory();

            // Auto advance to done step
            CurrentStepIndex = 5;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            StatusMessage = $"เกิดข้อผิดพลาด: {ex.Message}";
            if (!_isRunAll)
                MessageBox.Show(ex.Message, "ข้อผิดพลาด", MessageBoxButton.OK, MessageBoxImage.Error);
            throw;
        }
        finally
        {
            if (ownsCts) IsProcessing = false;
        }
    }

    // Save topic to history after video completion (fire-and-forget)
    private void SaveTopicToHistory()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(TopicText))
                {
                    await _historyService.SaveTopicAsync(TopicText);
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "Failed to save topic to history");
            }
        });
    }

    // === Pipeline Timer ===

    private void StartPipelineTimer(int totalSteps)
    {
        _pipelineStepTimings.Clear();
        _pipelineStartTime = DateTime.Now;
        _currentStepStartTime = DateTime.Now;
        _pipelineTotalSteps = totalSteps;
        _pipelineCurrentStep = 0;
        IsPipelineRunning = true;
        IsPipelineComplete = false;
        PipelineCompletedSteps = "";
        PipelineSummary = "";
        PipelineElapsedText = "รวม 00:00";

        _pipelineTimerCts?.Cancel();
        _pipelineTimerCts?.Dispose();
        _pipelineTimerCts = CancellationTokenSource.CreateLinkedTokenSource(_processingCts!.Token);

        var cts = _pipelineTimerCts;
        _pipelineTimerTask = Task.Run(async () =>
        {
            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    var elapsed = DateTime.Now - _pipelineStartTime;
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        PipelineElapsedText = $"รวม {elapsed:mm\\:ss}";
                    });
                    await Task.Delay(1000, cts.Token).ConfigureAwait(false);
                }
            }
            catch { }
        }, cts.Token);
    }

    private void SetPipelineStep(string stepName)
    {
        _pipelineCurrentStep++;
        _currentStepStartTime = DateTime.Now;
        PipelineStepLabel = $"ขั้นตอน {_pipelineCurrentStep}/{_pipelineTotalSteps}: {stepName}";
    }

    private void AdvancePipelineStep(string completedName, string nextName)
    {
        var stepDuration = DateTime.Now - _currentStepStartTime;
        _pipelineStepTimings.Add((completedName, stepDuration));

        PipelineCompletedSteps = string.Join("  |  ",
            _pipelineStepTimings.Select(s => $"{s.Name} {s.Duration:mm\\:ss} \u2713"));

        SetPipelineStep(nextName);
    }

    private async Task StopPipelineTimer(bool completed)
    {
        _pipelineTimerCts?.Cancel();
        if (_pipelineTimerTask != null)
            try { await _pipelineTimerTask; } catch { }
        _pipelineTimerCts?.Dispose();
        _pipelineTimerCts = null;
        _pipelineTimerTask = null;

        var totalElapsed = DateTime.Now - _pipelineStartTime;
        PipelineElapsedText = $"รวม {totalElapsed:mm\\:ss}";

        if (completed && _pipelineStepTimings.Count > 0)
        {
            IsPipelineComplete = true;
            var breakdown = string.Join("  |  ",
                _pipelineStepTimings.Select(s => $"{s.Name} {s.Duration:mm\\:ss}"));
            PipelineSummary = $"เสร็จสมบูรณ์ — รวม {totalElapsed:mm\\:ss}  |  {breakdown}";
        }
        else
        {
            IsPipelineRunning = false;
        }
    }

    // === Run All ===
    [RelayCommand]
    private async Task AutoFromImagesAsync()
    {
        var scenes = AllScenes;
        if (scenes.Count == 0)
        {
            MessageBox.Show("กรุณาสร้างบทก่อน", "แจ้งเตือน", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var existingImages = scenes.Count(s => !string.IsNullOrWhiteSpace(s.ImagePath) && File.Exists(s.ImagePath));
        var existingAudio = Parts.Count(p => !string.IsNullOrWhiteSpace(p.AudioPath) && File.Exists(p.AudioPath));
        var result = MessageBox.Show(
            $"รันอัตโนมัติ: ภาพ + เสียง (ขนาน) → วิดีโอ\n\n" +
            $"ภาพที่มีแล้ว: {existingImages}/{scenes.Count} (ต้อง gen: {scenes.Count - existingImages})\n" +
            $"เสียงที่มีแล้ว: {existingAudio}/3 (ต้อง gen: {3 - existingAudio})\n\n" +
            $"ขั้นตอน:\n1. สร้างภาพ + เสียง พร้อมกัน\n2. สร้างวิดีโอ",
            "ยืนยัน Auto", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        var pipelineCompleted = false;
        try
        {
            IsProcessing = true;
            _processingCts?.Cancel();
            _processingCts?.Dispose();
            _processingCts = new CancellationTokenSource();
            _isRunAll = true;

            AppLogger.Log("AutoFromImages: starting parallel image+audio generation");

            // Pipeline timer: 2 steps (ภาพ+เสียง → วิดีโอ)
            StartPipelineTimer(2);
            SetPipelineStep("ภาพ+เสียง");

            // Run image generation and audio generation in parallel
            CurrentStepIndex = 2;
            StatusMessage = "กำลังสร้างภาพและเสียงพร้อมกัน...";

            var imageTask = GenerateAllImagesAsync();
            var audioTask = GenerateAllAudioAsync();

            // Cross-cancel: if either fails, cancel CTS to stop the other
            var cts = _processingCts;
            _ = imageTask.ContinueWith(t =>
            {
                AppLogger.LogError(t.Exception!, "imageTask faulted, cancelling audio");
                try { cts.Cancel(); } catch { }
            }, TaskContinuationOptions.OnlyOnFaulted);
            _ = audioTask.ContinueWith(t =>
            {
                AppLogger.LogError(t.Exception!, "audioTask faulted, cancelling images");
                try { cts.Cancel(); } catch { }
            }, TaskContinuationOptions.OnlyOnFaulted);

            try
            {
                await Task.WhenAll(imageTask, audioTask);
                AppLogger.Log("AutoFromImages: both tasks completed successfully");
            }
            catch (Exception whenAllEx)
            {
                AppLogger.Log($"AutoFromImages: Task.WhenAll threw {whenAllEx.GetType().Name}");
                AppLogger.Log($"  imageTask: Status={imageTask.Status}, IsFaulted={imageTask.IsFaulted}");
                AppLogger.Log($"  audioTask: Status={audioTask.Status}, IsFaulted={audioTask.IsFaulted}");

                // Extract the real error (ignore secondary cancellations)
                var realError = new[] { imageTask, audioTask }
                    .Where(t => t.IsFaulted)
                    .SelectMany(t => t.Exception!.InnerExceptions)
                    .FirstOrDefault(e => e is not OperationCanceledException);

                if (realError != null)
                {
                    AppLogger.LogError(realError, "AutoFromImages real error");
                    throw realError;
                }
                AppLogger.Log("AutoFromImages: no real error found (all OperationCanceledException), rethrowing");
                throw; // Genuine cancellation
            }

            _processingCts.Token.ThrowIfCancellationRequested();

            // Verify images are complete
            var missingImages = AllScenes.Count(s => string.IsNullOrWhiteSpace(s.ImagePath) || !File.Exists(s.ImagePath));
            if (missingImages > 0)
            {
                StatusMessage = $"ภาพยังไม่ครบ ({missingImages} ภาพขาด) — หยุด pipeline";
                MessageBox.Show(
                    $"ภาพยังไม่ครบ: ขาด {missingImages}/{AllScenes.Count} ภาพ\n\n" +
                    $"กรุณาสร้างภาพที่เหลือก่อน แล้วกด Auto อีกครั้ง",
                    "ภาพไม่ครบ", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Verify audio is complete
            var missingAudio = Parts.Count(p => string.IsNullOrWhiteSpace(p.AudioPath) || !File.Exists(p.AudioPath));
            if (missingAudio > 0)
            {
                StatusMessage = $"เสียงยังไม่ครบ ({missingAudio} part ขาด) — หยุด pipeline";
                MessageBox.Show(
                    $"เสียงยังไม่ครบ: ขาด {missingAudio}/3 parts\n\n" +
                    $"กรุณาสร้างเสียงที่เหลือก่อน",
                    "เสียงไม่ครบ", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Pipeline timer: advance to step 2
            AdvancePipelineStep("ภาพ+เสียง", "วิดีโอ");

            // Step 4: Create video
            AppLogger.Log("AutoFromImages: creating video...");
            CurrentStepIndex = 4;
            await CreateVideoAsync();

            // Pipeline timer: record final step
            _pipelineStepTimings.Add(("วิดีโอ", DateTime.Now - _currentStepStartTime));
            pipelineCompleted = true;

            AppLogger.Log("AutoFromImages: COMPLETE");
            CurrentStepIndex = 5;
            MessageBox.Show("สร้างวิดีโอเสร็จสมบูรณ์!", "สำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
            SaveTopicToHistory();

            var folder = GetOutputFolder();
            if (Directory.Exists(folder))
                System.Diagnostics.Process.Start("explorer.exe", folder);
        }
        catch (OperationCanceledException ex)
        {
            AppLogger.Log($"AutoFromImages: CANCELLED ({ex.GetType().Name}: {ex.Message})");
            return;
        }
        catch (Exception ex)
        {
            AppLogger.LogError(ex, "AutoFromImages FAILED");
            StatusMessage = $"เกิดข้อผิดพลาด: {ex.Message}";
            MessageBox.Show(ex.Message, "ข้อผิดพลาด", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            await StopPipelineTimer(pipelineCompleted);
            _isRunAll = false;
            IsProcessing = false;
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }
    }

    [RelayCommand]
    private async Task RunAllAsync()
    {
        if (string.IsNullOrWhiteSpace(TopicText))
        {
            MessageBox.Show("กรุณาใส่หัวข้อเรื่อง", "แจ้งเตือน", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = MessageBox.Show(
            $"ต้องการรันทั้งหมดอัตโนมัติหรือไม่?\n\n" +
            $"หัวข้อ: {TopicText}\n\n" +
            $"ขั้นตอน:\n1. สร้างบทแบบ Scene\n2. สร้างภาพ + เสียง พร้อมกัน\n3. สร้างวิดีโอ Multi-Image",
            "ยืนยัน Run All", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        var pipelineCompleted = false;
        try
        {
            IsProcessing = true;
            _processingCts?.Cancel();
            _processingCts?.Dispose();
            _processingCts = new CancellationTokenSource();
            _isRunAll = true;

            AppLogger.Log("RunAll: starting full pipeline");

            // ===== Auto-generate cover image if doesn't exist =====
            var outputFolder = GetOutputFolder();
            Directory.CreateDirectory(outputFolder);
            var expectedCoverPath = Path.Combine(outputFolder, $"ปกไทย_ep{EpisodeNumber}.png");

            // Check only if stored CoverImagePath is valid (respect user's manual selection)
            bool needsCoverImage = string.IsNullOrWhiteSpace(CoverImagePath)
                                  || !File.Exists(CoverImagePath);

            if (needsCoverImage)
            {
                AppLogger.Log("RunAll: Auto-generating cover image (doesn't exist)");
                StatusMessage = "กำลังสร้างภาพปกอัตโนมัติ...";
                CurrentProgress = 5;

                try
                {
                    // Validate Google API Key
                    if (string.IsNullOrWhiteSpace(_settings.GoogleApiKey))
                    {
                        MessageBox.Show("กรุณาตั้งค่า Google API Key ก่อนรัน RunAll\n\n" +
                                      "ต้องการ API Key สำหรับสร้างภาพปก\nเปิด Settings → Google API Key (TTS)",
                                        "ต้องการ API Key", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Get reference image
                    string? refImagePath = null;
                    if (!string.IsNullOrEmpty(_settings.ReferenceImagePath) && File.Exists(_settings.ReferenceImagePath))
                    {
                        refImagePath = _settings.ReferenceImagePath;
                    }

                    // Always regenerate prompt based on current topic and category
                    CoverImagePrompt = GenerateCoverImagePrompt(TopicText, SelectedCategory, refImagePath != null);

                    // Retry loop
                    byte[]? imageBytes = null;
                    Exception? lastException = null;
                    for (int attempt = 1; attempt <= 3; attempt++)
                    {
                        try
                        {
                            StatusMessage = attempt == 1
                                ? "กำลังสร้างภาพปก..."
                                : $"พยายามสร้างภาพปกครั้งที่ {attempt}/3...";
                            CurrentProgress = 10 + (attempt * 5);

                            imageBytes = await _googleTtsService.GenerateImageAsync(
                                CoverImagePrompt,
                                _settings.CloudImageModel,
                                refImagePath,
                                "16:9",
                                _processingCts.Token);

                            CurrentProgress = 25;
                            break;
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            lastException = ex;
                            if (attempt < 3)
                            {
                                await Task.Delay(2000, _processingCts.Token);
                            }
                        }
                    }

                    if (imageBytes == null)
                    {
                        throw lastException ?? new Exception("ไม่สามารถสร้างภาพปกได้");
                    }

                    // Save cover image
                    await File.WriteAllBytesAsync(expectedCoverPath, imageBytes);
                    CoverImagePath = expectedCoverPath;

                    // Sync ReferenceImagePath to use the newly created cover image
                    if (File.Exists(expectedCoverPath))
                    {
                        ReferenceImagePath = CompressImageIfNeeded(expectedCoverPath, GetScenesFolder());
                        AppLogger.Log($"RunAll: Synced ReferenceImagePath to cover image: {ReferenceImagePath}");
                    }

                    SaveState();

                    AppLogger.Log($"RunAll: Cover image auto-generated: {CoverImagePath}");
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    AppLogger.LogError(ex, "RunAll: Cover image generation failed");
                    var continueWithoutCover = MessageBox.Show(
                        $"ไม่สามารถสร้างภาพปกได้: {ex.Message}\n\n" +
                        $"ต้องการดำเนินการต่อโดยไม่มีภาพปกหรือไม่?",
                        "ภาพปกไม่สำเร็จ", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                    if (continueWithoutCover != MessageBoxResult.Yes)
                    {
                        StatusMessage = "ยกเลิก RunAll - ต้องมีภาพปก";
                        return;
                    }
                }
            }
            else
            {
                AppLogger.Log($"RunAll: Cover image already exists: {CoverImagePath}");

                // CRITICAL FIX: Also sync ReferenceImagePath to the browsed cover image
                if (File.Exists(CoverImagePath))
                {
                    ReferenceImagePath = CompressImageIfNeeded(CoverImagePath, GetScenesFolder());
                    SaveState();
                    AppLogger.Log($"RunAll: Synced ReferenceImagePath to browsed cover: {ReferenceImagePath}");
                }
            }
            // ===== END: Auto-generate cover image =====

            // Pipeline timer: 3 steps (บท → ภาพ+เสียง → วิดีโอ)
            StartPipelineTimer(3);
            SetPipelineStep("สร้างบท");

            // Step 0: Generate scripts (must complete before images & audio)
            CurrentStepIndex = 0;
            await GenerateSceneScriptAsync();
            _processingCts.Token.ThrowIfCancellationRequested();

            // Validate script generation produced scenes
            if (AllScenes.Count == 0 || Parts.Count == 0)
            {
                StatusMessage = "สร้างบทไม่สำเร็จ — ไม่มี scene ที่สร้างได้";
                MessageBox.Show(
                    "สร้างบทไม่สำเร็จ ไม่มี scene ที่สร้างได้\n\nกรุณาลองใหม่อีกครั้ง",
                    "บทไม่สำเร็จ", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Pipeline timer: advance to step 2
            AdvancePipelineStep("บท", "ภาพ+เสียง");

            // Steps 2+3: Generate images and audio in parallel
            CurrentStepIndex = 2;
            StatusMessage = "กำลังสร้างภาพและเสียงพร้อมกัน...";

            var imageTask = GenerateAllImagesAsync();
            var audioTask = GenerateAllAudioAsync();

            // Cross-cancel: if either fails, cancel CTS to stop the other
            var cts = _processingCts;
            _ = imageTask.ContinueWith(_ => { try { cts.Cancel(); } catch { } }, TaskContinuationOptions.OnlyOnFaulted);
            _ = audioTask.ContinueWith(_ => { try { cts.Cancel(); } catch { } }, TaskContinuationOptions.OnlyOnFaulted);

            try
            {
                await Task.WhenAll(imageTask, audioTask);
            }
            catch
            {
                var realError = new[] { imageTask, audioTask }
                    .Where(t => t.IsFaulted)
                    .SelectMany(t => t.Exception!.InnerExceptions)
                    .FirstOrDefault(e => e is not OperationCanceledException);

                if (realError != null)
                    throw realError;
                throw;
            }

            _processingCts.Token.ThrowIfCancellationRequested();

            // Verify images are complete
            var missingImages = AllScenes.Count(s => string.IsNullOrWhiteSpace(s.ImagePath) || !File.Exists(s.ImagePath));
            if (missingImages > 0)
            {
                StatusMessage = $"ภาพยังไม่ครบ ({missingImages} ภาพขาด) — หยุด pipeline";
                MessageBox.Show(
                    $"ภาพยังไม่ครบ: ขาด {missingImages}/{AllScenes.Count} ภาพ\n\n" +
                    $"กรุณาสร้างภาพที่เหลือก่อน แล้วกด Run All อีกครั้ง",
                    "ภาพไม่ครบ", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Verify audio is complete
            var missingAudio = Parts.Count(p => string.IsNullOrWhiteSpace(p.AudioPath) || !File.Exists(p.AudioPath));
            if (missingAudio > 0)
            {
                StatusMessage = $"เสียงยังไม่ครบ ({missingAudio} part ขาด) — หยุด pipeline";
                MessageBox.Show(
                    $"เสียงยังไม่ครบ: ขาด {missingAudio}/3 parts\n\n" +
                    $"กรุณาสร้างเสียงที่เหลือก่อน",
                    "เสียงไม่ครบ", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Pipeline timer: advance to step 3
            AdvancePipelineStep("ภาพ+เสียง", "วิดีโอ");

            // Step 4: Create video
            CurrentStepIndex = 4;
            await CreateVideoAsync();

            // Pipeline timer: record final step
            _pipelineStepTimings.Add(("วิดีโอ", DateTime.Now - _currentStepStartTime));
            pipelineCompleted = true;

            AppLogger.Log("RunAll: COMPLETE");
            MessageBox.Show("สร้างวิดีโอ Multi-Image เสร็จสมบูรณ์!", "สำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
            SaveTopicToHistory();

            var folder = GetOutputFolder();
            if (Directory.Exists(folder))
                System.Diagnostics.Process.Start("explorer.exe", folder);
        }
        catch (OperationCanceledException ex)
        {
            AppLogger.Log($"RunAll: CANCELLED ({ex.GetType().Name}: {ex.Message})");
            return;
        }
        catch (Exception ex)
        {
            AppLogger.LogError(ex, "RunAll FAILED");
            StatusMessage = $"เกิดข้อผิดพลาด: {ex.Message}";
            MessageBox.Show(ex.Message, "ข้อผิดพลาด", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            await StopPipelineTimer(pipelineCompleted);
            _isRunAll = false;
            IsProcessing = false;
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }
    }

    // === Audio Playback ===
    [RelayCommand]
    private void ToggleAudioPlayback(int partIndex)
    {
        if (PlayingAudioIndex == partIndex)
        {
            StopAudioPlayback();
            return;
        }

        StopAudioPlayback();

        if (partIndex < 0 || partIndex >= Parts.Count) return;
        var path = Parts[partIndex].AudioPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            StatusMessage = "ไม่พบไฟล์เสียง";
            return;
        }

        try
        {
            var reader = new AudioFileReader(path);
            var player = new WaveOutEvent();
            _audioFileReader = reader;
            _waveOut = player;
            player.Init(reader);
            var idx = partIndex;
            player.PlaybackStopped += (s, e) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (PlayingAudioIndex == idx) PlayingAudioIndex = -1;
                });
            };
            player.Play();
            PlayingAudioIndex = partIndex;
        }
        catch (Exception ex)
        {
            StatusMessage = $"เล่นเสียงไม่ได้: {ex.Message}";
            PlayingAudioIndex = -1;
        }
    }

    private void StopAudioPlayback()
    {
        var waveOut = _waveOut;
        var reader = _audioFileReader;
        _waveOut = null;
        _audioFileReader = null;
        try { waveOut?.Stop(); } catch { }
        try { waveOut?.Dispose(); } catch { }
        try { reader?.Dispose(); } catch { }
        PlayingAudioIndex = -1;
    }

    // === BGM Preview Playback ===
    [RelayCommand]
    private void ToggleBgmPreview()
    {
        if (IsBgmPreviewPlaying)
        {
            StopBgmPreview();
            return;
        }

        string? previewPath = null;
        if (SelectedBgmTrack?.Track != null)
            previewPath = BgmLibrary.GetFullPath(SelectedBgmTrack.Track);
        else if (!string.IsNullOrWhiteSpace(SelectedBgmTrack?.CustomFilePath))
            previewPath = SelectedBgmTrack.CustomFilePath;
        else if (!string.IsNullOrWhiteSpace(BgmFilePath))
            previewPath = BgmFilePath;

        if (string.IsNullOrWhiteSpace(previewPath) || !File.Exists(previewPath))
        {
            SnackbarQueue.Enqueue("เลือกเพลง BGM ก่อน");
            return;
        }

        try
        {
            StopBgmPreview();
            var reader = new AudioFileReader(previewPath);
            reader.Volume = Math.Min((float)(BgmVolume * 2), 1.0f);
            var player = new WaveOutEvent();
            _bgmPreviewReader = reader;
            _bgmPreviewPlayer = player;
            player.Init(reader);
            player.PlaybackStopped += (s, e) =>
            {
                Application.Current.Dispatcher.Invoke(() => IsBgmPreviewPlaying = false);
            };
            player.Play();
            IsBgmPreviewPlaying = true;
        }
        catch (Exception ex)
        {
            SnackbarQueue.Enqueue($"เล่น BGM ไม่ได้: {ex.Message}");
            IsBgmPreviewPlaying = false;
        }
    }

    private void StopBgmPreview()
    {
        var player = _bgmPreviewPlayer;
        var reader = _bgmPreviewReader;
        _bgmPreviewPlayer = null;
        _bgmPreviewReader = null;
        try { player?.Stop(); } catch { }
        try { player?.Dispose(); } catch { }
        try { reader?.Dispose(); } catch { }
        IsBgmPreviewPlaying = false;
    }

    // === Navigation ===
    [RelayCommand]
    private void CancelProcessing()
    {
        _processingCts?.Cancel();
        StatusMessage = "ยกเลิกการทำงาน";
        CurrentProgress = 0;
        IsPipelineRunning = false;
        IsPipelineComplete = false;
    }

    [RelayCommand]
    private void NextStep() { if (CurrentStepIndex < 5) CurrentStepIndex++; }

    [RelayCommand]
    private void PreviousStep() { if (CurrentStepIndex > 0) CurrentStepIndex--; }

    [RelayCommand]
    private void GoToStep(int step) { if (step >= 0 && step <= 5) CurrentStepIndex = step; }

    [RelayCommand]
    private void OpenOutputFolder()
    {
        var folder = GetOutputFolder();
        if (Directory.Exists(folder))
            System.Diagnostics.Process.Start("explorer.exe", folder);
        else
            StatusMessage = "ยังไม่มีโฟลเดอร์ Output";
    }

    [RelayCommand]
    private void OpenVideo()
    {
        if (!string.IsNullOrWhiteSpace(FinalVideoPath) && File.Exists(FinalVideoPath))
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(FinalVideoPath) { UseShellExecute = true });
    }

    // === SD Forge Management ===
    [RelayCommand]
    private async Task LaunchSdAsync()
    {
        if (string.IsNullOrWhiteSpace(_settings.StableDiffusionBatPath))
        {
            if (!BrowseSdPath()) return;
        }

        IsSdStarting = true;
        UpdateSdStatus();
        try
        {
            _processingCts?.Cancel();
            _processingCts?.Dispose();
            _processingCts = new CancellationTokenSource();
            var progress = new Progress<string>(s => StatusMessage = s);
            IsSdConnected = await _sdService.LaunchAsync(progress, _processingCts.Token);
            if (IsSdConnected)
            {
                await RefreshModelsAsync();
                StatusMessage = "SD Forge พร้อมใช้งาน";
            }
            else
            {
                StatusMessage = "SD Forge ไม่สามารถเริ่มได้";
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "ยกเลิกการเริ่ม SD Forge";
        }
        catch (Exception ex)
        {
            StatusMessage = $"เกิดข้อผิดพลาด: {ex.Message}";
            MessageBox.Show(ex.Message, "SD Forge", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsSdStarting = false;
            UpdateSdStatus();
        }
    }

    [RelayCommand]
    private async Task StopSdAsync()
    {
        await _sdService.StopAsync();
        IsSdConnected = false;
        UpdateSdStatus();
        StatusMessage = "ปิด SD Forge แล้ว";
    }

    [RelayCommand]
    private void OpenSdPathBrowser()
    {
        BrowseSdPath();
    }

    private bool BrowseSdPath()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Batch files (*.bat)|*.bat|All files (*.*)|*.*",
            Title = "เลือกไฟล์เปิด SD Forge (เช่น run.bat, webui-user.bat)"
        };
        if (dialog.ShowDialog() == true)
        {
            _settings.StableDiffusionBatPath = dialog.FileName;
            _settings.Save();
            UpdateSdStatus();
            SnackbarQueue.Enqueue($"บันทึกที่ตั้ง SD: {dialog.FileName}");
            return true;
        }
        return false;
    }

    private void UpdateSdStatus()
    {
        if (IsSdStarting)
            SdStatusText = "SD: กำลังเริ่ม...";
        else if (IsSdConnected)
            SdStatusText = IsXlModel
                ? $"SD: เชื่อมต่อแล้ว (XL {ImageWidth}x{ImageHeight})"
                : $"SD: เชื่อมต่อแล้ว ({ImageWidth}x{ImageHeight})";
        else if (string.IsNullOrWhiteSpace(_settings.StableDiffusionBatPath))
            SdStatusText = "SD: ยังไม่ได้ตั้งค่า";
        else
            SdStatusText = "SD: ไม่ได้เปิด";
    }

    partial void OnSelectedModelChanged(string value)
    {
        // Update SDXL-aware settings
        OnPropertyChanged(nameof(IsXlModel));
        _settings.StableDiffusionCfgScale = IsXlModel ? 6.0 : 8.5;
        _settings.StableDiffusionModelName = value ?? "";
        UpdateSdStatus();

        // Switch model on SD Forge if connected (skip during initial model list load)
        if (!_isLoadingModels && !string.IsNullOrWhiteSpace(value) && IsSdConnected && !IsModelLoading)
        {
            _ = SwitchModelAsync(value);
        }
    }

    private async Task SwitchModelAsync(string modelName)
    {
        IsModelLoading = true;
        StatusMessage = $"กำลังโหลด model: {modelName}...";
        try
        {
            await _sdService.SetModelAsync(modelName);
            StatusMessage = $"โหลด model สำเร็จ: {modelName}";
            SnackbarQueue.Enqueue($"เปลี่ยน model เป็น {modelName}");
            _settings.Save();
        }
        catch (Exception ex)
        {
            StatusMessage = $"เปลี่ยน model ไม่สำเร็จ: {ex.Message}";
            SnackbarQueue.Enqueue($"เปลี่ยน model ไม่สำเร็จ");
        }
        finally
        {
            IsModelLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshModelsAsync()
    {
        if (!IsSdConnected) return;
        try
        {
            IsModelLoading = true;
            _isLoadingModels = true; // prevent model switch during list rebuild
            StatusMessage = "กำลังสแกน model ใหม่...";
            await _sdService.RefreshCheckpointsAsync();
            StatusMessage = "กำลังโหลดรายชื่อ model...";
            var models = await _sdService.GetModelsAsync();
            var currentModel = await _sdService.GetCurrentModelAsync();

            AvailableModels.Clear();
            foreach (var m in models)
                AvailableModels.Add(m);

            // Set selected without triggering model switch (it's already loaded)
            if (!string.IsNullOrWhiteSpace(currentModel) && AvailableModels.Contains(currentModel))
                SelectedModel = currentModel;

            StatusMessage = $"พบ {models.Count} models (ใช้: {currentModel})";
        }
        catch (Exception ex)
        {
            StatusMessage = $"โหลดรายชื่อ model ไม่ได้: {ex.Message}";
        }
        finally
        {
            _isLoadingModels = false;
            IsModelLoading = false;
        }
    }

    private bool _isLoadingModels; // prevent model switch during initial load
    private bool _isRestoringState; // prevent settings save during state restoration
    private bool _categorySetByMainWindow; // track if category was explicitly passed from MainWindow

    private async Task<bool> EnsureSdRunningAsync(CancellationToken cancellationToken)
    {
        AppLogger.Log("EnsureSdRunning: testing connection...");
        IsSdConnected = await _sdService.TestConnectionAsync();
        if (IsSdConnected)
        {
            // Auto-load model list if not loaded yet
            if (AvailableModels.Count == 0)
                await RefreshModelsAsync();
            UpdateSdStatus();
            return true;
        }

        // First-time: browse for bat path
        if (string.IsNullOrWhiteSpace(_settings.StableDiffusionBatPath))
        {
            if (!BrowseSdPath()) return false;
        }

        // Auto-launch
        IsSdStarting = true;
        UpdateSdStatus();
        try
        {
            var launchProgress = new Progress<string>(s => StatusMessage = s);
            IsSdConnected = await _sdService.LaunchAsync(launchProgress, cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            StatusMessage = $"SD Forge เริ่มไม่ได้: {ex.Message}";
            MessageBox.Show(
                $"ไม่สามารถเริ่ม SD Forge ได้\n\n{ex.Message}",
                "SD Forge", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsSdStarting = false;
            UpdateSdStatus();
        }

        return IsSdConnected;
    }

    // === Helpers ===
    private string GetOutputFolder()
    {
        var folderName = !string.IsNullOrWhiteSpace(TopicText)
            ? $"EP{EpisodeNumber} {SanitizeFolderName(TopicText)}"
            : $"EP{EpisodeNumber}";
        return Path.Combine(_settings.OutputBasePath, folderName);
    }

    private string GetScenesFolder()
        => Path.Combine(GetOutputFolder(), "scenes");

    private const long MaxCoverImageBytes = 2 * 1024 * 1024; // 2MB

    /// <summary>Compress image to ≤2MB if needed. Returns original path if already small enough.</summary>
    internal static string CompressImageIfNeeded(string imagePath, string outputFolder, string outputFileName = "cover_compressed.jpg")
    {
        // Validate inputs first
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            AppLogger.Log("CompressImageIfNeeded: imagePath is null or empty");
            return imagePath;
        }

        if (string.IsNullOrWhiteSpace(outputFolder))
        {
            AppLogger.Log("CompressImageIfNeeded: outputFolder is null or empty");
            return imagePath;
        }

        try
        {
            var fileInfo = new FileInfo(imagePath);
            if (!fileInfo.Exists || fileInfo.Length <= MaxCoverImageBytes)
                return imagePath;

            // Create output directory - this can throw if path is invalid
            try
            {
                Directory.CreateDirectory(outputFolder);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or PathTooLongException)
            {
                AppLogger.LogError(ex, $"CompressImageIfNeeded: Cannot create output folder: {outputFolder}");
                return imagePath; // Fallback to original
            }
            var compressedPath = Path.Combine(outputFolder, outputFileName);

            var bi = new BitmapImage();
            bi.BeginInit();
            bi.UriSource = new Uri(Path.GetFullPath(imagePath));
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.EndInit();
            bi.Freeze();

            BitmapSource source = bi;
            if (bi.PixelWidth > 1920)
            {
                double scale = 1920.0 / bi.PixelWidth;
                source = new TransformedBitmap(bi, new ScaleTransform(scale, scale));
            }

            foreach (var quality in new[] { 90, 80, 70, 60, 50 })
            {
                var encoder = new JpegBitmapEncoder { QualityLevel = quality };
                encoder.Frames.Add(BitmapFrame.Create(source));
                using var ms = new MemoryStream();
                encoder.Save(ms);
                if (ms.Length <= MaxCoverImageBytes)
                {
                    File.WriteAllBytes(compressedPath, ms.ToArray());
                    AppLogger.Log($"Cover compressed: {fileInfo.Length / 1024}KB → {ms.Length / 1024}KB (JPEG q={quality})");
                    return compressedPath;
                }
            }

            // Last resort: resize to 1280px + quality 50
            if (bi.PixelWidth > 1280)
            {
                double finalScale = 1280.0 / bi.PixelWidth;
                source = new TransformedBitmap(bi, new ScaleTransform(finalScale, finalScale));
            }
            var finalEncoder = new JpegBitmapEncoder { QualityLevel = 50 };
            finalEncoder.Frames.Add(BitmapFrame.Create(source));
            using var finalMs = new MemoryStream();
            finalEncoder.Save(finalMs);
            File.WriteAllBytes(compressedPath, finalMs.ToArray());
            AppLogger.Log($"Cover compressed (final): {fileInfo.Length / 1024}KB → {finalMs.Length / 1024}KB");
            return compressedPath;
        }
        catch (Exception ex) when (ex is OutOfMemoryException or NotSupportedException or System.IO.IOException)
        {
            // These are recoverable errors - fallback to original image
            AppLogger.LogError(ex, $"CompressImageIfNeeded failed (recoverable): {ex.GetType().Name}, using original");
            return imagePath;
        }
        catch (Exception ex)
        {
            // Unexpected errors - log and fallback to original (safer than crashing)
            AppLogger.LogError(ex, $"CompressImageIfNeeded failed (unexpected): {ex.GetType().Name}, using original");
            return imagePath;
        }
    }

    private static string SanitizeFolderName(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Where(c => !invalidChars.Contains(c)).ToArray());
        sanitized = sanitized.Replace("?", "").Replace(":", "").Replace("\"", "").Replace("*", "");
        sanitized = sanitized.Trim();
        if (sanitized.Length > 100) sanitized = sanitized.Substring(0, 100);
        return sanitized;
    }

    // === Cover Image Generation ===
    private string GenerateCoverImagePrompt(string topicTitle, ContentCategory category, bool hasReferenceImage)
    {
        var refLine = hasReferenceImage
            ? $"ให้ออกมาโทนสีและสไตล์คล้ายกับภาพตัวอย่างที่แนบมา แต่เปลี่ยนบริบทรูปให้เกี่ยวกับหมวดหมู่: {category.DisplayName}"
            : $"หมวดหมู่: {category.DisplayName}";

        return $@"ช่วยสร้างภาพปกสไตล์วินเทจสำหรับ podcast ไทย
ชื่อเรื่อง: ""{topicTitle}""

{refLine}

รายละเอียด:
- แสดงชื่อเรื่อง ""{topicTitle}"" ในภาพด้วยอักษรไทยที่อ่านง่าย
- สไตล์โปสเตอร์วินเทจไทย บรรยากาศอบอุ่น
- ขนาดภาพ 16:9 เหมาะสำหรับ YouTube thumbnail";
    }

    [RelayCommand]
    private async Task GenerateCoverImageAsync()
    {
        if (string.IsNullOrWhiteSpace(TopicText))
        {
            MessageBox.Show("กรุณาใส่หัวข้อเรื่องก่อน", "แจ้งเตือน", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Validate Google API Key
        if (string.IsNullOrWhiteSpace(_settings.GoogleApiKey))
        {
            MessageBox.Show("กรุณาตั้งค่า Google API Key ก่อน\n\nเปิด Settings → Google API Key (TTS)",
                            "ต้องการ API Key", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            IsProcessing = true;
            StatusMessage = "กำลังสร้างภาพปก...";
            CurrentProgress = 0;
            _processingCts?.Cancel();
            _processingCts = new CancellationTokenSource();

            // Use reference image if available
            string? refImagePath = null;
            if (!string.IsNullOrEmpty(_settings.ReferenceImagePath))
            {
                if (File.Exists(_settings.ReferenceImagePath))
                {
                    refImagePath = _settings.ReferenceImagePath;
                }
                else
                {
                    StatusMessage = "⚠️ รูปอ้างอิงไม่พบ — สร้างภาพโดยไม่มี reference style";
                }
            }

            // Always regenerate prompt based on current topic and category
            StatusMessage = "กำลังสร้าง Prompt สำหรับภาพปก...";
            CoverImagePrompt = GenerateCoverImagePrompt(TopicText, SelectedCategory, refImagePath != null);

            // Retry up to 3 times with same parameters
            byte[]? imageBytes = null;
            Exception? lastException = null;
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    StatusMessage = attempt == 1
                        ? "กำลังสร้างภาพปก (อาจใช้เวลาสักครู่)..."
                        : $"กำลังพยายามสร้างภาพปกครั้งที่ {attempt}/3...";

                    imageBytes = await _googleTtsService.GenerateImageAsync(
                        CoverImagePrompt,
                        _settings.CloudImageModel,
                        refImagePath,
                        "16:9",
                        _processingCts.Token);

                    break; // Success - exit retry loop
                }
                catch (OperationCanceledException)
                {
                    throw; // Re-throw immediately - user wants to cancel
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    if (attempt < 3)
                    {
                        StatusMessage = $"พยายามครั้งที่ {attempt} ล้มเหลว — รอ 2 วินาทีแล้วลองใหม่...";
                        await Task.Delay(2000, _processingCts.Token);
                    }
                }
            }

            if (imageBytes == null)
            {
                throw lastException ?? new Exception("ไม่สามารถสร้างภาพปกได้หลังจากพยายาม 3 ครั้ง");
            }

            // Save the generated image
            var outputFolder = GetOutputFolder();
            Directory.CreateDirectory(outputFolder);

            var imagePath = Path.Combine(outputFolder, $"ปกไทย_ep{EpisodeNumber}.png");
            await File.WriteAllBytesAsync(imagePath, imageBytes);

            CoverImagePath = imagePath;
            StatusMessage = "สร้างภาพปกสำเร็จ";
            CurrentProgress = 100;
            SaveState();

            SnackbarQueue.Enqueue("สร้างภาพปกสำเร็จ!");

            AppLogger.Log($"Cover image generated: {imagePath}");
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "ยกเลิกการสร้างภาพปก";
        }
        catch (Exception ex)
        {
            AppLogger.LogError(ex, "GenerateCoverImageAsync failed");
            StatusMessage = $"เกิดข้อผิดพลาด: {ex.Message}";
            MessageBox.Show($"ไม่สามารถสร้างภาพปกได้: {ex.Message}\n\nลองใช้ปุ่ม Browse เลือกรูปแทน หรือสร้างภาพปกใน MainWindow ก่อน",
                            "ข้อผิดพลาด", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private void BrowseCoverImage()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Image files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|All files (*.*)|*.*",
            Title = "เลือกภาพปก"
        };

        if (dialog.ShowDialog() == true)
        {
            // Validate file exists (race condition protection)
            if (File.Exists(dialog.FileName))
            {
                try
                {
                    // Copy to project folder to prevent external file deletion issues
                    var outputFolder = GetOutputFolder();
                    Directory.CreateDirectory(outputFolder);

                    var extension = Path.GetExtension(dialog.FileName);
                    var targetPath = Path.Combine(outputFolder, $"cover_browsed{extension}");

                    // Copy file (overwrite if exists)
                    File.Copy(dialog.FileName, targetPath, overwrite: true);

                    // Always set CoverImagePath if file copy succeeded
                    CoverImagePath = targetPath;

                    // Non-blocking validation: warn if image might have issues
                    try
                    {
                        using var stream = File.OpenRead(targetPath);
                        var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        bitmap.StreamSource = stream;
                        bitmap.EndInit();
                    }
                    catch (Exception valEx)
                    {
                        AppLogger.Log($"BrowseCoverImage: Image validation warning: {valEx.Message}");
                        SnackbarQueue.Enqueue("⚠️ ภาพอาจแสดงผลไม่ถูกต้อง แต่ยังใช้งานได้");
                    }

                    // Auto-sync reference image to match browsed cover
                    ReferenceImagePath = CompressImageIfNeeded(targetPath, GetScenesFolder());
                    AppLogger.Log($"BrowseCoverImage: Synced ReferenceImagePath to: {ReferenceImagePath}");

                    SaveState();
                    SnackbarQueue.Enqueue($"คัดลอกภาพปกเข้าโปรเจกต์แล้ว");
                    AppLogger.Log($"BrowseCoverImage: Copied from '{dialog.FileName}' to '{targetPath}'");
                }
                catch (Exception ex)
                {
                    AppLogger.LogError(ex, "BrowseCoverImage: Failed to copy file");
                    SnackbarQueue.Enqueue($"⚠️ คัดลอกไฟล์ไม่สำเร็จ: {ex.Message}");
                }
            }
            else
            {
                SnackbarQueue.Enqueue("⚠️ ไฟล์ไม่พบ กรุณาลองใหม่");
            }
        }
    }

    [RelayCommand]
    private void OpenYouTubeCover()
    {
        if (!string.IsNullOrWhiteSpace(CoverImageForYouTubePath) && File.Exists(CoverImageForYouTubePath))
        {
            try
            {
                // Open file in Explorer with the file selected
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{CoverImageForYouTubePath}\"");
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "OpenYouTubeCover failed");
                SnackbarQueue.Enqueue($"เปิดไฟล์ไม่ได้: {ex.Message}");
            }
        }
        else
        {
            SnackbarQueue.Enqueue("ยังไม่มีภาพปก YouTube");
        }
    }

    private void UpdateStepCompletion()
    {
        StepCompleted[0] = Parts.Count == 3 && Parts.All(p => p.Scenes.Count > 0);
        StepCompleted[1] = StepCompleted[0]; // Review is optional
        StepCompleted[2] = AllScenes.Count > 0 && AllScenes.All(s => !string.IsNullOrWhiteSpace(s.ImagePath) && File.Exists(s.ImagePath));
        StepCompleted[3] = Parts.All(p => !string.IsNullOrWhiteSpace(p.AudioPath) && File.Exists(p.AudioPath));
        StepCompleted[4] = !string.IsNullOrWhiteSpace(FinalVideoPath) && File.Exists(FinalVideoPath);
        StepCompleted[5] = StepCompleted[4];
        OnPropertyChanged(nameof(StepCompleted));

        // Lock category selection once scripts exist (prevent thematic inconsistency)
        IsCategoryLocked = Parts.Count > 0 && Parts.Any(p => p.Scenes.Count > 0);

        // Auto-save after every step completion
        SaveState();
    }

    // === State Persistence ===
    public void SaveState()
    {
        if (Parts.Count == 0) return;

        var state = new MultiImageState
        {
            EpisodeNumber = EpisodeNumber,
            TopicText = TopicText,
            CurrentStepIndex = CurrentStepIndex,
            FinalVideoPath = FinalVideoPath,
            StepCompleted = StepCompleted.ToList(),
            ReferenceImagePath = ReferenceImagePath,
            DenoisingStrength = DenoisingStrength,
            IsRealisticStyle = IsRealisticStyle,
            UseSceneChaining = UseSceneChaining,
            SelectedModel = SelectedModel,
            UseCloudImageGen = UseCloudImageGen,
            SelectedCloudModel = SelectedCloudModel,
            BgmFilePath = BgmFilePath,
            BgmVolume = BgmVolume,
            BgmEnabled = BgmEnabled,
            CategoryKey = SelectedCategory.Key,
            CoverImagePath = CoverImagePath,
            CoverImagePrompt = CoverImagePrompt
        };

        foreach (var part in Parts)
        {
            var pd = new MultiImageState.PartData
            {
                PartNumber = part.PartNumber,
                AudioPath = part.AudioPath,
                AudioDurationSeconds = part.AudioDurationSeconds
            };
            foreach (var scene in part.Scenes)
            {
                pd.Scenes.Add(new MultiImageState.SceneRecord
                {
                    SceneIndex = scene.SceneIndex,
                    Text = scene.Text,
                    ImagePrompt = scene.ImagePrompt,
                    ImagePath = scene.ImagePath,
                    DurationSeconds = scene.DurationSeconds
                });
            }
            state.Parts.Add(pd);
        }

        if (!state.Save(GetOutputFolder()))
        {
            SnackbarQueue.Enqueue("บันทึกโปรเจกต์ไม่สำเร็จ! ตรวจสอบโฟลเดอร์ Output");
        }
    }

    private async Task SaveScriptsToFilesAsync()
    {
        if (Parts.Count == 0) return;

        try
        {
            var outputFolder = GetOutputFolder();
            Directory.CreateDirectory(outputFolder);

            for (int i = 0; i < Parts.Count; i++)
            {
                var narration = Parts[i].GetFullNarrationText();
                if (string.IsNullOrWhiteSpace(narration)) continue;

                var partNumber = i + 1;

                // Save as .txt
                var txtPath = Path.Combine(outputFolder, $"บท{partNumber}.txt");
                await File.WriteAllTextAsync(txtPath, narration);

                // Save as .docx
                var docxPath = Path.Combine(outputFolder, $"บท{partNumber}.docx");
                await _documentService.SaveAsDocxAsync(narration, docxPath);
            }

            // Save topic file (once)
            var topicPath = Path.Combine(outputFolder, "หัวข้อเรื่อง.txt");
            if (!File.Exists(topicPath))
            {
                await File.WriteAllTextAsync(topicPath,
                    $"หัวข้อ: {TopicText}\nEpisode: {EpisodeNumber}\nวันที่สร้าง: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            }

            AppLogger.Log($"SaveScriptsToFiles: saved {Parts.Count} parts to {outputFolder}");
        }
        catch (Exception ex)
        {
            AppLogger.LogError(ex, "SaveScriptsToFiles failed");
        }
    }

    private bool TryLoadState()
    {
        // Try direct path first
        var outputFolder = GetOutputFolder();
        var state = MultiImageState.Load(outputFolder);

        // If not found, scan for any EP folder matching this episode number
        if ((state == null || !state.HasData()) && Directory.Exists(_settings.OutputBasePath))
        {
            var prefix = $"EP{EpisodeNumber}";
            var dirs = Directory.GetDirectories(_settings.OutputBasePath, prefix + "*");
            foreach (var dir in dirs)
            {
                // Verify exact EP number match (EP1 should not match EP10)
                var folderName = Path.GetFileName(dir);
                if (folderName.Length > prefix.Length && char.IsDigit(folderName[prefix.Length]))
                    continue; // Skip EP10, EP11, etc. when looking for EP1

                state = MultiImageState.Load(dir);
                if (state != null && state.HasData() && state.EpisodeNumber == EpisodeNumber)
                {
                    outputFolder = dir;
                    break;
                }
                state = null;
            }
        }

        if (state == null || !state.HasData()) return false;
        if (state.EpisodeNumber != EpisodeNumber) return false;

        RestoreFromState(state, outputFolder);
        return true;
    }

    private void RestoreFromState(MultiImageState state, string actualOutputFolder)
    {
        _isRestoringState = true;

        // Extract topic from actual folder path to ensure consistency
        // This handles cases where TopicText had special characters that were sanitized
        var folderName = Path.GetFileName(actualOutputFolder);
        var match = System.Text.RegularExpressions.Regex.Match(folderName, @"^EP\d+\s+(.+)$");
        if (match.Success)
        {
            TopicText = match.Groups[1].Value.Trim();
            AppLogger.Log($"RestoreFromState: Extracted TopicText from folder: '{TopicText}' (EP{state.EpisodeNumber})");
        }
        else
        {
            // Fallback to state if folder name doesn't match pattern
            TopicText = state.TopicText;
            AppLogger.Log($"RestoreFromState: Using TopicText from state: '{TopicText}' (EP{state.EpisodeNumber})");
        }
        CurrentStepIndex = state.CurrentStepIndex;
        FinalVideoPath = state.FinalVideoPath;
        ReferenceImagePath = state.ReferenceImagePath;
        DenoisingStrength = state.DenoisingStrength > 0.01 ? state.DenoisingStrength : 0.6;
        IsRealisticStyle = state.IsRealisticStyle;
        UseSceneChaining = state.UseSceneChaining;

        // Restore model selection without triggering API switch
        if (!string.IsNullOrWhiteSpace(state.SelectedModel))
        {
            _isLoadingModels = true;
            SelectedModel = state.SelectedModel;
            _isLoadingModels = false;
        }

        // Restore cloud image gen settings
        UseCloudImageGen = state.UseCloudImageGen;
        SelectedCloudModel = !string.IsNullOrWhiteSpace(state.SelectedCloudModel)
            ? state.SelectedCloudModel
            : "gemini-2.5-flash-image";

        // Restore category (saved state wins since scripts were generated with it,
        // but notify user if it differs from MainWindow's selection)
        var savedCategory = ContentCategoryRegistry.GetByKey(state.CategoryKey);
        if (_categorySetByMainWindow && savedCategory.Key != SelectedCategory.Key)
        {
            SnackbarQueue.Enqueue($"หมวดหมู่ถูกเปลี่ยนเป็น \"{savedCategory.DisplayName}\" ตามโปรเจกต์ที่บันทึกไว้");
        }
        SelectedCategory = savedCategory;

        // Restore BGM settings
        BgmEnabled = state.BgmEnabled;
        BgmFilePath = state.BgmFilePath ?? "";
        BgmVolume = state.BgmVolume > 0.001 ? state.BgmVolume : 0.25;
        SyncBgmSelection();

        // Restore cover image
        CoverImagePath = state.CoverImagePath ?? "";
        CoverImagePrompt = state.CoverImagePrompt ?? "";

        Parts.Clear();
        foreach (var pd in state.Parts)
        {
            var part = new ScenePart
            {
                PartNumber = pd.PartNumber,
                AudioPath = pd.AudioPath,
                AudioDurationSeconds = pd.AudioDurationSeconds
            };
            foreach (var sr in pd.Scenes)
            {
                part.Scenes.Add(new SceneData
                {
                    SceneIndex = sr.SceneIndex,
                    Text = sr.Text,
                    ImagePrompt = sr.ImagePrompt,
                    ImagePath = sr.ImagePath,
                    DurationSeconds = sr.DurationSeconds
                });
            }
            Parts.Add(part);
        }

        for (int i = 0; i < state.StepCompleted.Count && i < StepCompleted.Count; i++)
            StepCompleted[i] = state.StepCompleted[i];

        TotalImages = TotalSceneCount;
        ImagesGenerated = AllScenes.Count(s => !string.IsNullOrWhiteSpace(s.ImagePath) && File.Exists(s.ImagePath));

        OnPropertyChanged(nameof(TotalSceneCount));
        OnPropertyChanged(nameof(AllScenes));
        OnPropertyChanged(nameof(StepCompleted));

        // Lock category if scripts exist
        IsCategoryLocked = Parts.Count > 0 && Parts.Any(p => p.Scenes.Count > 0);

        _isRestoringState = false;
        StatusMessage = $"โหลดโปรเจกต์ (บันทึกเมื่อ {state.LastSaved:dd/MM/yyyy HH:mm})";
    }

    public void OnWindowClosing()
    {
        SaveState();

        // Sync EP number back to settings so it can be found on restart
        _settings.LastEpisodeNumber = EpisodeNumber;
        _settings.Save();

        _processingCts?.Cancel();
        StopAudioPlayback();
        StopBgmPreview();

        if (_sdService.IsProcessRunning)
        {
            var result = MessageBox.Show(
                "SD Forge ยังเปิดอยู่ ต้องการปิดด้วยหรือไม่?",
                "ปิด SD Forge", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
                _sdService.StopAsync().Wait();
        }
    }
}
