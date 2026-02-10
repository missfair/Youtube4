using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Windows.Media.Imaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YoutubeAutomation.Models;
using YoutubeAutomation.Services.Interfaces;

namespace YoutubeAutomation.Services;

public class StableDiffusionService : IStableDiffusionService
{
    private readonly HttpClient _httpClient;
    private readonly ProjectSettings _settings;
    private Process? _sdProcess;

    public StableDiffusionService(ProjectSettings settings)
    {
        _settings = settings;
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromMinutes(5); // Increased from 2min for XL models
    }

    public bool IsProcessRunning
    {
        get
        {
            try { return _sdProcess != null && !_sdProcess.HasExited; }
            catch { return false; }
        }
    }

    public async Task<byte[]> GenerateImageAsync(
        string prompt,
        string negativePrompt = "",
        int width = 768,
        int height = 432,
        CancellationToken cancellationToken = default)
    {
        AppLogger.Log($"txt2img: {width}x{height}, steps={_settings.StableDiffusionSteps}, cfg={_settings.StableDiffusionCfgScale}, prompt={prompt[..Math.Min(60, prompt.Length)]}...");
        var baseUrl = _settings.StableDiffusionUrl.TrimEnd('/');
        var url = $"{baseUrl}/sdapi/v1/txt2img";

        if (string.IsNullOrWhiteSpace(negativePrompt))
        {
            negativePrompt = "text, letters, words, numbers, watermark, signature, logo, photographic, 3d render, anime, blurry, low quality, deformed, disfigured, out of frame, monochrome, grayscale, black and white";
        }

        var request = new
        {
            prompt = prompt,
            negative_prompt = negativePrompt,
            width = width,
            height = height,
            steps = _settings.StableDiffusionSteps,
            cfg_scale = _settings.StableDiffusionCfgScale,
            sampler_name = "DPM++ 2M Karras",
            seed = -1,
            batch_size = 1
        };

        var json = JsonConvert.SerializeObject(request);

        return await PostWithRetryAsync(url, json, baseUrl, "txt2img", cancellationToken);
    }

    public async Task<byte[]> GenerateImageWithReferenceAsync(
        string prompt,
        string referenceImagePath,
        double denoisingStrength = 0.6,
        string negativePrompt = "",
        int width = 768,
        int height = 432,
        CancellationToken cancellationToken = default)
    {
        AppLogger.Log($"img2img: {width}x{height}, denoising={denoisingStrength}, ref={Path.GetFileName(referenceImagePath)}, prompt={prompt[..Math.Min(60, prompt.Length)]}...");
        var baseUrl = _settings.StableDiffusionUrl.TrimEnd('/');
        var url = $"{baseUrl}/sdapi/v1/img2img";

        if (string.IsNullOrWhiteSpace(negativePrompt))
        {
            negativePrompt = "text, letters, words, numbers, watermark, signature, logo, photographic, 3d render, anime, blurry, low quality, deformed, disfigured, out of frame, monochrome, grayscale, black and white";
        }

        // Resize reference image to target dimensions to reduce payload + VRAM
        var refImageBytes = ResizeReferenceImage(referenceImagePath, width, height);
        var base64RefImage = Convert.ToBase64String(refImageBytes);
        AppLogger.Log($"img2img ref image resized: {refImageBytes.Length / 1024}KB base64={base64RefImage.Length / 1024}KB");

        var request = new
        {
            prompt = prompt,
            negative_prompt = negativePrompt,
            init_images = new[] { base64RefImage },
            denoising_strength = denoisingStrength,
            resize_mode = 1, // Crop and resize
            width = width,
            height = height,
            steps = _settings.StableDiffusionSteps,
            cfg_scale = _settings.StableDiffusionCfgScale,
            sampler_name = "DPM++ 2M Karras",
            seed = -1,
            batch_size = 1
        };

        var json = JsonConvert.SerializeObject(request);

        try
        {
            var result = await PostWithRetryAsync(url, json, baseUrl, "img2img", cancellationToken);
            AppLogger.Log($"img2img completed successfully ({result.Length} bytes)");
            return result;
        }
        catch (OperationCanceledException) { throw; } // Don't fallback on user cancel
        catch (Exception ex)
        {
            // Fallback: if img2img fails after retries, try txt2img (no reference but still get an image)
            AppLogger.LogError(ex, "img2img failed, falling back to txt2img");
            var txt2imgUrl = $"{baseUrl}/sdapi/v1/txt2img";
            var fallbackRequest = new
            {
                prompt = prompt,
                negative_prompt = negativePrompt,
                width = width,
                height = height,
                steps = _settings.StableDiffusionSteps,
                cfg_scale = _settings.StableDiffusionCfgScale,
                sampler_name = "DPM++ 2M Karras",
                seed = -1,
                batch_size = 1
            };
            var fallbackJson = JsonConvert.SerializeObject(fallbackRequest);
            return await PostWithRetryAsync(txt2imgUrl, fallbackJson, baseUrl, "txt2img (fallback)", cancellationToken);
        }
    }

    /// <summary>POST to SD API with retry on 5xx errors (VRAM exhaustion, etc.)</summary>
    private async Task<byte[]> PostWithRetryAsync(
        string url, string json, string baseUrl, string apiName,
        CancellationToken cancellationToken, int maxRetries = 2, int retryDelayMs = 5000)
    {
        HttpResponseMessage? response = null;
        string? responseContent = null;

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                AppLogger.Log($"SD {apiName} attempt {attempt + 1}/{maxRetries} - POST {url} (payload {json.Length / 1024}KB)...");
                response = await _httpClient.PostAsync(url, content, cancellationToken);
                sw.Stop();
                AppLogger.Log($"SD {apiName} attempt {attempt + 1} - response {response.StatusCode} in {sw.ElapsedMilliseconds}ms");
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                sw.Stop();
                AppLogger.LogError(ex, $"SD {apiName} HTTP TIMEOUT after {sw.ElapsedMilliseconds}ms");
                throw new Exception(
                    $"Stable Diffusion {apiName} timeout ({sw.ElapsedMilliseconds}ms)\n\n" +
                    $"โมเดลอาจหนักเกินไป — ลองลดขนาดภาพหรือเปลี่ยนโมเดล");
            }
            catch (HttpRequestException ex)
            {
                sw.Stop();
                AppLogger.LogError(ex, $"SD {apiName} connection failed");
                throw new Exception(
                    $"ไม่สามารถเชื่อมต่อ Stable Diffusion Forge ได้\n\n" +
                    $"กรุณาตรวจสอบ:\n" +
                    $"1. เปิด Stable Diffusion Forge แล้วหรือยัง?\n" +
                    $"2. URL ถูกต้องหรือไม่? ({baseUrl})\n" +
                    $"3. เปิดด้วย --api flag หรือไม่?");
            }

            responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
                break;

            // Retry on 5xx server errors (VRAM exhaustion, OSError, etc.)
            if ((int)response.StatusCode >= 500 && attempt < maxRetries - 1)
            {
                AppLogger.Log($"SD {apiName} attempt {attempt + 1} failed ({response.StatusCode}), retrying in {retryDelayMs}ms... Response: {responseContent[..Math.Min(200, responseContent.Length)]}");
                await Task.Delay(retryDelayMs, cancellationToken);
                continue;
            }

            AppLogger.Log($"SD {apiName} FAILED: {response.StatusCode} - {responseContent[..Math.Min(300, responseContent.Length)]}");
            throw new Exception($"Stable Diffusion {apiName} API error: {response.StatusCode} - {responseContent}");
        }

        var result = JObject.Parse(responseContent!);
        var images = result["images"] as JArray;

        if (images == null || images.Count == 0)
        {
            AppLogger.Log($"SD {apiName} returned no images! Response keys: {string.Join(", ", result.Properties().Select(p => p.Name))}");
            throw new Exception($"Stable Diffusion {apiName} ไม่ได้ส่งภาพกลับมา");
        }

        var base64Image = images[0]?.ToString() ?? "";
        AppLogger.Log($"SD {apiName} OK - image base64 length: {base64Image.Length}");
        return Convert.FromBase64String(base64Image);
    }

    /// <summary>Common ports SD Forge / WebUI may listen on.</summary>
    private static readonly int[] CommonPorts = { 7860, 7861, 7862, 7863 };

    public async Task<bool> TestConnectionAsync()
    {
        // First try the configured URL
        var baseUrl = _settings.StableDiffusionUrl.TrimEnd('/');
        if (await TryConnectAsync(baseUrl))
            return true;

        // Auto-scan common ports
        AppLogger.Log($"SD not found at {baseUrl}, scanning common ports...");
        var uri = new Uri(baseUrl);
        foreach (var port in CommonPorts)
        {
            if (port == uri.Port) continue; // Already tried

            var candidateUrl = $"{uri.Scheme}://{uri.Host}:{port}";
            if (await TryConnectAsync(candidateUrl))
            {
                AppLogger.Log($"SD found on port {port}! Updating URL: {_settings.StableDiffusionUrl} -> {candidateUrl}");
                _settings.StableDiffusionUrl = candidateUrl;
                _settings.Save();
                return true;
            }
        }

        AppLogger.Log("SD not found on any common port");
        return false;
    }

    private async Task<bool> TryConnectAsync(string baseUrl)
    {
        try
        {
            var url = $"{baseUrl.TrimEnd('/')}/sdapi/v1/options";
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var response = await _httpClient.GetAsync(url, cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task RefreshCheckpointsAsync(CancellationToken cancellationToken = default)
    {
        var baseUrl = _settings.StableDiffusionUrl.TrimEnd('/');
        var url = $"{baseUrl}/sdapi/v1/refresh-checkpoints";
        await _httpClient.PostAsync(url, null, cancellationToken);
    }

    public async Task<List<string>> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        var baseUrl = _settings.StableDiffusionUrl.TrimEnd('/');
        var url = $"{baseUrl}/sdapi/v1/sd-models";

        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        var models = JArray.Parse(content);
        return models
            .Select(m => m["title"]?.ToString() ?? "")
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();
    }

    public async Task<string> GetCurrentModelAsync(CancellationToken cancellationToken = default)
    {
        var baseUrl = _settings.StableDiffusionUrl.TrimEnd('/');
        var url = $"{baseUrl}/sdapi/v1/options";

        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        var options = JObject.Parse(content);
        return options["sd_model_checkpoint"]?.ToString() ?? "";
    }

    public async Task SetModelAsync(string modelName, CancellationToken cancellationToken = default)
    {
        var baseUrl = _settings.StableDiffusionUrl.TrimEnd('/');
        var url = $"{baseUrl}/sdapi/v1/options";

        var payload = new { sd_model_checkpoint = modelName };
        var json = JsonConvert.SerializeObject(payload);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(url, httpContent, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new Exception($"เปลี่ยน model ไม่สำเร็จ: {response.StatusCode} - {errorContent}");
        }
    }

    public async Task<bool> LaunchAsync(IProgress<string>? status = null, CancellationToken cancellationToken = default)
    {
        // Already running? (includes port auto-scan)
        if (await TestConnectionAsync()) return true;

        var batPath = _settings.StableDiffusionBatPath;
        if (string.IsNullOrWhiteSpace(batPath) || !File.Exists(batPath))
            throw new InvalidOperationException(
                "ยังไม่ได้ตั้งค่าที่ตั้ง SD Forge\n\nกรุณาเลือกไฟล์ .bat ของ SD Forge (เช่น run.bat หรือ webui-user.bat)");

        // Launch process (minimized, new window)
        status?.Report("กำลังเริ่ม SD Forge...");
        _sdProcess = Process.Start(new ProcessStartInfo
        {
            FileName = batPath,
            WorkingDirectory = Path.GetDirectoryName(batPath)!,
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Minimized
        });

        // Poll until ready (timeout 4 minutes)
        for (int i = 0; i < 120; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Check if process died
            if (_sdProcess != null && _sdProcess.HasExited)
            {
                throw new Exception($"SD Forge process หยุดทำงานก่อนเวลา (exit code: {_sdProcess.ExitCode})");
            }

            status?.Report($"กำลังเริ่ม SD Forge... ({i * 2} วินาที)");
            await Task.Delay(2000, cancellationToken);

            if (await TestConnectionAsync()) return true;
        }

        throw new TimeoutException("SD Forge ไม่สามารถเริ่มได้ภายใน 4 นาที กรุณาตรวจสอบ console window ของ SD Forge");
    }

    /// <summary>
    /// Resize reference image to target dimensions using WPF imaging.
    /// Reduces payload size and VRAM usage for img2img.
    /// </summary>
    private static byte[] ResizeReferenceImage(string imagePath, int targetWidth, int targetHeight)
    {
        try
        {
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.UriSource = new Uri(Path.GetFullPath(imagePath));
            bi.DecodePixelWidth = targetWidth;
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.EndInit();
            bi.Freeze();

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bi));

            using var ms = new MemoryStream();
            encoder.Save(ms);
            return ms.ToArray();
        }
        catch (Exception ex)
        {
            // Fallback: send original file if resize fails
            AppLogger.LogError(ex, "ResizeReferenceImage failed, using original");
            return File.ReadAllBytes(imagePath);
        }
    }

    public Task StopAsync()
    {
        try
        {
            if (_sdProcess != null && !_sdProcess.HasExited)
            {
                _sdProcess.Kill(true); // Kill entire process tree (.NET 8)
                _sdProcess.WaitForExit(5000);
            }
        }
        catch { }
        finally
        {
            try { _sdProcess?.Dispose(); } catch { }
            _sdProcess = null;
        }
        return Task.CompletedTask;
    }
}
