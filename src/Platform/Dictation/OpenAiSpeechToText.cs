using System.Net.Http;
using System.Net.Http.Headers;
using Koy.Core.Dictation;

namespace Koy.Platform.Dictation;

/// <summary>
/// OpenAI uyumlu <c>/audio/transcriptions</c> uç noktası: OpenAI, OpenRouter, Groq ya da kullanıcının kendi bilgisayarında
/// çalışan bir sunucu (o zaman ses bilgisayardan çıkmaz). Koy yerel sunucuyu başlatmaz, yalnız adresine bağlanır (AGENTS #4).
/// </summary>
public sealed class OpenAiSpeechToText(Func<DictationConfig> config, Func<string> apiKey) : ISpeechToText
{
    public Task<TranscriptionResult> TranscribeAsync(byte[] wav, TranscriptionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(wav);
        ArgumentNullException.ThrowIfNull(request);

        var current = config();
        var model = string.IsNullOrWhiteSpace(request.ModelOverride) ? current.TranscribeModel : request.ModelOverride!;

        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(wav);
        file.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        form.Add(file, "file", "audio.wav");
        form.Add(new StringContent(model), "model");
        form.Add(new StringContent(OpenAiProtocol.WantsVerboseJson(model) ? "verbose_json" : "json"), "response_format");
        if (!string.IsNullOrWhiteSpace(request.Language) && !string.Equals(request.Language, "auto", StringComparison.OrdinalIgnoreCase))
            form.Add(new StringContent(request.Language.Trim()), "language");
        if (!string.IsNullOrWhiteSpace(request.Prompt))
            form.Add(new StringContent(request.Prompt), "prompt");

        var message = new HttpRequestMessage(HttpMethod.Post, OpenAiProtocol.EndpointUrl(current.BaseUrl, "audio/transcriptions")) { Content = form };
        OpenAiHttp.ApplyKey(message, apiKey());
        return SendAndDisposeAsync(message, cancellationToken);
    }

    private static async Task<TranscriptionResult> SendAndDisposeAsync(HttpRequestMessage message, CancellationToken cancellationToken)
    {
        using (message)
            return await OpenAiHttp.SendAsync(message, OpenAiProtocol.ParseTranscription, cancellationToken).ConfigureAwait(false);
    }
}
