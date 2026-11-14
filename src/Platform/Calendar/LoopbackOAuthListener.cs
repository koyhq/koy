using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Koy.Core.Calendar;

namespace Koy.Platform.Calendar;

/// <summary>
/// Yerel loopback (<c>http://127.0.0.1:{port}/callback/</c>) üzerinden OAuth 2.0 yetkilendirme kodunu bekler.
/// </summary>
public static class LoopbackOAuthListener
{
    public static int AllocateFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    public static async Task<OAuthCallbackResult> AuthorizeAsync(
        CalendarProvider provider,
        string clientId,
        string codeChallenge,
        string state,
        string redirectUri,
        TimeSpan timeout,
        CancellationToken ct)
    {
        using var http = new HttpListener();
        http.Prefixes.Add(redirectUri);
        http.Start();

        var authUrl = OAuthPkce.BuildAuthorizationUrl(provider, clientId, redirectUri, codeChallenge, state);
        Process.Start(new ProcessStartInfo(authUrl.AbsoluteUri) { UseShellExecute = true });

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);

        try
        {
            var contextTask = http.GetContextAsync();
            var completed = await Task.WhenAny(contextTask, Task.Delay(Timeout.Infinite, timeoutCts.Token)).ConfigureAwait(false);
            if (completed != contextTask)
                return OAuthCallbackResult.Fail(ct.IsCancellationRequested ? "cancelled" : "timeout");

            var context = await contextTask.ConfigureAwait(false);
            var result = OAuthPkce.ValidateCallback(context.Request.Url, state);
            await WriteHtmlResponseAsync(context.Response, result.Success).ConfigureAwait(false);
            return result;
        }
        catch (OperationCanceledException)
        {
            return OAuthCallbackResult.Fail(ct.IsCancellationRequested ? "cancelled" : "timeout");
        }
        finally
        {
            try
            {
                http.Stop();
            }
            catch
            {
            }
        }
    }

    private static async Task WriteHtmlResponseAsync(HttpListenerResponse response, bool success)
    {
        var title = success ? "Koy" : "Koy";
        var heading = success ? "Bağlantı tamamlandı / Connected" : "Yetkilendirme tamamlanamadı / Authorization failed";
        var body = success
            ? "Bu sekmeyi kapatıp Koy uygulamasına dönebilirsiniz. / You can close this tab and return to Koy."
            : "Lütfen Koy ayarlarından yeniden deneyin. / Please try again from Koy settings.";

        var html = $$"""
            <!DOCTYPE html>
            <html lang="tr">
            <head><meta charset="utf-8"><title>{{title}}</title>
            <style>body{font-family:Segoe UI,sans-serif;background:#111;color:#f4f4f5;display:flex;align-items:center;justify-content:center;height:100vh;margin:0}
            .card{background:#1c1c1f;border:1px solid #2e2e33;border-radius:14px;padding:28px 32px;max-width:420px;text-align:center}
            h1{font-size:18px;margin:0 0 8px}p{font-size:14px;color:#a1a1aa;margin:0;line-height:1.5}</style></head>
            <body><div class="card"><h1>{{heading}}</h1><p>{{body}}</p></div></body></html>
            """;

        var bytes = Encoding.UTF8.GetBytes(html);
        response.StatusCode = success ? 200 : 400;
        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        response.Close();
    }
}
