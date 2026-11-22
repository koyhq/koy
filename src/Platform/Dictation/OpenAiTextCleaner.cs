using System.Net.Http;
using System.Text;
using Koy.Core.Dictation;

namespace Koy.Platform.Dictation;

/// <summary>
/// Temizleme ve çeviri: OpenAI uyumlu <c>/chat/completions</c>, tek tur, araç yok, önceki yazıdan hafıza yok.
/// Metin mikrofondan geldiği için veridir; çit ve istem Core'da (<see cref="CleanupPrompts"/>).
/// </summary>
public sealed class OpenAiTextCleaner(Func<DictationConfig> config, Func<string> apiKey) : ITextCleaner
{
    public Task<string> CompleteAsync(string systemPrompt, string userMessage, CancellationToken cancellationToken)
    {
        var current = config();
        var baseUrl = string.IsNullOrWhiteSpace(current.CleanupBaseUrl) ? current.BaseUrl : current.CleanupBaseUrl;
        var json = OpenAiProtocol.BuildChatRequest(current.CleanupModel, systemPrompt, userMessage);

        var message = new HttpRequestMessage(HttpMethod.Post, OpenAiProtocol.EndpointUrl(baseUrl, "chat/completions"))
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        OpenAiHttp.ApplyKey(message, apiKey());
        return SendAndDisposeAsync(message, cancellationToken);
    }

    private static async Task<string> SendAndDisposeAsync(HttpRequestMessage message, CancellationToken cancellationToken)
    {
        using (message)
            return await OpenAiHttp.SendAsync(message, OpenAiProtocol.ParseChatReply, cancellationToken).ConfigureAwait(false);
    }
}
