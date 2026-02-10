using System.IO;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YoutubeAutomation.Models;
using YoutubeAutomation.Services.Interfaces;

namespace YoutubeAutomation.Services;

public class GoogleTtsService : IGoogleTtsService
{
    private readonly HttpClient _httpClient;
    private readonly ProjectSettings _settings;
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";

    public GoogleTtsService(ProjectSettings settings)
    {
        _settings = settings;
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromMinutes(5); // TTS can take time
    }

    public async Task<byte[]> GenerateAudioAsync(
        string text,
        string voiceName,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report(10);

        var model = _settings.TtsModel;
        var url = $"{BaseUrl}/{model}:generateContent?key={_settings.GoogleApiKey}";

        // Format text for TTS
        var formattedText = $"Read aloud in an informative, lower pitch, and mysterious tone.\n\nSpeaker 1: {text}";

        var request = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = formattedText }
                    }
                }
            },
            generationConfig = new
            {
                responseModalities = new[] { "AUDIO" },
                speechConfig = new
                {
                    voiceConfig = new
                    {
                        prebuiltVoiceConfig = new
                        {
                            voiceName = voiceName
                        }
                    }
                }
            }
        };

        var json = JsonConvert.SerializeObject(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        progress?.Report(30);

        var response = await _httpClient.PostAsync(url, content, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Google TTS API error: {response.StatusCode} - {responseContent}");
        }

        progress?.Report(70);

        var result = JObject.Parse(responseContent);
        var inlineData = result["candidates"]?[0]?["content"]?["parts"]?[0]?["inlineData"];

        if (inlineData == null)
        {
            throw new Exception("No audio data in response");
        }

        var base64Data = inlineData["data"]?.ToString() ?? "";
        var mimeType = inlineData["mimeType"]?.ToString() ?? "audio/L16;rate=24000";

        var pcmData = Convert.FromBase64String(base64Data);

        // Parse sample rate from mime type
        var sampleRate = 24000;
        var bitsPerSample = 16;
        if (mimeType.Contains("rate="))
        {
            var rateMatch = System.Text.RegularExpressions.Regex.Match(mimeType, @"rate=(\d+)");
            if (rateMatch.Success)
            {
                sampleRate = int.Parse(rateMatch.Groups[1].Value);
            }
        }
        if (mimeType.Contains("L"))
        {
            var bitsMatch = System.Text.RegularExpressions.Regex.Match(mimeType, @"L(\d+)");
            if (bitsMatch.Success)
            {
                bitsPerSample = int.Parse(bitsMatch.Groups[1].Value);
            }
        }

        progress?.Report(90);

        var wavData = ConvertPcmToWav(pcmData, sampleRate, 1, bitsPerSample);

        progress?.Report(100);

        return wavData;
    }

    private byte[] ConvertPcmToWav(byte[] pcmData, int sampleRate, int channels, int bitsPerSample)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        var bytesPerSample = bitsPerSample / 8;
        var blockAlign = channels * bytesPerSample;
        var byteRate = sampleRate * blockAlign;

        // RIFF header
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + pcmData.Length); // ChunkSize
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));

        // fmt subchunk
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16); // Subchunk1Size (16 for PCM)
        writer.Write((short)1); // AudioFormat (1 for PCM)
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write((short)blockAlign);
        writer.Write((short)bitsPerSample);

        // data subchunk
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(pcmData.Length);
        writer.Write(pcmData);

        return ms.ToArray();
    }

    public async Task<bool> TestConnectionAsync(string apiKey)
    {
        try
        {
            var url = $"https://generativelanguage.googleapis.com/v1beta/models?key={apiKey}";
            var response = await _httpClient.GetAsync(url);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
