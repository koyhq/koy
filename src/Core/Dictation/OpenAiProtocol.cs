using System.Text;
using System.Text.Json;

namespace Koy.Core.Dictation;

/// <summary>
/// OpenAI uyumlu uç noktaların (OpenAI, OpenRouter, Groq, yerelde çalışan bir sunucu…) istek ve yanıt biçimi. Saf ve testli;
/// ağ çağrısı Platform'da. Yansımalı serileştirme yok: istek <see cref="Utf8JsonWriter"/> ile, yanıt <see cref="JsonDocument"/> ile.
/// </summary>
public static class OpenAiProtocol
{
    public static string EndpointUrl(string baseUrl, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        return baseUrl.Trim().TrimEnd('/') + "/" + path.TrimStart('/');
    }

    /// <summary>
    /// Dili ve süreyi de geri veren <c>verbose_json</c> yalnız Whisper ailesinde var; öbür modeller yalnız <c>json</c> bilir
    /// ve bilinmeyen biçimi reddeder.
    /// </summary>
    public static bool WantsVerboseJson(string model) =>
        model.Contains("whisper", StringComparison.OrdinalIgnoreCase) && !model.Contains("gpt-4o", StringComparison.OrdinalIgnoreCase);

    public static string BuildChatRequest(string model, string systemPrompt, string userMessage)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("model", model);
            writer.WriteNumber("temperature", 0);
            writer.WriteStartArray("messages");
            WriteMessage(writer, "system", systemPrompt);
            WriteMessage(writer, "user", userMessage);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>Yanıt JSON değilse (bazı sunucular düz metin döner) gövde metnin kendisidir.</summary>
    public static TranscriptionResult ParseTranscription(string body)
    {
        ArgumentNullException.ThrowIfNull(body);
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return new TranscriptionResult(body.Trim());

            var text = root.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() ?? "" : "";
            var language = root.TryGetProperty("language", out var l) && l.ValueKind == JsonValueKind.String ? l.GetString() ?? "" : "";
            return new TranscriptionResult(text.Trim(), LanguageCode(language));
        }
        catch (JsonException)
        {
            return new TranscriptionResult(body.Trim());
        }
    }

    /// <summary>Sohbet yanıtının metni; <c>content</c> dizi de olabilir (parçalar birleştirilir). Yoksa boş.</summary>
    public static string ParseChatReply(string body)
    {
        ArgumentNullException.ThrowIfNull(body);
        try
        {
            using var document = JsonDocument.Parse(body);
            if (!document.RootElement.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
                return "";

            if (!choices[0].TryGetProperty("message", out var message) || !message.TryGetProperty("content", out var content))
                return "";

            switch (content.ValueKind)
            {
                case JsonValueKind.String:
                    return content.GetString() ?? "";
                case JsonValueKind.Array:
                    var builder = new StringBuilder();
                    foreach (var part in content.EnumerateArray())
                    {
                        if (part.ValueKind == JsonValueKind.Object && part.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                            builder.Append(text.GetString());
                    }

                    return builder.ToString();
                default:
                    return "";
            }
        }
        catch (JsonException)
        {
            return "";
        }
    }

    /// <summary>Hata gövdesindeki <c>error.message</c>; yoksa durum kodu.</summary>
    public static string ErrorMessage(int status, string? body)
    {
        var fallback = "HTTP " + status.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(body))
            return fallback;

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.String)
                    return fallback + ": " + error.GetString();
                if (error.ValueKind == JsonValueKind.Object && error.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
                    return fallback + ": " + message.GetString();
            }
        }
        catch (JsonException)
        {
            // JSON olmayan gövde: durum kodu yeter.
        }

        return fallback;
    }

    /// <summary>Beklenip yeniden denemeye değer durumlar: zaman aşımı, hız sınırı, sunucu hatası.</summary>
    public static bool IsTransient(int status) => status is 408 or 425 or 429 || status >= 500;

    /// <summary>Whisper dil adını ("turkish") kısa koda ("tr") çevirir; tanınmayan olduğu gibi (küçük harf) döner.</summary>
    public static string LanguageCode(string language)
    {
        var name = language.Trim().ToLowerInvariant();
        return name switch
        {
            "turkish" => "tr",
            "english" => "en",
            "german" => "de",
            "french" => "fr",
            "spanish" => "es",
            "italian" => "it",
            "portuguese" => "pt",
            "russian" => "ru",
            "arabic" => "ar",
            "japanese" => "ja",
            "korean" => "ko",
            "chinese" => "zh",
            "dutch" => "nl",
            "polish" => "pl",
            "ukrainian" => "uk",
            "hindi" => "hi",
            _ => name,
        };
    }

    private static void WriteMessage(Utf8JsonWriter writer, string role, string content)
    {
        writer.WriteStartObject();
        writer.WriteString("role", role);
        writer.WriteString("content", content);
        writer.WriteEndObject();
    }
}
