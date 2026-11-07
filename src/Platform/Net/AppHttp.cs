using System.Net.Http;

namespace Koy.Platform.Net;

/// <summary>
/// Uygulamanın tek <see cref="HttpClient"/> örneği (hava durumu ve şehir araması paylaşır). Her çağrıda yeni
/// HttpClient yaratmak soket tüketir; ilk kullanımda kurulur, açılışta hiçbir bağlantı açılmaz.
/// </summary>
public static class AppHttp
{
    public static HttpClient Client { get; } = Create();

    private static HttpClient Create()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Koy (+https://github.com/koyhq/koy)");
        return client;
    }
}
