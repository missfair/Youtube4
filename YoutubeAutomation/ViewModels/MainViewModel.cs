using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using NAudio.Wave;
using YoutubeAutomation.Models;
using YoutubeAutomation.Prompts;
using Microsoft.Extensions.DependencyInjection;
using YoutubeAutomation.Services.Interfaces;

namespace YoutubeAutomation.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IOpenRouterService _openRouterService;
    private readonly IGoogleTtsService _googleTtsService;
    private readonly IFfmpegService _ffmpegService;
    private readonly IDocumentService _documentService;
    private readonly ProjectSettings _settings;

    // Feature 2: Cancel support
    private CancellationTokenSource? _processingCts;

    // Feature 3: Audio playback
    private WaveOutEvent? _waveOut;
    private AudioFileReader? _audioFileReader;

    [ObservableProperty] private int currentStepIndex = 0;
    [ObservableProperty] private VideoProject currentProject = new();
    [ObservableProperty] private ProjectSettings projectSettings;
    [ObservableProperty] private bool isProcessing;
    [ObservableProperty] private string statusMessage = "พร้อมทำงาน";
    [ObservableProperty] private int currentProgress;
    [ObservableProperty] private string topicSubject = "";
    [ObservableProperty] private ObservableCollection<VideoTopic> generatedTopics = new();
    [ObservableProperty] private VideoTopic? selectedTopic;
    [ObservableProperty] private ObservableCollection<string> scriptParts = new() { "", "", "" };
    [ObservableProperty] private string coverImagePrompt = "";
    [ObservableProperty] private string coverImagePath = "";
    [ObservableProperty] private ObservableCollection<string> audioPaths = new() { "", "", "" };
    [ObservableProperty] private string finalVideoPath = "";
    [ObservableProperty] private string youtubeTitle = "";
    [ObservableProperty] private string youtubeDescription = "";
    [ObservableProperty] private string youtubeTags = "";
    [ObservableProperty] private bool showSettings;
    [ObservableProperty] private int playingAudioIndex = -1;  // Feature 3
    [ObservableProperty] private string estimatedDuration = "";  // Feature 10

    // Feature 5: Snackbar
    public SnackbarMessageQueue SnackbarQueue { get; } = new(TimeSpan.FromSeconds(2));

    // Feature 1: Step completion states
    public ObservableCollection<bool> StepCompleted { get; } = new() { false, false, false, false, false, false, false };

    // Available models
    public ObservableCollection<string> AvailableModels { get; } = new()
    {
        "google/gemini-2.5-flash",
        "google/gemini-2.5-pro",
        "google/gemini-2.0-flash-001",
        "anthropic/claude-3.5-sonnet",
        "openai/gpt-4o"
    };

    // Available image generation models (multimodal models that may support image output)
    public ObservableCollection<string> AvailableImageModels { get; } = new()
    {
        "google/gemini-2.0-flash-exp:free",
        "google/gemini-2.5-flash",
        "google/gemini-2.5-pro"
    };

    public ObservableCollection<string> AvailableVoices { get; } = new()
    {
        "charon",
        "kore",
        "fenrir",
        "aoede",
        "puck",
        "zephyr",
        "orus",
        "leda"
    };

    public MainViewModel(
        IOpenRouterService openRouterService,
        IGoogleTtsService googleTtsService,
        IFfmpegService ffmpegService,
        IDocumentService documentService,
        ProjectSettings settings)
    {
        _openRouterService = openRouterService;
        _googleTtsService = googleTtsService;
        _ffmpegService = ffmpegService;
        _documentService = documentService;
        _settings = settings;
        ProjectSettings = settings;

        // Load saved project state if exists
        LoadProjectState();
    }

    private void LoadProjectState()
    {
        var state = ProjectState.Load();
        if (state != null && state.HasData())
        {
            TopicSubject = state.TopicSubject;
            CurrentProject.EpisodeNumber = state.EpisodeNumber;

            for (int i = 0; i < 3; i++)
            {
                if (i < state.ScriptParts.Count)
                    ScriptParts[i] = state.ScriptParts[i];
                if (i < state.AudioPaths.Count)
                    AudioPaths[i] = state.AudioPaths[i];
            }

            CoverImagePrompt = state.CoverImagePrompt;
            CoverImagePath = state.CoverImagePath;
            FinalVideoPath = state.FinalVideoPath;
            CurrentStepIndex = state.CurrentStepIndex;

            StatusMessage = $"โหลดโปรเจกต์ล่าสุด (บันทึกเมื่อ {state.LastSaved:dd/MM/yyyy HH:mm})";
        }
    }

    public void SaveProjectState()
    {
        var state = new ProjectState
        {
            EpisodeNumber = CurrentProject.EpisodeNumber,
            TopicSubject = TopicSubject,
            ScriptParts = ScriptParts.ToList(),
            CoverImagePrompt = CoverImagePrompt,
            CoverImagePath = CoverImagePath,
            AudioPaths = AudioPaths.ToList(),
            FinalVideoPath = FinalVideoPath,
            CurrentStepIndex = CurrentStepIndex
        };
        state.Save();
    }

    // Browse: Load existing episode folder
    [RelayCommand]
    private async Task LoadEpisodeFolderAsync()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "เลือกโฟลเดอร์ Episode ที่ต้องการโหลด"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            var folder = dialog.FolderName;
            var folderName = Path.GetFileName(folder);
            StatusMessage = $"กำลังโหลดจาก {folderName}...";

            // Try parse episode number from folder name (e.g. "EP19 ทำไม...")
            if (folderName.StartsWith("EP", StringComparison.OrdinalIgnoreCase))
            {
                var numStr = new string(folderName.Skip(2).TakeWhile(char.IsDigit).ToArray());
                if (int.TryParse(numStr, out var epNum))
                {
                    CurrentProject.EpisodeNumber = epNum;
                    OnPropertyChanged(nameof(CurrentProject));
                }
            }

            // Load topic from หัวข้อเรื่อง.txt
            var topicFile = Path.Combine(folder, "หัวข้อเรื่อง.txt");
            if (File.Exists(topicFile))
            {
                var content = await File.ReadAllTextAsync(topicFile);
                var line = content.Split('\n').FirstOrDefault(l => l.StartsWith("หัวข้อ:"));
                TopicSubject = line != null ? line.Replace("หัวข้อ:", "").Trim() : content.Trim();
            }

            // Load scripts
            for (int i = 1; i <= 3; i++)
            {
                var scriptFile = Path.Combine(folder, $"บท{i}.txt");
                if (File.Exists(scriptFile))
                {
                    ScriptParts[i - 1] = await File.ReadAllTextAsync(scriptFile);
                }
            }
            OnPropertyChanged(nameof(ScriptParts));

            // Load audio paths
            for (int i = 1; i <= 3; i++)
            {
                var pattern = $"ep*_{i}.wav";
                var found = Directory.GetFiles(folder, pattern).FirstOrDefault();
                if (found != null)
                    AudioPaths[i - 1] = found;
            }
            OnPropertyChanged(nameof(AudioPaths));

            // Load cover image
            var coverFiles = Directory.GetFiles(folder, "ปก*.png")
                .Concat(Directory.GetFiles(folder, "ปก*.jpg")).ToArray();
            if (coverFiles.Length > 0)
                CoverImagePath = coverFiles[0];

            // Load video
            var videoFiles = Directory.GetFiles(folder, "EP*_Thai.mp4");
            if (videoFiles.Length > 0)
                FinalVideoPath = videoFiles[0];

            SaveProjectState();
            StatusMessage = $"โหลด {folderName} สำเร็จ";
        }
        catch (Exception ex)
        {
            StatusMessage = $"เกิดข้อผิดพลาด: {ex.Message}";
            MessageBox.Show($"ไม่สามารถโหลด Episode ได้: {ex.Message}", "ข้อผิดพลาด", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // Browse: Load script file
    [RelayCommand]
    private async Task BrowseScriptFileAsync(object partNumberObj)
    {
        var partNumber = Convert.ToInt32(partNumberObj);
        var dialog = new OpenFileDialog
        {
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            Title = $"เลือกไฟล์บทส่วนที่ {partNumber}"
        };

        if (dialog.ShowDialog() == true)
        {
            ScriptParts[partNumber - 1] = await File.ReadAllTextAsync(dialog.FileName);
            OnPropertyChanged(nameof(ScriptParts));
            StatusMessage = $"โหลดบทส่วนที่ {partNumber} สำเร็จ";
            SaveProjectState();
        }
    }

    // Browse: Load audio file
    [RelayCommand]
    private void BrowseAudioFile(object partNumberObj)
    {
        var partNumber = Convert.ToInt32(partNumberObj);
        var dialog = new OpenFileDialog
        {
            Filter = "Audio files (*.wav;*.mp3)|*.wav;*.mp3|All files (*.*)|*.*",
            Title = $"เลือกไฟล์เสียงส่วนที่ {partNumber}"
        };

        if (dialog.ShowDialog() == true)
        {
            AudioPaths[partNumber - 1] = dialog.FileName;
            OnPropertyChanged(nameof(AudioPaths));
            StatusMessage = $"โหลดเสียงส่วนที่ {partNumber} สำเร็จ";
            SaveProjectState();
            UpdateStepCompletionStates();
        }
    }

    // Step 1: Generate Topics
    [RelayCommand]
    private async Task GenerateTopicsAsync()
    {
        if (string.IsNullOrWhiteSpace(TopicSubject))
        {
            MessageBox.Show("กรุณาใส่หัวข้อที่ต้องการ", "แจ้งเตือน", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            IsProcessing = true;
            StatusMessage = "กำลังสร้างหัวข้อ...";
            CurrentProgress = 0;
            _processingCts?.Cancel();
            _processingCts = new CancellationTokenSource();

            var prompt = PromptTemplates.GetTopicGenerationPrompt(TopicSubject);
            var progress = new Progress<int>(p => CurrentProgress = p);

            var response = await _openRouterService.GenerateTextAsync(
                prompt,
                _settings.TopicGenerationModel,
                progress,
                _processingCts.Token);

            // Parse response into topics
            GeneratedTopics.Clear();
            var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            int index = 0;
            string currentTitle = "";
            string currentDesc = "";

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("1.") || trimmed.StartsWith("2.") || trimmed.StartsWith("3.") ||
                    trimmed.StartsWith("4.") || trimmed.StartsWith("5."))
                {
                    if (!string.IsNullOrEmpty(currentTitle))
                    {
                        GeneratedTopics.Add(new VideoTopic
                        {
                            Index = index++,
                            Title = currentTitle,
                            Description = currentDesc
                        });
                    }
                    currentTitle = trimmed.Substring(trimmed.IndexOf('.') + 1).Trim();
                    currentDesc = "";
                }
                else if (trimmed.StartsWith("คำอธิบาย") || trimmed.Contains(":"))
                {
                    var colonIndex = trimmed.IndexOf(':');
                    if (colonIndex >= 0)
                    {
                        currentDesc = trimmed.Substring(colonIndex + 1).Trim();
                    }
                }
            }

            // Add last topic
            if (!string.IsNullOrEmpty(currentTitle))
            {
                GeneratedTopics.Add(new VideoTopic
                {
                    Index = index,
                    Title = currentTitle,
                    Description = currentDesc
                });
            }

            StatusMessage = $"สร้างหัวข้อสำเร็จ {GeneratedTopics.Count} หัวข้อ";
            CurrentProgress = 100;
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

    // Helper to get topic title (from selected topic or manual input)
    private string GetTopicTitle()
    {
        if (SelectedTopic != null)
            return SelectedTopic.Title;
        if (!string.IsNullOrWhiteSpace(TopicSubject))
            return TopicSubject;
        return "";
    }

    // Step 2: Generate Script
    [RelayCommand]
    private async Task GenerateScriptAsync(object partNumberObj)
    {
        var partNumber = Convert.ToInt32(partNumberObj);
        var topicTitle = GetTopicTitle();

        if (string.IsNullOrWhiteSpace(topicTitle))
        {
            MessageBox.Show("กรุณาใส่หัวข้อหรือเลือกหัวข้อก่อน", "แจ้งเตือน", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            IsProcessing = true;
            StatusMessage = $"กำลังสร้างบทส่วนที่ {partNumber}...";
            CurrentProgress = 0;
            _processingCts?.Cancel();
            _processingCts = new CancellationTokenSource();

            // Collect previous parts as context
            var previousParts = new List<string>();
            for (int i = 0; i < partNumber - 1; i++)
            {
                previousParts.Add(ScriptParts[i]);
            }

            var prompt = PromptTemplates.GetScriptGenerationPrompt(topicTitle, partNumber, previousParts);
            var progress = new Progress<int>(p => CurrentProgress = p);

            var response = await _openRouterService.GenerateTextAsync(
                prompt,
                _settings.ScriptGenerationModel,
                progress,
                _processingCts.Token);

            ScriptParts[partNumber - 1] = response;
            OnPropertyChanged(nameof(ScriptParts));

            // Save script as text file
            await SaveScriptToFileAsync(partNumber, response, topicTitle);

            StatusMessage = $"สร้างบทส่วนที่ {partNumber} สำเร็จ";
            CurrentProgress = 100;
            SaveProjectState();
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

    private async Task SaveScriptToFileAsync(int partNumber, string content, string topicTitle)
    {
        try
        {
            var outputFolder = GetOutputFolder();
            Directory.CreateDirectory(outputFolder);

            // Save script text file
            var scriptPath = Path.Combine(outputFolder, $"บท{partNumber}.txt");
            await File.WriteAllTextAsync(scriptPath, content);

            // Save topic title text file (for reference when folder name is sanitized)
            var topicPath = Path.Combine(outputFolder, "หัวข้อเรื่อง.txt");
            if (!File.Exists(topicPath))
            {
                await File.WriteAllTextAsync(topicPath, $"หัวข้อ: {topicTitle}\nEpisode: {CurrentProject.EpisodeNumber}\nวันที่สร้าง: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            }
        }
        catch { /* Ignore file save errors */ }
    }

    [RelayCommand]
    private async Task GenerateAllScriptsAsync()
    {
        var topicTitle = GetTopicTitle();

        if (string.IsNullOrWhiteSpace(topicTitle))
        {
            MessageBox.Show("กรุณาใส่หัวข้อหรือเลือกหัวข้อก่อน", "แจ้งเตือน", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            IsProcessing = true;
            _processingCts?.Cancel();
            _processingCts = new CancellationTokenSource();
            for (int i = 1; i <= 3; i++)
            {
                StatusMessage = $"กำลังสร้างบทส่วนที่ {i}/3...";
                CurrentProgress = (i - 1) * 33;

                // Collect previous parts as context
                var previousParts = new List<string>();
                for (int j = 0; j < i - 1; j++)
                {
                    previousParts.Add(ScriptParts[j]);
                }

                var prompt = PromptTemplates.GetScriptGenerationPrompt(topicTitle, i, previousParts);
                var response = await _openRouterService.GenerateTextAsync(
                    prompt,
                    _settings.ScriptGenerationModel,
                    null,
                    _processingCts.Token);

                ScriptParts[i - 1] = response;
                OnPropertyChanged(nameof(ScriptParts));

                // Save script as text file
                await SaveScriptToFileAsync(i, response, topicTitle);
            }
            StatusMessage = "สร้างบททั้งหมดสำเร็จ";
            CurrentProgress = 100;
            SaveProjectState();
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

    // Step 3: Generate Image Prompt
    [RelayCommand]
    private async Task GenerateImagePromptAsync()
    {
        var topicTitle = GetTopicTitle();

        if (string.IsNullOrWhiteSpace(topicTitle))
        {
            MessageBox.Show("กรุณาใส่หัวข้อหรือเลือกหัวข้อก่อน", "แจ้งเตือน", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            IsProcessing = true;
            StatusMessage = "กำลังสร้าง Prompt สำหรับรูปปก...";
            CurrentProgress = 0;
            _processingCts?.Cancel();
            _processingCts = new CancellationTokenSource();

            var prompt = PromptTemplates.GetImagePromptGenerationPrompt(topicTitle);
            var progress = new Progress<int>(p => CurrentProgress = p);

            CoverImagePrompt = await _openRouterService.GenerateTextAsync(
                prompt,
                _settings.ImagePromptModel,
                progress,
                _processingCts.Token);

            StatusMessage = "สร้าง Prompt สำหรับรูปปกสำเร็จ";
            CurrentProgress = 100;
            SaveProjectState();
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
    private void BrowseCoverImage()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Image files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|All files (*.*)|*.*",
            Title = "เลือกรูปปก"
        };

        if (dialog.ShowDialog() == true)
        {
            CoverImagePath = MultiImageViewModel.CompressImageIfNeeded(dialog.FileName, GetOutputFolder());
            StatusMessage = "เลือกรูปปกแล้ว";
            SaveProjectState();
            UpdateStepCompletionStates();
        }
    }

    [RelayCommand]
    private void CopyPromptToClipboard()
    {
        if (!string.IsNullOrEmpty(CoverImagePrompt))
        {
            Clipboard.SetText(CoverImagePrompt);
            StatusMessage = "คัดลอก Prompt แล้ว";
            SnackbarQueue.Enqueue("คัดลอก Prompt แล้ว!");
        }
    }

    // Generate Cover Image using AI
    [RelayCommand]
    private async Task GenerateCoverImageAsync()
    {
        var topicTitle = GetTopicTitle();

        if (string.IsNullOrWhiteSpace(topicTitle))
        {
            MessageBox.Show("กรุณาใส่หัวข้อหรือเลือกหัวข้อก่อน", "แจ้งเตือน", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            IsProcessing = true;
            StatusMessage = "กำลังสร้างรูปปก...";
            CurrentProgress = 0;
            _processingCts?.Cancel();
            _processingCts = new CancellationTokenSource();

            var progress = new Progress<int>(p => CurrentProgress = p);

            // First generate the image prompt if not already done
            if (string.IsNullOrWhiteSpace(CoverImagePrompt))
            {
                StatusMessage = "กำลังสร้าง Prompt...";
                CoverImagePrompt = await _openRouterService.GenerateImagePromptAsync(
                    topicTitle,
                    _settings.ImagePromptModel,
                    _processingCts.Token);
            }

            StatusMessage = "กำลังสร้างรูปปก (อาจใช้เวลาสักครู่)...";

            // Use reference image if available
            string? refImagePath = null;
            if (!string.IsNullOrEmpty(_settings.ReferenceImagePath) && File.Exists(_settings.ReferenceImagePath))
            {
                refImagePath = _settings.ReferenceImagePath;
            }

            var imageBytes = await _openRouterService.GenerateImageAsync(
                CoverImagePrompt,
                _settings.ImageGenerationModel,
                refImagePath,
                progress,
                _processingCts.Token,
                topicTitle);

            // Save the generated image
            var outputFolder = GetOutputFolder();
            Directory.CreateDirectory(outputFolder);

            var imagePath = Path.Combine(outputFolder, $"ปกไทย_ep{CurrentProject.EpisodeNumber}.png");
            await File.WriteAllBytesAsync(imagePath, imageBytes);

            CoverImagePath = imagePath;
            StatusMessage = "สร้างรูปปกสำเร็จ";
            CurrentProgress = 100;
            SaveProjectState();
        }
        catch (OperationCanceledException) { return; }
        catch (Exception ex)
        {
            StatusMessage = $"เกิดข้อผิดพลาด: {ex.Message}";
            MessageBox.Show($"ไม่สามารถสร้างรูปได้: {ex.Message}\n\nลองใช้ปุ่ม Browse เลือกรูปแทน", "ข้อผิดพลาด", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private void BrowseReferenceImage()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Image files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|All files (*.*)|*.*",
            Title = "เลือกรูปอ้างอิงสำหรับ Style"
        };

        if (dialog.ShowDialog() == true)
        {
            _settings.ReferenceImagePath = dialog.FileName;
            OnPropertyChanged(nameof(ProjectSettings));
            StatusMessage = "เลือกรูปอ้างอิงแล้ว";
        }
    }

    // Run All Flow - Automate entire workflow
    [RelayCommand]
    private async Task RunAllFlowAsync()
    {
        var topicTitle = GetTopicTitle();

        if (string.IsNullOrWhiteSpace(topicTitle))
        {
            MessageBox.Show("กรุณาใส่หัวข้อหรือเลือกหัวข้อก่อน", "แจ้งเตือน", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = MessageBox.Show(
            $"ต้องการรันทั้งหมดอัตโนมัติหรือไม่?\n\nหัวข้อ: {topicTitle}\n\nขั้นตอน:\n1. สร้างบทพูด 3 ส่วน\n2. สร้างรูปปก\n3. สร้างเสียง 3 ไฟล์\n4. สร้างวิดีโอ\n\n(ใช้เวลาประมาณ 10-15 นาที)",
            "ยืนยันการรันทั้งหมด",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            IsProcessing = true;
            _processingCts?.Cancel();
            _processingCts = new CancellationTokenSource();
            var totalSteps = 8; // 3 scripts + 1 image + 3 audio + 1 video
            var currentStep = 0;

            // Step 1-3: Generate Scripts
            for (int i = 1; i <= 3; i++)
            {
                _processingCts.Token.ThrowIfCancellationRequested();
                currentStep++;

                // Skip if script already exists for this part
                if (!string.IsNullOrWhiteSpace(ScriptParts[i - 1]))
                {
                    StatusMessage = $"[{currentStep}/{totalSteps}] บทส่วนที่ {i} มีอยู่แล้ว - ข้าม";
                    CurrentProgress = (currentStep * 100) / totalSteps;
                    continue;
                }

                StatusMessage = $"[{currentStep}/{totalSteps}] กำลังสร้างบทส่วนที่ {i}/3...";
                CurrentProgress = (currentStep * 100) / totalSteps;

                var previousParts = new List<string>();
                for (int j = 0; j < i - 1; j++)
                {
                    previousParts.Add(ScriptParts[j]);
                }

                var prompt = PromptTemplates.GetScriptGenerationPrompt(topicTitle, i, previousParts);
                var response = await _openRouterService.GenerateTextAsync(
                    prompt,
                    _settings.ScriptGenerationModel,
                    null,
                    _processingCts.Token);

                ScriptParts[i - 1] = response;
                OnPropertyChanged(nameof(ScriptParts));

                // Save script as text file
                await SaveScriptToFileAsync(i, response, topicTitle);
                SaveProjectState();
            }

            // Step 4: Generate Cover Image
            currentStep++;
            CurrentProgress = (currentStep * 100) / totalSteps;

            _processingCts.Token.ThrowIfCancellationRequested();
            // Skip if cover image already exists
            if (!string.IsNullOrWhiteSpace(CoverImagePath) && File.Exists(CoverImagePath))
            {
                StatusMessage = $"[{currentStep}/{totalSteps}] รูปปกมีอยู่แล้ว - ข้าม";
            }
            else
            {
                StatusMessage = $"[{currentStep}/{totalSteps}] กำลังสร้างรูปปก...";

                // Generate image prompt first
                if (string.IsNullOrWhiteSpace(CoverImagePrompt))
                {
                    CoverImagePrompt = await _openRouterService.GenerateImagePromptAsync(
                        topicTitle,
                        _settings.ImagePromptModel,
                        _processingCts.Token);
                }

                try
                {
                    string? refImagePath = null;
                    if (!string.IsNullOrEmpty(_settings.ReferenceImagePath) && File.Exists(_settings.ReferenceImagePath))
                    {
                        refImagePath = _settings.ReferenceImagePath;
                    }

                    var imageBytes = await _openRouterService.GenerateImageAsync(
                        CoverImagePrompt,
                        _settings.ImageGenerationModel,
                        refImagePath,
                        null,
                        _processingCts.Token,
                        topicTitle);

                    var outputFolder = GetOutputFolder();
                    Directory.CreateDirectory(outputFolder);

                    var imagePath = Path.Combine(outputFolder, $"ปกไทย_ep{CurrentProject.EpisodeNumber}.png");
                    await File.WriteAllBytesAsync(imagePath, imageBytes);
                    CoverImagePath = imagePath;
                }
                catch (Exception imgEx)
                {
                    // Image generation failed, continue with manual selection later
                    StatusMessage = $"สร้างรูปปกไม่สำเร็จ: {imgEx.Message} - กรุณาเลือกรูปด้วยตนเอง";
                    MessageBox.Show($"ไม่สามารถสร้างรูปอัตโนมัติได้: {imgEx.Message}\n\nกรุณาเลือกรูปปกด้วยตนเองหลังจากขั้นตอนอื่นเสร็จ",
                        "คำเตือน", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }

            SaveProjectState();

            // Step 5-7: Generate Audio
            var audioOutputFolder = GetOutputFolder();
            Directory.CreateDirectory(audioOutputFolder);

            for (int i = 1; i <= 3; i++)
            {
                _processingCts.Token.ThrowIfCancellationRequested();
                currentStep++;

                // Skip if audio already exists for this part
                if (!string.IsNullOrWhiteSpace(AudioPaths[i - 1]) && File.Exists(AudioPaths[i - 1]))
                {
                    StatusMessage = $"[{currentStep}/{totalSteps}] เสียงส่วนที่ {i} มีอยู่แล้ว - ข้าม";
                    CurrentProgress = (currentStep * 100) / totalSteps;
                    continue;
                }

                var partIndex = i;
                var startTime = DateTime.Now;
                var cts = CancellationTokenSource.CreateLinkedTokenSource(_processingCts.Token);
                try
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            while (!cts.Token.IsCancellationRequested)
                            {
                                var elapsed = DateTime.Now - startTime;
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    StatusMessage = $"[{currentStep}/{totalSteps}] กำลังสร้างเสียงส่วนที่ {partIndex}/3... ({elapsed:mm\\:ss})";
                                });
                                await Task.Delay(1000, cts.Token).ConfigureAwait(false);
                            }
                        }
                        catch (Exception) { /* Expected when cancelled or disposed */ }
                    }, cts.Token);

                    var audioBytes = await _googleTtsService.GenerateAudioAsync(
                        ScriptParts[partIndex - 1],
                        _settings.TtsVoice,
                        null,
                        _processingCts.Token);

                    cts.Cancel();

                    var audioPath = Path.Combine(audioOutputFolder, $"ep{CurrentProject.EpisodeNumber}_{partIndex}.wav");
                    await File.WriteAllBytesAsync(audioPath, audioBytes);

                    AudioPaths[partIndex - 1] = audioPath;
                    OnPropertyChanged(nameof(AudioPaths));
                    CurrentProgress = (currentStep * 100) / totalSteps;
                    SaveProjectState();
                }
                finally
                {
                    cts.Cancel();
                    cts.Dispose();
                }
            }

            _processingCts.Token.ThrowIfCancellationRequested();
            // Step 8: Create Video (only if cover image exists)
            if (!string.IsNullOrWhiteSpace(CoverImagePath) && File.Exists(CoverImagePath))
            {
                currentStep++;
                StatusMessage = $"[{currentStep}/{totalSteps}] กำลังสร้างวิดีโอ...";
                CurrentProgress = (currentStep * 100) / totalSteps;

                var audioFiles = AudioPaths.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
                var videoPath = Path.Combine(audioOutputFolder, $"EP{CurrentProject.EpisodeNumber}_Thai.mp4");

                FinalVideoPath = await _ffmpegService.CreateVideoFromImageAndAudioAsync(
                    CoverImagePath,
                    audioFiles,
                    videoPath,
                    null,
                    _processingCts.Token);

                SaveProjectState();
            }
            else
            {
                MessageBox.Show("ข้ามการสร้างวิดีโอเนื่องจากไม่มีรูปปก\nกรุณาเลือกรูปปกแล้วกดสร้างวิดีโอด้วยตนเอง",
                    "แจ้งเตือน", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            CurrentProgress = 100;
            StatusMessage = "รันทั้งหมดเสร็จสิ้น!";

            MessageBox.Show("สร้างวิดีโอเสร็จสมบูรณ์!", "สำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);

            // Open output folder
            var finalFolder = GetOutputFolder();
            if (Directory.Exists(finalFolder))
            {
                System.Diagnostics.Process.Start("explorer.exe", finalFolder);
            }
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
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }
    }

    // Step 4: Generate Audio
    [RelayCommand]
    private async Task GenerateAudioAsync(object partNumberObj)
    {
        var partNumber = Convert.ToInt32(partNumberObj);

        if (string.IsNullOrWhiteSpace(ScriptParts[partNumber - 1]))
        {
            MessageBox.Show($"กรุณาสร้างบทส่วนที่ {partNumber} ก่อน", "แจ้งเตือน", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _processingCts?.Cancel();
        _processingCts = new CancellationTokenSource();
        var cts = CancellationTokenSource.CreateLinkedTokenSource(_processingCts.Token);
        try
        {
            IsProcessing = true;
            CurrentProgress = 0;

            // Start elapsed time timer
            var startTime = DateTime.Now;
            _ = Task.Run(async () =>
            {
                try
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        var elapsed = DateTime.Now - startTime;
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            StatusMessage = $"กำลังสร้างเสียงส่วนที่ {partNumber}... ({elapsed:mm\\:ss})";
                        });
                        await Task.Delay(1000, cts.Token).ConfigureAwait(false);
                    }
                }
                catch (Exception) { /* Expected when cancelled or disposed */ }
            }, cts.Token);

            var progress = new Progress<int>(p => CurrentProgress = p);

            var audioBytes = await _googleTtsService.GenerateAudioAsync(
                ScriptParts[partNumber - 1],
                _settings.TtsVoice,
                progress,
                _processingCts.Token);

            cts.Cancel(); // Stop the timer

            // Save to temp or output folder
            var outputFolder = GetOutputFolder();
            Directory.CreateDirectory(outputFolder);

            var audioPath = Path.Combine(outputFolder, $"ep{CurrentProject.EpisodeNumber}_{partNumber}.wav");
            await File.WriteAllBytesAsync(audioPath, audioBytes);

            AudioPaths[partNumber - 1] = audioPath;
            OnPropertyChanged(nameof(AudioPaths));

            StatusMessage = $"สร้างเสียงส่วนที่ {partNumber} สำเร็จ";
            CurrentProgress = 100;
            SaveProjectState();
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
            cts.Dispose();
            IsProcessing = false;
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }
    }

    [RelayCommand]
    private async Task GenerateAllAudioAsync()
    {
        // Check if all scripts are ready
        for (int i = 0; i < 3; i++)
        {
            if (string.IsNullOrWhiteSpace(ScriptParts[i]))
            {
                MessageBox.Show($"กรุณาสร้างบทส่วนที่ {i + 1} ก่อน", "แจ้งเตือน", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        try
        {
            IsProcessing = true;
            _processingCts?.Cancel();
            _processingCts = new CancellationTokenSource();
            var outputFolder = GetOutputFolder();
            Directory.CreateDirectory(outputFolder);

            for (int i = 1; i <= 3; i++)
            {
                _processingCts.Token.ThrowIfCancellationRequested();
                CurrentProgress = (i - 1) * 33;

                // Skip if audio already exists for this part
                if (!string.IsNullOrWhiteSpace(AudioPaths[i - 1]) && File.Exists(AudioPaths[i - 1]))
                {
                    StatusMessage = $"เสียงส่วนที่ {i} มีอยู่แล้ว - ข้าม";
                    continue;
                }

                // Start elapsed time timer for this part
                var partIndex = i;
                var startTime = DateTime.Now;
                var cts = CancellationTokenSource.CreateLinkedTokenSource(_processingCts.Token);
                try
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            while (!cts.Token.IsCancellationRequested)
                            {
                                var elapsed = DateTime.Now - startTime;
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    StatusMessage = $"กำลังสร้างเสียงส่วนที่ {partIndex}/3... ({elapsed:mm\\:ss})";
                                });
                                await Task.Delay(1000, cts.Token).ConfigureAwait(false);
                            }
                        }
                        catch (Exception) { /* Expected when cancelled or disposed */ }
                    }, cts.Token);

                    var audioBytes = await _googleTtsService.GenerateAudioAsync(
                        ScriptParts[partIndex - 1],
                        _settings.TtsVoice,
                        null,
                        _processingCts.Token);

                    cts.Cancel(); // Stop the timer

                    var audioPath = Path.Combine(outputFolder, $"ep{CurrentProject.EpisodeNumber}_{partIndex}.wav");
                    await File.WriteAllBytesAsync(audioPath, audioBytes);

                    AudioPaths[partIndex - 1] = audioPath;
                    OnPropertyChanged(nameof(AudioPaths));
                }
                finally
                {
                    cts.Cancel();
                    cts.Dispose();
                }
            }
            StatusMessage = "สร้างเสียงทั้งหมดสำเร็จ";
            CurrentProgress = 100;
            SaveProjectState();
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
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }
    }

    // Auto: Generate all audio (with retry) + Create Video
    [RelayCommand]
    private async Task GenerateAudioAndCreateVideoAsync()
    {
        // Validate scripts
        for (int i = 0; i < 3; i++)
        {
            if (string.IsNullOrWhiteSpace(ScriptParts[i]))
            {
                MessageBox.Show($"กรุณาสร้างบทส่วนที่ {i + 1} ก่อน", "แจ้งเตือน", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        if (string.IsNullOrWhiteSpace(CoverImagePath) || !File.Exists(CoverImagePath))
        {
            MessageBox.Show("กรุณาเลือกรูปปกก่อน (Step 3)", "แจ้งเตือน", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        const int maxRetries = 3;
        try
        {
            IsProcessing = true;
            CurrentProgress = 0;
            _processingCts?.Cancel();
            _processingCts = new CancellationTokenSource();
            var outputFolder = GetOutputFolder();
            Directory.CreateDirectory(outputFolder);

            // --- Phase 1: Generate Audio (with retry) ---
            for (int i = 1; i <= 3; i++)
            {
                _processingCts.Token.ThrowIfCancellationRequested();
                // Skip if audio already exists
                if (!string.IsNullOrWhiteSpace(AudioPaths[i - 1]) && File.Exists(AudioPaths[i - 1]))
                {
                    StatusMessage = $"เสียงส่วนที่ {i} มีอยู่แล้ว - ข้าม";
                    continue;
                }

                bool success = false;
                for (int attempt = 1; attempt <= maxRetries; attempt++)
                {
                    var startTime = DateTime.Now;
                    var cts = CancellationTokenSource.CreateLinkedTokenSource(_processingCts.Token);
                    try
                    {
                        var partIndex = i;
                        var attemptNum = attempt;
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                while (!cts.Token.IsCancellationRequested)
                                {
                                    var elapsed = DateTime.Now - startTime;
                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        var retryText = attemptNum > 1 ? $" (ครั้งที่ {attemptNum}/{maxRetries})" : "";
                                        StatusMessage = $"กำลังสร้างเสียงส่วนที่ {partIndex}/3{retryText}... ({elapsed:mm\\:ss})";
                                    });
                                    await Task.Delay(1000, cts.Token).ConfigureAwait(false);
                                }
                            }
                            catch (Exception) { }
                        }, cts.Token);

                        CurrentProgress = ((i - 1) * 25);

                        var audioBytes = await _googleTtsService.GenerateAudioAsync(
                            ScriptParts[i - 1],
                            _settings.TtsVoice,
                            null,
                            _processingCts.Token);

                        cts.Cancel();

                        var audioPath = Path.Combine(outputFolder, $"ep{CurrentProject.EpisodeNumber}_{i}.wav");
                        await File.WriteAllBytesAsync(audioPath, audioBytes);

                        AudioPaths[i - 1] = audioPath;
                        OnPropertyChanged(nameof(AudioPaths));
                        SaveProjectState();

                        success = true;
                        break; // Success, no more retries
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        cts.Cancel();
                        if (attempt < maxRetries)
                        {
                            StatusMessage = $"เสียงส่วนที่ {i} ล้มเหลว (ครั้งที่ {attempt}/{maxRetries}): {ex.Message} - กำลังลองใหม่...";
                            await Task.Delay(2000); // Wait before retry
                        }
                        else
                        {
                            StatusMessage = $"เสียงส่วนที่ {i} ล้มเหลวทั้ง {maxRetries} ครั้ง: {ex.Message}";
                        }
                    }
                    finally
                    {
                        cts.Cancel();
                        cts.Dispose();
                    }
                }

                if (!success)
                {
                    MessageBox.Show($"สร้างเสียงส่วนที่ {i} ไม่สำเร็จหลังลอง {maxRetries} ครั้ง\nกรุณาลองใหม่หรือเลือกไฟล์เสียงด้วยตนเอง",
                        "ข้อผิดพลาด", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            // --- Phase 2: Create Video ---
            _processingCts.Token.ThrowIfCancellationRequested();
            StatusMessage = "กำลังสร้างวิดีโอ...";
            CurrentProgress = 75;

            var audioFiles = AudioPaths.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
            var videoPath = Path.Combine(outputFolder, $"EP{CurrentProject.EpisodeNumber}_Thai.mp4");
            var progress = new Progress<int>(p => CurrentProgress = 75 + (p / 4)); // 75-100%

            FinalVideoPath = await _ffmpegService.CreateVideoFromImageAndAudioAsync(
                CoverImagePath,
                audioFiles,
                videoPath,
                progress,
                _processingCts.Token);

            SaveProjectState();

            CurrentProgress = 100;
            StatusMessage = "สร้างเสียง + วิดีโอ สำเร็จ!";

            // Auto navigate to video step
            CurrentStepIndex = 4;

            MessageBox.Show("สร้างเสียงและวิดีโอเสร็จสมบูรณ์!", "สำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);

            // Open output folder
            if (Directory.Exists(outputFolder))
            {
                System.Diagnostics.Process.Start("explorer.exe", outputFolder);
            }
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
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }
    }

    // Step 5: Create Video
    [RelayCommand]
    private async Task CreateVideoAsync()
    {
        if (string.IsNullOrWhiteSpace(CoverImagePath))
        {
            MessageBox.Show("กรุณาเลือกรูปปกก่อน", "แจ้งเตือน", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var audioFiles = AudioPaths.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        if (audioFiles.Count == 0)
        {
            MessageBox.Show("กรุณาสร้างไฟล์เสียงก่อน", "แจ้งเตือน", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            IsProcessing = true;
            StatusMessage = "กำลังสร้างวิดีโอ...";
            CurrentProgress = 0;
            _processingCts?.Cancel();
            _processingCts = new CancellationTokenSource();

            var outputFolder = GetOutputFolder();
            Directory.CreateDirectory(outputFolder);

            var videoPath = Path.Combine(outputFolder, $"EP{CurrentProject.EpisodeNumber}_Thai.mp4");
            var progress = new Progress<int>(p => CurrentProgress = p);

            FinalVideoPath = await _ffmpegService.CreateVideoFromImageAndAudioAsync(
                CoverImagePath,
                audioFiles,
                videoPath,
                progress,
                _processingCts.Token);

            StatusMessage = "สร้างวิดีโอสำเร็จ";
            CurrentProgress = 100;
            SaveProjectState();
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

    // Step 6: Save All
    [RelayCommand]
    private async Task SaveAllAsync()
    {
        try
        {
            IsProcessing = true;
            StatusMessage = "กำลังบันทึกไฟล์ทั้งหมด...";
            CurrentProgress = 0;

            var outputFolder = GetOutputFolder();
            Directory.CreateDirectory(outputFolder);

            // Save scripts as DOCX
            for (int i = 0; i < ScriptParts.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(ScriptParts[i]))
                {
                    var docxPath = Path.Combine(outputFolder, $"บท{i + 1}.docx");
                    await _documentService.SaveAsDocxAsync(ScriptParts[i], docxPath);
                }
                CurrentProgress = (i + 1) * 20;
            }

            // Copy cover image if exists
            if (!string.IsNullOrWhiteSpace(CoverImagePath) && File.Exists(CoverImagePath))
            {
                var destPath = Path.Combine(outputFolder, "ปกไทย" + Path.GetExtension(CoverImagePath));
                File.Copy(CoverImagePath, destPath, true);
            }

            CurrentProgress = 100;
            StatusMessage = $"บันทึกไฟล์ทั้งหมดที่ {outputFolder}";

            // Open folder
            System.Diagnostics.Process.Start("explorer.exe", outputFolder);
        }
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

    // Step 7: YouTube Upload Info
    [RelayCommand]
    private void GenerateYoutubeInfo()
    {
        var topicTitle = GetTopicTitle();
        if (string.IsNullOrWhiteSpace(topicTitle))
        {
            MessageBox.Show("กรุณาใส่หัวข้อก่อน", "แจ้งเตือน", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        YoutubeTitle = topicTitle;

        // Base hashtags
        var baseTags = "#เรื่องแปลกๆ #เรื่องแปลกแต่จริง #เรื่องแปลก #เรื่องแปลกน่ารู้ #วิทยาศาสตร์ #สารคดี #สารคดีวิทยาศาสตร์ #sciencepodcast #ความรู้รอบตัว #เรียนรู้รอบตัว";

        // Extract keywords from topic for extra tags
        var keywords = topicTitle
            .Replace("ทำไม", "").Replace("?", "").Replace("ถึง", "")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 1)
            .Select(w => $"#{w.Trim()}")
            .ToList();

        var extraTags = string.Join(" ", keywords);

        YoutubeTags = string.IsNullOrWhiteSpace(extraTags)
            ? baseTags
            : $"{extraTags} {baseTags}";

        YoutubeDescription = $"{topicTitle}\n\n{YoutubeTags}";

        StatusMessage = "สร้างข้อมูล YouTube สำเร็จ";
    }

    [RelayCommand]
    private void CopyYoutubeTitle()
    {
        if (!string.IsNullOrEmpty(YoutubeTitle))
        {
            Clipboard.SetText(YoutubeTitle);
            StatusMessage = "คัดลอกชื่อวิดีโอแล้ว";
            SnackbarQueue.Enqueue("คัดลอกชื่อวิดีโอแล้ว!");
        }
    }

    [RelayCommand]
    private void CopyYoutubeDescription()
    {
        if (!string.IsNullOrEmpty(YoutubeDescription))
        {
            Clipboard.SetText(YoutubeDescription);
            StatusMessage = "คัดลอกคำอธิบายแล้ว";
            SnackbarQueue.Enqueue("คัดลอกคำอธิบายแล้ว!");
        }
    }

    [RelayCommand]
    private void CopyYoutubeTags()
    {
        if (!string.IsNullOrEmpty(YoutubeTags))
        {
            Clipboard.SetText(YoutubeTags);
            StatusMessage = "คัดลอก Tags แล้ว";
            SnackbarQueue.Enqueue("คัดลอก Tags แล้ว!");
        }
    }

    // Open Multi-Image Window
    [RelayCommand]
    private void OpenMultiImageWindow()
    {
        var window = App.Services.GetRequiredService<MultiImageWindow>();
        var vm = App.Services.GetRequiredService<MultiImageViewModel>();

        // Import topic from main window if available
        var topic = GetTopicTitle();
        vm.Initialize(topic, CurrentProject.EpisodeNumber, CoverImagePath);

        window.DataContext = vm;
        window.Owner = Application.Current.MainWindow;
        window.Show();
    }

    // Feature 2: Cancel Processing
    [RelayCommand]
    private void CancelProcessing()
    {
        _processingCts?.Cancel();
        StatusMessage = "ยกเลิกการทำงาน";
        CurrentProgress = 0;
    }

    // Feature 3: Audio Playback
    [RelayCommand]
    private void ToggleAudioPlayback(object indexObj)
    {
        var index = Convert.ToInt32(indexObj);

        if (PlayingAudioIndex == index)
        {
            StopAudioPlayback();
            return;
        }

        StopAudioPlayback();

        var path = AudioPaths[index];
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
            player.PlaybackStopped += (s, e) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (PlayingAudioIndex == index) PlayingAudioIndex = -1;
                });
                reader.Dispose();
                player.Dispose();
            };
            player.Play();
            PlayingAudioIndex = index;
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

    // Feature 8: Open Output Folder
    [RelayCommand]
    private void OpenOutputFolder()
    {
        var folder = GetOutputFolder();
        if (Directory.Exists(folder))
        {
            System.Diagnostics.Process.Start("explorer.exe", folder);
        }
        else if (Directory.Exists(_settings.OutputBasePath))
        {
            System.Diagnostics.Process.Start("explorer.exe", _settings.OutputBasePath);
        }
        else
        {
            StatusMessage = "ยังไม่มีโฟลเดอร์ Output";
        }
    }

    // Feature 10: Update audio duration estimate
    private void UpdateEstimatedDuration()
    {
        try
        {
            var total = TimeSpan.Zero;
            var parts = new List<string>();

            for (int i = 0; i < AudioPaths.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(AudioPaths[i]) && File.Exists(AudioPaths[i]))
                {
                    var duration = _ffmpegService.GetAudioDuration(AudioPaths[i]);
                    total += duration;
                    parts.Add($"Part {i + 1}: {duration:mm\\:ss}");
                }
            }

            EstimatedDuration = total > TimeSpan.Zero
                ? $"ความยาวรวม: {total:mm\\:ss} ({string.Join(", ", parts)})"
                : "";
        }
        catch (Exception)
        {
            EstimatedDuration = "ไม่สามารถคำนวณความยาวได้";
        }
    }

    // Feature 1: Update step completion states
    private void UpdateStepCompletionStates()
    {
        StepCompleted[0] = !string.IsNullOrWhiteSpace(TopicSubject) || SelectedTopic != null;
        StepCompleted[1] = ScriptParts.All(s => !string.IsNullOrWhiteSpace(s));
        StepCompleted[2] = !string.IsNullOrWhiteSpace(CoverImagePath) && File.Exists(CoverImagePath);
        StepCompleted[3] = AudioPaths.All(a => !string.IsNullOrWhiteSpace(a) && File.Exists(a));
        StepCompleted[4] = !string.IsNullOrWhiteSpace(FinalVideoPath);
        StepCompleted[6] = !string.IsNullOrWhiteSpace(YoutubeTitle);
        OnPropertyChanged(nameof(StepCompleted));
    }

    // Feature 4 + 10: Auto-actions on step change
    partial void OnCurrentStepIndexChanged(int value)
    {
        if (value == 4)
            UpdateEstimatedDuration();

        if (value == 6 && string.IsNullOrWhiteSpace(YoutubeTitle) && !string.IsNullOrWhiteSpace(GetTopicTitle()))
            GenerateYoutubeInfo();

        UpdateStepCompletionStates();
    }

    // Update step bar when processing finishes
    partial void OnIsProcessingChanged(bool value)
    {
        if (!value)
            UpdateStepCompletionStates();
    }

    // Navigation
    [RelayCommand]
    private void NextStep()
    {
        if (CurrentStepIndex < 6)
        {
            CurrentStepIndex++;
        }
    }

    [RelayCommand]
    private void PreviousStep()
    {
        if (CurrentStepIndex > 0)
        {
            CurrentStepIndex--;
        }
    }

    [RelayCommand]
    private void GoToStep(object stepObj)
    {
        var step = Convert.ToInt32(stepObj);
        if (step >= 0 && step <= 6)
        {
            CurrentStepIndex = step;
        }
    }

    // Settings
    [RelayCommand]
    private void ToggleSettings()
    {
        ShowSettings = !ShowSettings;
    }

    [RelayCommand]
    private void SaveSettings()
    {
        _settings.Save();
        StatusMessage = "บันทึกการตั้งค่าแล้ว";
        ShowSettings = false;
    }

    [RelayCommand]
    private async Task TestOpenRouterAsync()
    {
        try
        {
            IsProcessing = true;
            StatusMessage = "กำลังทดสอบ OpenRouter API...";

            var result = await _openRouterService.TestConnectionAsync(_settings.OpenRouterApiKey);
            StatusMessage = result ? "OpenRouter API ทำงานปกติ" : "OpenRouter API ใช้งานไม่ได้";
            MessageBox.Show(result ? "เชื่อมต่อสำเร็จ!" : "ไม่สามารถเชื่อมต่อได้ กรุณาตรวจสอบ API Key",
                "ผลการทดสอบ", MessageBoxButton.OK,
                result ? MessageBoxImage.Information : MessageBoxImage.Error);
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private async Task TestGoogleTtsAsync()
    {
        try
        {
            IsProcessing = true;
            StatusMessage = "กำลังทดสอบ Google TTS API...";

            var result = await _googleTtsService.TestConnectionAsync(_settings.GoogleApiKey);
            StatusMessage = result ? "Google TTS API ทำงานปกติ" : "Google TTS API ใช้งานไม่ได้";
            MessageBox.Show(result ? "เชื่อมต่อสำเร็จ!" : "ไม่สามารถเชื่อมต่อได้ กรุณาตรวจสอบ API Key",
                "ผลการทดสอบ", MessageBoxButton.OK,
                result ? MessageBoxImage.Information : MessageBoxImage.Error);
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private async Task TestFfmpegAsync()
    {
        try
        {
            IsProcessing = true;
            StatusMessage = "กำลังทดสอบ FFmpeg...";

            var result = await _ffmpegService.TestFfmpegAsync(_settings.FfmpegPath);
            StatusMessage = result ? "FFmpeg ทำงานปกติ" : "FFmpeg ใช้งานไม่ได้";
            MessageBox.Show(result ? "FFmpeg พร้อมใช้งาน!" : "ไม่พบ FFmpeg กรุณาตรวจสอบ path",
                "ผลการทดสอบ", MessageBoxButton.OK,
                result ? MessageBoxImage.Information : MessageBoxImage.Error);
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private async Task TestNvencAsync()
    {
        try
        {
            IsProcessing = true;
            StatusMessage = "กำลังทดสอบ GPU Encoding (NVENC)...";

            var result = await _ffmpegService.TestNvencAsync();
            StatusMessage = result ? "NVENC พร้อมใช้งาน" : "NVENC ไม่พร้อมใช้งาน";

            if (result)
            {
                MessageBox.Show("GPU Encoding (NVENC) พร้อมใช้งาน!\n\nคุณสามารถเปิดใช้ 'ใช้ GPU Encoding' ได้",
                    "ผลการทดสอบ", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("GPU Encoding (NVENC) ไม่พร้อมใช้งาน\n\nอาจเป็นเพราะ:\n- ไม่มี NVIDIA GPU\n- FFmpeg ไม่รองรับ NVENC\n- Driver ไม่ถูกต้อง",
                    "ผลการทดสอบ", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private void BrowseOutputFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "เลือกโฟลเดอร์สำหรับบันทึกไฟล์"
        };

        if (dialog.ShowDialog() == true)
        {
            _settings.OutputBasePath = dialog.FolderName;
            OnPropertyChanged(nameof(ProjectSettings));
        }
    }

    [RelayCommand]
    private void BrowseFfmpegPath()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "เลือกโฟลเดอร์ FFmpeg"
        };

        if (dialog.ShowDialog() == true)
        {
            _settings.FfmpegPath = dialog.FolderName;
            OnPropertyChanged(nameof(ProjectSettings));
        }
    }

    [RelayCommand]
    private void NewProject()
    {
        // Ask user to confirm if there's existing data
        var currentState = ProjectState.Load();
        if (currentState != null && currentState.HasData())
        {
            var result = MessageBox.Show(
                "คุณมีโปรเจกต์ที่ยังไม่เสร็จอยู่ ต้องการเริ่มใหม่หรือไม่?\n\n(ข้อมูลเดิมจะถูกลบ)",
                "ยืนยันการเริ่มโปรเจกต์ใหม่",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;
        }

        // Clear saved state
        ProjectState.Clear();

        CurrentProject = new VideoProject
        {
            EpisodeNumber = _settings.LastEpisodeNumber + 1
        };
        TopicSubject = "";
        GeneratedTopics.Clear();
        SelectedTopic = null;
        ScriptParts = new ObservableCollection<string> { "", "", "" };
        CoverImagePrompt = "";
        CoverImagePath = "";
        AudioPaths = new ObservableCollection<string> { "", "", "" };
        FinalVideoPath = "";
        CurrentStepIndex = 0;
        StatusMessage = "เริ่มโปรเจกต์ใหม่";
    }

    private string GetOutputFolder()
    {
        var topicName = GetTopicTitle();
        var folderName = !string.IsNullOrWhiteSpace(topicName)
            ? $"EP{CurrentProject.EpisodeNumber} {SanitizeFolderName(topicName)}"
            : $"EP{CurrentProject.EpisodeNumber}";

        return Path.Combine(_settings.OutputBasePath, folderName);
    }

    private static string SanitizeFolderName(string name)
    {
        // Remove invalid characters for Windows folder names
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Where(c => !invalidChars.Contains(c)).ToArray());

        // Also remove some additional problematic characters
        sanitized = sanitized.Replace("?", "").Replace(":", "").Replace("\"", "").Replace("*", "");

        // Trim and limit length
        sanitized = sanitized.Trim();
        if (sanitized.Length > 100)
            sanitized = sanitized.Substring(0, 100);

        return sanitized;
    }
}
