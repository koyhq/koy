using System.Net.Http;
using System.Net.Http.Headers;
using Koy.Core.Dictation;

namespace Koy.Platform.Dictation;

/// <summary>
/// Konuşma tanıma ve temizleme isteklerinin ortak gönderimi. Uygulamanın paylaşılan 10 sn'lik istemcisi (<c>AppHttp</c>) burada
/// yetmez: birkaç dakikalık ses yüklemesi uzun sürer. Ayrı istemci, ilk kullanımda kurulur; açılışta bağlantı açılmaz.
/// Hata sınıflandırması Core'da (<see cref="OpenAiProtocol.IsTransient"/>): hız sınırı ve ağ hatası beklenip yeniden denenir,
/// yanlış anahtar ya da yanlış model denenmez.
/// </summary>
internal static class OpenAiHttp
{
    private static readonly HttpClient Client = Create();

    private static HttpClient Create()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Koy (+https://github.com/koyhq/koy)");
        return client;
    }

    public static void ApplyKey(HttpRequestMessage message, string apiKey)
    {
        if (!string.IsNullOrWhiteSpace(apiKey))
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
    }

    public static async Task<T> SendAsync<T>(HttpRequestMessage message, Func<string, T> parse, CancellationToken cancellationToken)
    {
        string body;
        int status;
        try
        {
            using var response = await Client.SendAsync(message, cancellationToken).ConfigureAwait(false);
            status = (int)response.StatusCode;
            body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
                return parse(body);
        }
        catch (HttpRequestException ex)
        {
            throw new SpeechApiException(ex.Message, transient: true, ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // İstemcinin kendi zaman aşımı: kullanıcı iptal etmedi.
            throw new SpeechApiException("Timed out", transient: true, ex);
        }
        catch (InvalidOperationException ex)
        {
            // Geçersiz adres (ör. şeması olmayan uç nokta): beklemekle düzelmez.
            throw new SpeechApiException(ex.Message, transient: false, ex);
        }

        throw new SpeechApiException(OpenAiProtocol.ErrorMessage(status, body), OpenAiProtocol.IsTransient(status));
    }
}
