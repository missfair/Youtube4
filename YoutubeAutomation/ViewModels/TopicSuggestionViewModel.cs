using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;
using YoutubeAutomation.Models;
using YoutubeAutomation.Services.Interfaces;

namespace YoutubeAutomation.ViewModels;

public partial class TopicSuggestionViewModel : ObservableObject
{
    private readonly IVideoHistoryService _historyService;
    private readonly IOpenRouterService _openRouterService;
    private readonly ProjectSettings _settings;
    private CancellationTokenSource? _processingCts;

    [ObservableProperty] private ContentCategory selectedCategory = ContentCategoryRegistry.Animal;
    [ObservableProperty] private ObservableCollection<string> historyTopics = new();
    [ObservableProperty] private ObservableCollection<SuggestedTopic> suggestedTopics = new();
    [ObservableProperty] private bool isProcessing;
    [ObservableProperty] private string statusMessage = "พร้อมทำงาน";
    [ObservableProperty] private int currentProgress;
    [ObservableProperty] private int historyCount;
    [ObservableProperty] private bool showHistory;
    [ObservableProperty] private bool hasSuggestedTopics;

    public ObservableCollection<ContentCategory> AvailableCategories { get; }
    public SnackbarMessageQueue SnackbarQueue { get; }

    public TopicSuggestionViewModel(
        IVideoHistoryService historyService,
        IOpenRouterService openRouterService,
        ProjectSettings settings)
    {
        _historyService = historyService;
        _openRouterService = openRouterService;
        _settings = settings;

        AvailableCategories = new ObservableCollection<ContentCategory>(ContentCategoryRegistry.All);
        SnackbarQueue = new SnackbarMessageQueue(TimeSpan.FromSeconds(3));

        // Set default category from settings
        SelectedCategory = ContentCategoryRegistry.GetByKey(_settings.DefaultCategoryKey);
    }

    // Update HasSuggestedTopics when collection changes
    partial void OnSuggestedTopicsChanged(ObservableCollection<SuggestedTopic> value)
    {
        HasSuggestedTopics = value.Count > 0;
    }

    public async Task InitializeAsync()
    {
        await LoadHistoryAsync();

        // First-time user guidance: Defer to avoid blocking UI during window load
        if (HistoryCount == 0)
        {
            // Use Dispatcher to show prompt after window is fully rendered
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                // Small delay to ensure window is visible
                await Task.Delay(100);

                var result = MessageBox.Show(
                    "ยังไม่มีประวัติหัวข้อในระบบ\n\n" +
                    "ต้องการสแกนโฟลเดอร์เพื่อนำเข้าหัวข้อจากวิดีโอที่เคยทำหรือไม่?\n" +
                    $"(จะสแกนโฟลเดอร์ EP ทั้งหมดใน {Path.GetDirectoryName(_historyService.GetHistoryFilePath())})",
                    "แนะนำสำหรับการใช้งานครั้งแรก",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    await RescanFoldersAsync();
                }
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }
    }

    private async Task LoadHistoryAsync()
    {
        try
        {
            StatusMessage = "กำลังโหลดประวัติหัวข้อ...";
            var topics = await _historyService.LoadHistoryAsync();

            HistoryTopics.Clear();
            HistoryCount = topics.Count;

            // Show first 20 topics
            foreach (var topic in topics.Take(20))
            {
                HistoryTopics.Add(topic);
            }

            StatusMessage = $"โหลดประวัติแล้ว {HistoryCount} หัวข้อ";
        }
        catch (Exception ex)
        {
            StatusMessage = $"เกิดข้อผิดพลาด: {ex.Message}";
            MessageBox.Show(ex.Message, "ข้อผิดพลาด", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task GenerateSuggestionsAsync()
    {
        if (string.IsNullOrWhiteSpace(_settings.OpenRouterApiKey))
        {
            MessageBox.Show("กรุณาตั้งค่า OpenRouter API Key ก่อน\n\nเปิด Settings → OpenRouter API Key",
                            "แจ้งเตือน", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            IsProcessing = true;
            _processingCts = new CancellationTokenSource();
            StatusMessage = "กำลังสร้างหัวข้อใหม่ด้วย AI...";
            CurrentProgress = 0;

            // Load latest history
            var history = await _historyService.LoadHistoryAsync();
            CurrentProgress = 20;

            // Build AI prompt
            var prompt = BuildTopicSuggestionPrompt(SelectedCategory, history);
            CurrentProgress = 30;

            // Call OpenRouter AI
            var progress = new Progress<int>(p => CurrentProgress = 30 + (int)(p * 0.6)); // 30-90%
            var response = await _openRouterService.GenerateTextAsync(
                prompt,
                _settings.ScriptGenerationModel,
                progress,
                _processingCts.Token);

            CurrentProgress = 90;

            // Parse response and create SuggestedTopic objects with current category
            var topics = ParseTopicSuggestions(response);
            var currentCategory = SelectedCategory; // Capture category used for generation

            SuggestedTopics.Clear();
            foreach (var topic in topics)
            {
                SuggestedTopics.Add(new SuggestedTopic(topic, currentCategory));
            }
            HasSuggestedTopics = SuggestedTopics.Count > 0;

            CurrentProgress = 100;
            StatusMessage = $"สร้างหัวข้อสำเร็จ ({topics.Count} หัวข้อ) - อ้างอิงจากประวัติ {history.Count} หัวข้อ";
            SnackbarQueue.Enqueue($"สร้างหัวข้อใหม่สำเร็จ! ({currentCategory.DisplayName})");
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "ยกเลิกการสร้างหัวข้อ";
        }
        catch (Exception ex)
        {
            StatusMessage = $"เกิดข้อผิดพลาด: {ex.Message}";
            MessageBox.Show(ex.Message, "ข้อผิดพลาด", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsProcessing = false;
            _processingCts?.Dispose();
            _processingCts = null;
        }
    }

    [RelayCommand]
    private void CancelProcessing()
    {
        _processingCts?.Cancel();
        StatusMessage = "ยกเลิกการทำงาน";
        CurrentProgress = 0;
    }

    [RelayCommand]
    private void CopyTopicToClipboard(SuggestedTopic? suggestedTopic)
    {
        if (suggestedTopic != null && !string.IsNullOrEmpty(suggestedTopic.Topic))
        {
            Clipboard.SetText(suggestedTopic.Topic);
            SnackbarQueue.Enqueue($"คัดลอก: {suggestedTopic.Topic}");
        }
    }

    [RelayCommand]
    private void UseInMainWindow(SuggestedTopic? suggestedTopic)
    {
        if (suggestedTopic == null || string.IsNullOrEmpty(suggestedTopic.Topic))
            return;

        try
        {
            // Get MainViewModel from DI container
            var mainViewModel = App.Services.GetRequiredService<MainViewModel>();

            // Set topic and category from the SuggestedTopic (preserves original category)
            // Use TopicSubject property directly to trigger property changed notifications
            mainViewModel.TopicSubject = suggestedTopic.Topic;
            mainViewModel.SelectedCategory = suggestedTopic.Category;

            // Copy to clipboard as backup
            Clipboard.SetText(suggestedTopic.Topic);

            SnackbarQueue.Enqueue($"✅ ตั้งหัวข้อใน MainWindow แล้ว: {suggestedTopic.Topic} ({suggestedTopic.Category.DisplayName})");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"ไม่สามารถตั้งหัวข้อได้: {ex.Message}", "ข้อผิดพลาด",
                            MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void UseInMultiImage(SuggestedTopic? suggestedTopic)
    {
        if (suggestedTopic == null || string.IsNullOrEmpty(suggestedTopic.Topic))
            return;

        try
        {
            // Check if MultiImageWindow is already open (including minimized windows)
            // Use IsLoaded to check if window exists (closed windows are removed from collection)
            var existingWindow = Application.Current.Windows.OfType<MultiImageWindow>()
                .FirstOrDefault(w => w.IsLoaded);

            if (existingWindow != null)
            {
                try
                {
                    var result = MessageBox.Show(
                        "มี Multi-Image Window เปิดอยู่แล้ว\n\nต้องการเปิดหน้าต่างใหม่หรือไม่?\n(งานที่กำลังทำอยู่จะยังคงอยู่ในหน้าต่างเดิม)",
                        "ยืนยัน", MessageBoxButton.YesNo, MessageBoxImage.Question);

                    if (result == MessageBoxResult.No)
                    {
                        existingWindow.Activate();
                        return;
                    }
                }
                catch
                {
                    // Window is invalid/disposed, continue to create new one
                }
            }

            // Get MainViewModel to get episode number
            var mainViewModel = App.Services.GetRequiredService<MainViewModel>();
            var epNumber = mainViewModel.CurrentProject.EpisodeNumber;

            // Create new MultiImageWindow and ViewModel
            var window = App.Services.GetRequiredService<MultiImageWindow>();
            var vm = App.Services.GetRequiredService<MultiImageViewModel>();

            // Initialize with topic and category from SuggestedTopic (preserves original category)
            vm.Initialize(suggestedTopic.Topic, epNumber, "", suggestedTopic.Category);

            window.DataContext = vm;
            window.Owner = Application.Current.MainWindow;
            window.Show();

            SnackbarQueue.Enqueue($"✅ เปิด Multi-Image พร้อมหัวข้อ: {suggestedTopic.Topic} ({suggestedTopic.Category.DisplayName})");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"ไม่สามารถเปิด Multi-Image ได้: {ex.Message}", "ข้อผิดพลาด",
                            MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task RescanFoldersAsync()
    {
        var result = MessageBox.Show(
            "ต้องการสแกนโฟลเดอร์ทั้งหมดใหม่หรือไม่?\n\n" +
            "(จะเขียนทับไฟล์ประวัติเดิม)\n" +
            $"ตำแหน่ง: {_historyService.GetHistoryFilePath()}",
            "ยืนยันการสแกน",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            IsProcessing = true;
            StatusMessage = "กำลังสแกนโฟลเดอร์...";

            var youtubePath = Path.GetDirectoryName(_historyService.GetHistoryFilePath());
            if (string.IsNullOrEmpty(youtubePath))
            {
                throw new Exception("ไม่พบ path ของโฟลเดอร์ Youtube");
            }

            var count = await _historyService.MigrateFromFoldersAsync(youtubePath);
            StatusMessage = $"สแกนเสร็จสิ้น: พบ {count} หัวข้อ";
            SnackbarQueue.Enqueue($"สแกนโฟลเดอร์สำเร็จ! พบ {count} หัวข้อ");

            await LoadHistoryAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"เกิดข้อผิดพลาด: {ex.Message}";
            MessageBox.Show($"ไม่สามารถสแกนโฟลเดอร์ได้: {ex.Message}\n\n" +
                           "ตรวจสอบว่ามีโฟลเดอร์ EP ใน Downloads\\Youtube หรือไม่",
                           "ข้อผิดพลาด", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private void ToggleHistory()
    {
        ShowHistory = !ShowHistory;
    }

    private string BuildTopicSuggestionPrompt(ContentCategory category, List<string> history)
    {
        var historyText = history.Count > 0
            ? string.Join("\n", history.Select(t => $"- {t}"))
            : "(ยังไม่มีประวัติหัวข้อ)";

        var examples = string.Join("\n", category.TopicExamples.Select(e => $"- \"{e}\""));

        return $@"สวมบทบาทเป็น {category.TopicRoleDescription} สำหรับหมวดหมู่ ""{category.DisplayName}""

ภารกิจของคุณ: เสนอหัวข้อใหม่ ๆ ที่น่าสนใจ มา 5 หัวข้อ โดยต้องไม่ซ้ำกับหัวข้อที่เคยทำมาแล้ว

=== หัวข้อที่เคยทำมาแล้ว (ห้ามซ้ำกับนี้) ===
{historyText}

=== เงื่อนไขสำหรับหัวข้อใหม่ ===
1. หัวข้อต้อง **สั้นกระชับ** ไม่เกิน 6-8 คำ (เหมาะกับปก YouTube)
2. {category.TopicPrefixRule}
3. เนื้อหาต้องเกี่ยวข้องกับหมวดหมู่ ""{category.DisplayName}""
4. ต้องไม่ซ้ำหรือคล้ายกับหัวข้อที่เคยทำมาแล้วด้านบน
5. ใช้คำที่ดึงดูดใจ น่าคลิก แต่ไม่หลอกลวง
6. ตัดคำฟุ่มเฟือยออก เช่น ""ได้อย่างไร"" ""ที่น่าทึ่ง"" ""กันแน่""

**ตัวอย่างหัวข้อที่ดี:**
{examples}

**ตัวอย่างหัวข้อที่ยาวเกินไป (ไม่ต้องการ):**
- ""{category.TopicBadExample}"" ❌

เนื้อหาต้องเข้มข้นพอขยายเป็นวิดีโอ 12-15 นาทีได้

ตอบในรูปแบบ:
1. [หัวข้อสั้นๆ]
2. [หัวข้อสั้นๆ]
3. [หัวข้อสั้นๆ]
4. [หัวข้อสั้นๆ]
5. [หัวข้อสั้นๆ]

(เฉพาะหัวข้อเท่านั้น ไม่ต้องมีคำอธิบาย)";
    }

    private List<string> ParseTopicSuggestions(string response)
    {
        var topics = new List<string>();
        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // Pattern: "1. หัวข้อ" or "- หัวข้อ" or "*  หัวข้อ"
            var match = Regex.Match(trimmed, @"^[\d\-\*\.]+\s*(.+)$");
            if (match.Success)
            {
                var topic = match.Groups[1].Value.Trim();
                // Remove quotes if present
                topic = topic.Trim('"', '"', '"', '\'');

                if (!string.IsNullOrWhiteSpace(topic) && topic.Length > 3)
                {
                    topics.Add(topic);
                }
            }
        }

        // Take first 5 topics
        return topics.Take(5).ToList();
    }
}
