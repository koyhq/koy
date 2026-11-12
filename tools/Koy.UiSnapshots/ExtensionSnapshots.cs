using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Windows;
using System.Windows.Threading;
using Koy.App.Features.Extensions;
using Koy.App.Features.Settings;
using Koy.App.Resources;
using Koy.Core.Settings;
using Koy.Core.Settings.Profiles;
using Koy.Core.Extensions.Store;
using Koy.Platform.Extensions;

namespace Koy.UiSnapshots;

/// <summary>
/// Koy penceresinin Eklentiler sayfasını çizer. Ağ yok: indeks, önizleme ve paketler depo kökündeki <c>store/</c>
/// klasöründen sahte bir <see cref="HttpMessageHandler"/> ile okunur (gerçek adresler aynen kullanılır, yalnız yanıtı
/// yerel dosya verir). Eklenti klasörü, kayıt, ayarlar, profiller, önbellek ve kullanıcı stili geçici klasörde;
/// gerçek %AppData%'ya hiçbir şey yazılmaz. Yalnız bu araç için; uygulamanın kendisinde böyle bir yol yoktur.
/// </summary>
internal static class ExtensionSnapshots
{
    private const string RawPrefix = "/koyhq/koy/main/";

    private sealed class RepoHandler(string repoRoot, bool offline) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? "";
            if (offline || !path.StartsWith(RawPrefix, StringComparison.Ordinal))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));

            var file = Path.GetFullPath(Path.Combine(repoRoot, path[RawPrefix.Length..].Replace('/', Path.DirectorySeparatorChar)));
            if (!file.StartsWith(repoRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(file))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(File.ReadAllBytes(file)) });
        }
    }

    private static string? FindRepoRoot()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
            {
                if (File.Exists(Path.Combine(dir.FullName, "store", "index.json")))
                    return dir.FullName;
            }
        }

        return null;
    }

    private sealed record Rig(KoyWindow Window, KoyWindowViewModel Vm, ExtensionStoreViewModel Ext, string TempDir);

    private static Rig Build(string repoRoot, bool offline)
    {
        var temp = Path.Combine(Path.GetTempPath(), "koy-uisnap-ext-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);

        var store = new SettingsStore(Path.Combine(temp, "settings.json"));
        store.Save(new AppSettings
        {
            General = new GeneralSettings { FirstRunCompleted = true },
            Appearance = new AppearanceSettings { Material = Program.Material },
        });

        var http = new HttpClient(new RepoHandler(repoRoot, offline));
        var client = new ExtensionStoreClient(
            new InstalledRegistryStore(Path.Combine(temp, "extensions.json")), Path.Combine(temp, "extensions"), http);
        var previews = new StorePreviewLoader(http, Path.Combine(temp, "store-cache"));
        var profiles = new ProfileService(store, new ProfileStore(Path.Combine(temp, "profiles.json")));
        var ext = new ExtensionStoreViewModel(client, previews, profiles, Path.Combine(temp, "user-style"));

        var vm = new KoyWindowViewModel(store, ext);
        var win = new KoyWindow(vm);
        if (win.FindName("ContentScrollViewer") is System.Windows.Controls.ScrollViewer sv)
            sv.VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Disabled;
        ((FrameworkElement)win.Content).Measure(new Size(880, double.PositiveInfinity));
        return new Rig(win, vm, ext, temp);
    }

    private static void Pump()
    {
        for (var i = 0; i < 3; i++)
        {
            Thread.Sleep(30);
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
        }
    }

    // Eşzamansız işler (indeks, önizleme, kurulum) iş parçacığı havuzunda biter; devamı arayüz iş parçacığına döner.
    private static void WaitUntil(Func<bool> done, int timeoutMs = 15000)
    {
        var until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!done() && DateTime.UtcNow < until)
        {
            Thread.Sleep(25);
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
        }

        Pump();
    }

    private static void LoadStore(Rig rig)
    {
        rig.Vm.SelectPage(KoyWindowViewModel.ExtensionsPageIndex);
        rig.Ext.SetActive(true);
        WaitUntil(() => !rig.Ext.IsLoading && rig.Ext.Cards.Count > 0);
        WaitUntil(() => rig.Ext.Cards.All(c => c.HasPreview || c.Item?.Preview is null), 5000);
    }

    private static StoreCardViewModel Card(Rig rig, string id) => rig.Ext.Cards.First(c => c.Id == id);

    private static void Install(Rig rig, string id)
    {
        Card(rig, id).InstallCommand.Execute(null);
        rig.Ext.ConfirmInstallCommand.Execute(null);
        WaitUntil(() => Card(rig, id).ShowUninstall);
    }

    public static void Render(string outDir, string prefix, string theme, Action<FrameworkElement, double, double, string> render)
    {
        var repoRoot = FindRepoRoot();
        if (repoRoot is null)
        {
            Console.WriteLine("store/index.json bulunamadi; Eklentiler goruntuleri atlandi.");
            return;
        }

        var previous = CultureInfo.CurrentUICulture;
        var dispatcherContext = SynchronizationContext.Current;
        // Dispatcher bağlamı: await devamları arayüz iş parçacığına dönsün.
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        var temps = new List<string>();
        try
        {
            string Name(string n) => Path.Combine(outDir, $"{prefix}-{theme}-koy-eklentiler-{n}.png");

            CultureInfo.CurrentUICulture = new CultureInfo("tr");
            Loc.Instance.Refresh();

            var rig = Build(repoRoot, offline: false);
            temps.Add(rig.TempDir);
            var root = (FrameworkElement)rig.Window.Content;
            LoadStore(rig);
            render(root, 880, 680, Name("liste"));

            // İngilizce
            CultureInfo.CurrentUICulture = new CultureInfo("en");
            Loc.Instance.Refresh();
            rig.Ext.RefreshLanguage();
            Pump();
            render(root, 880, 680, Name("liste-en"));
            CultureInfo.CurrentUICulture = new CultureInfo("tr");
            Loc.Instance.Refresh();
            rig.Ext.RefreshLanguage();

            // Ayrıntı açık
            Card(rig, "koyhq.nord-theme").IsExpanded = true;
            Pump();
            render(root, 880, 680, Name("ayrinti"));
            Card(rig, "koyhq.nord-theme").IsExpanded = false;

            // Kurulum onayı
            Card(rig, "koyhq.nord-theme").InstallCommand.Execute(null);
            Pump();
            render(root, 880, 680, Name("onay"));
            rig.Ext.CancelConfirmCommand.Execute(null);

            // Kurulu + uygulanmış
            Install(rig, "koyhq.nord-theme");
            Install(rig, "koyhq.mono-icons");
            Install(rig, "koyhq.minimal-profile");
            Card(rig, "koyhq.nord-theme").ApplyCommand.Execute(null);
            Card(rig, "koyhq.mono-icons").ApplyCommand.Execute(null);
            Card(rig, "koyhq.minimal-profile").ApplyCommand.Execute(null);
            Pump();
            render(root, 880, 680, Name("kurulu"));

            // Yalnız kurulu süzgeci, kaldırma onayı bir kartta
            rig.Ext.FilterKey = "Installed";
            Card(rig, "koyhq.mono-icons").RequestUninstallCommand.Execute(null);
            Pump();
            render(root, 880, 680, Name("kurulu-sure"));
            Card(rig, "koyhq.mono-icons").CancelUninstallCommand.Execute(null);
            rig.Ext.FilterKey = "All";

            // Kategori ve sıralama
            rig.Ext.CategoryKey = "Theme";
            rig.Ext.SortKey = "Alphabetical";
            Pump();
            render(root, 880, 680, Name("kategori"));
            rig.Ext.CategoryKey = "All";
            rig.Ext.SortKey = "Newest";

            // İndirme sürerken (sahte ilerleme)
            var busy = Card(rig, "koyhq.aurora-background");
            busy.ShowInstall = false;
            busy.IsBusy = true;
            busy.Progress = 0.62;
            Pump();
            render(root, 880, 680, Name("indiriliyor"));
            busy.IsBusy = false;
            busy.ShowInstall = true;
            busy.Progress = 0;

            // Arama sonucu yok
            rig.Ext.SearchText = "zzz";
            Pump();
            render(root, 880, 680, Name("bos"));
            rig.Ext.SearchText = "";

            rig.Ext.SetActive(false);

            // Çevrimdışı: katalog alınamadı
            var offline = Build(repoRoot, offline: true);
            temps.Add(offline.TempDir);
            offline.Vm.SelectPage(KoyWindowViewModel.ExtensionsPageIndex);
            offline.Ext.SetActive(true);
            WaitUntil(() => !offline.Ext.IsLoading);
            render((FrameworkElement)offline.Window.Content, 880, 680, Name("hata"));
            offline.Ext.SetActive(false);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
            Loc.Instance.Refresh();
            SynchronizationContext.SetSynchronizationContext(dispatcherContext);
            foreach (var temp in temps)
            {
                try { Directory.Delete(temp, true); } catch { }
            }
        }
    }
}
