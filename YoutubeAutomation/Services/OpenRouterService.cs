using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YoutubeAutomation.Models;
using YoutubeAutomation.Services.Interfaces;

namespace YoutubeAutomation.Services;

public class OpenRouterService : IOpenRouterService
{
    private readonly HttpClient _httpClient;
    private readonly ProjectSettings _settings;
    private const string BaseUrl = "https://openrouter.ai/api/v1/chat/completions";

    public OpenRouterService(ProjectSettings settings)
    {
        _settings = settings;
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromMinutes(3);
        _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "https://youtube-automation.local");
        _httpClient.DefaultRequestHeaders.Add("X-Title", "YouTube Automation");
    }

    public async Task<string> GenerateTextAsync(
        string prompt,
        string model,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report(10);

        var request = new
        {
            model = model,
            messages = new[]
            {
                new { role = "user", content = prompt }
            },
            max_tokens = 8192,
            temperature = 0.7
        };

        var json = JsonConvert.SerializeObject(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, BaseUrl);
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.OpenRouterApiKey);
        requestMessage.Content = content;

        progress?.Report(30);

        var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"OpenRouter API error: {response.StatusCode} - {responseContent}");
        }

        progress?.Report(80);

        var result = JObject.Parse(responseContent);
        var text = result["choices"]?[0]?["message"]?["content"]?.ToString() ?? "";

        progress?.Report(100);

        return text;
    }

    public async Task<string> GenerateImagePromptAsync(
        string topic,
        string model,
        CancellationToken cancellationToken = default,
        ContentCategory? category = null)
    {
        var cat = category ?? ContentCategoryRegistry.Animal;
        var prompt = $@"สร้าง prompt ภาษาอังกฤษสำหรับสร้างรูปปก YouTube video ในหัวข้อ: {topic}

รูปแบบที่ต้องการ:
- Style: {cat.CloudImageStyleDescription}
- Technique: {cat.CoverImageTechnique}
- Color Palette: {cat.CloudImageColorPalette}
- Composition: ฉากกว้างที่เป็นองค์ประกอบของเรื่อง

ตอบเป็น prompt ภาษาอังกฤษเท่านั้น ไม่ต้องมีคำอธิบายอื่น";

        var basePrompt = await GenerateTextAsync(prompt, model, null, cancellationToken);

        // Append Thai topic title instruction to the prompt
        var finalPrompt = $@"{basePrompt}

IMPORTANT: The image MUST include the Thai text ""{topic}"" displayed prominently in the image. The text should be large, bold, clearly readable, placed at the top or center of the image, on a vintage-style banner, scroll, or aged paper overlay.";

        return finalPrompt;
    }

    public async Task<byte[]> GenerateImageAsync(
        string prompt,
        string model,
        string? referenceImagePath = null,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default,
        string? topicTitle = null,
        ContentCategory? category = null)
    {
        progress?.Report(10);

        // Build the message content with text and optional reference image
        var contentParts = new List<object>();

        // Add reference image if provided
        if (!string.IsNullOrEmpty(referenceImagePath) && File.Exists(referenceImagePath))
        {
            var imageBytes = await File.ReadAllBytesAsync(referenceImagePath, cancellationToken);
            var base64Image = Convert.ToBase64String(imageBytes);
            var mimeType = Path.GetExtension(referenceImagePath).ToLower() switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".webp" => "image/webp",
                _ => "image/png"
            };

            contentParts.Add(new
            {
                type = "image_url",
                image_url = new
                {
                    url = $"data:{mimeType};base64,{base64Image}"
                }
            });
        }

        // Add text prompt with style instructions
        // Include topic title text in the image if provided
        var textInstruction = string.IsNullOrEmpty(topicTitle)
            ? "No text or letters in the image."
            : $@"IMPORTANT: Include the Thai text ""{topicTitle}"" prominently in the image.
The text should be large, bold, and clearly readable.
Place the text in the upper portion or center of the image.
Use a style that matches the vintage aesthetic - perhaps on a banner, scroll, or aged paper overlay.";

        var cat = category ?? ContentCategoryRegistry.Animal;
        var fullPrompt = $@"Generate a YouTube thumbnail image with the following requirements:

Style: {cat.CloudImageStyleDescription} with {cat.CoverImageTechnique.ToLower()}.
Color Palette: {cat.CloudImageColorPalette}.
Composition: Wide scene showing the main subject.
Aspect Ratio: 16:9 (landscape)

{textInstruction}

Subject: {prompt}

{(referenceImagePath != null ? "Use the provided reference image as style guide for the vintage scientific illustration aesthetic." : "")}";

        contentParts.Add(new
        {
            type = "text",
            text = fullPrompt
        });

        var request = new
        {
            model = model,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = contentParts
                }
            },
            max_tokens = 4096
        };

        var json = JsonConvert.SerializeObject(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, BaseUrl);
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.OpenRouterApiKey);
        requestMessage.Content = content;

        progress?.Report(30);

        var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"OpenRouter Image API error: {response.StatusCode} - {responseContent}");
        }

        progress?.Report(70);

        var result = JObject.Parse(responseContent);

        // Try to extract image from response (different models return differently)
        // Check for inline_data format (Gemini style)
        var inlineData = result["candidates"]?[0]?["content"]?["parts"]?[0]?["inlineData"]
            ?? result["choices"]?[0]?["message"]?["content"];

        if (inlineData == null)
        {
            // Try to get image URL from the response
            var imageUrl = result["choices"]?[0]?["message"]?["content"]?.ToString();
            if (!string.IsNullOrEmpty(imageUrl) && imageUrl.StartsWith("http"))
            {
                // Download image from URL
                var imageResponse = await _httpClient.GetAsync(imageUrl, cancellationToken);
                var imageBytes = await imageResponse.Content.ReadAsByteArrayAsync(cancellationToken);
                progress?.Report(100);
                return imageBytes;
            }

            // Try to extract base64 from text content
            var textContent = result["choices"]?[0]?["message"]?["content"]?.ToString() ?? "";
            if (textContent.Contains("base64"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(textContent, @"data:image/[^;]+;base64,([A-Za-z0-9+/=]+)");
                if (match.Success)
                {
                    progress?.Report(100);
                    return Convert.FromBase64String(match.Groups[1].Value);
                }
            }

            throw new Exception("No image data in response. The model may not support image generation.");
        }

        // Handle inline data (base64)
        string base64Data;
        if (inlineData is JObject inlineObj)
        {
            base64Data = inlineObj["data"]?.ToString() ?? "";
        }
        else
        {
            base64Data = inlineData.ToString() ?? "";
            // Try to extract base64 if it's in a data URL format
            var match = System.Text.RegularExpressions.Regex.Match(base64Data, @"base64,([A-Za-z0-9+/=]+)");
            if (match.Success)
            {
                base64Data = match.Groups[1].Value;
            }
        }

        progress?.Report(100);
        return Convert.FromBase64String(base64Data);
    }

    public async Task<byte[]> GenerateSceneImageFromCloudAsync(
        string prompt,
        string model,
        string? referenceImagePath = null,
        string aspectRatio = "16:9",
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report(10);

        var contentParts = new List<object>();

        // Add reference image if provided (for scene chaining)
        if (!string.IsNullOrEmpty(referenceImagePath) && File.Exists(referenceImagePath))
        {
            var imageBytes = await File.ReadAllBytesAsync(referenceImagePath, cancellationToken);
            var base64Image = Convert.ToBase64String(imageBytes);
            var mimeType = Path.GetExtension(referenceImagePath).ToLower() switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".webp" => "image/webp",
                _ => "image/png"
            };

            contentParts.Add(new
            {
                type = "image_url",
                image_url = new { url = $"data:{mimeType};base64,{base64Image}" }
            });
        }

        // Add text prompt directly (no style wrapping — cloud model handles style from prompt)
        contentParts.Add(new { type = "text", text = prompt });

        var request = new
        {
            model = model,
            messages = new[]
            {
                new { role = "user", content = contentParts }
            },
            modalities = new[] { "image", "text" },
            image_config = new { aspect_ratio = aspectRatio },
            max_tokens = 4096
        };

        var json = JsonConvert.SerializeObject(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, BaseUrl);
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.OpenRouterApiKey);
        requestMessage.Content = content;

        progress?.Report(30);

        var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new Exception($"OpenRouter Cloud Image error: {response.StatusCode} - {responseContent}");

        progress?.Report(70);

        var result = JObject.Parse(responseContent);

        // Strategy 1: images array (OpenRouter documented format)
        var imagesArray = result["choices"]?[0]?["message"]?["images"] as JArray;
        if (imagesArray != null && imagesArray.Count > 0)
        {
            var imageUrl = imagesArray[0]?["image_url"]?["url"]?.ToString();
            if (!string.IsNullOrEmpty(imageUrl))
            {
                var match = System.Text.RegularExpressions.Regex.Match(imageUrl, @"base64,([A-Za-z0-9+/=]+)");
                if (match.Success)
                {
                    progress?.Report(100);
                    return Convert.FromBase64String(match.Groups[1].Value);
                }
                // If it's a URL instead of base64
                if (imageUrl.StartsWith("http"))
                {
                    var imgResponse = await _httpClient.GetAsync(imageUrl, cancellationToken);
                    progress?.Report(100);
                    return await imgResponse.Content.ReadAsByteArrayAsync(cancellationToken);
                }
            }
        }

        // Strategy 2: inline base64 in text content
        var textContent = result["choices"]?[0]?["message"]?["content"]?.ToString() ?? "";
        if (textContent.Contains("base64"))
        {
            var match = System.Text.RegularExpressions.Regex.Match(textContent, @"data:image/[^;]+;base64,([A-Za-z0-9+/=]+)");
            if (match.Success)
            {
                progress?.Report(100);
                return Convert.FromBase64String(match.Groups[1].Value);
            }
        }

        // Strategy 3: direct URL
        if (!string.IsNullOrEmpty(textContent) && textContent.StartsWith("http"))
        {
            var imgResponse = await _httpClient.GetAsync(textContent, cancellationToken);
            progress?.Report(100);
            return await imgResponse.Content.ReadAsByteArrayAsync(cancellationToken);
        }

        throw new Exception(
            $"No image data in OpenRouter response. Model '{model}' may not support image generation.\n\n" +
            $"Response: {responseContent[..Math.Min(500, responseContent.Length)]}");
    }

    public async Task<bool> TestConnectionAsync(string apiKey)
    {
        try
        {
            var request = new
            {
                model = "google/gemini-2.5-flash",
                messages = new[]
                {
                    new { role = "user", content = "Say 'OK' if you receive this." }
                },
                max_tokens = 10
            };

            var json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, BaseUrl);
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            requestMessage.Headers.Add("HTTP-Referer", "https://youtube-automation.local");
            requestMessage.Content = content;

            var response = await _httpClient.SendAsync(requestMessage);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
