using Koy.Core.Search;

namespace Koy.Core.StartMenu;

/// <summary>Bir aracı başlatma denemesinin sonucu.</summary>
public enum ToolLaunchResult
{
    Started,
    /// <summary>Dosya yok ya da kabukta tanımlı değil (ör. Windows Terminal kurulu değil).</summary>
    NotFound,
    /// <summary>Kullanıcı UAC istemini reddetti; hata değildir.</summary>
    Declined,
    Failed
}

/// <summary>Başlat sağ tık menüsünün Windows tarafı. Uygulaması Platform'da; WinRT/Win32 tipi sızmaz.</summary>
public interface IStartMenuHost
{
    /// <summary>Makinede pil var mı (Mobilite Merkezi yalnız o zaman gösterilir).</summary>
    bool HasBattery { get; }

    /// <summary>Hedefi kabuk üzerinden açar. Hata fırlatmaz, sonucu döner.</summary>
    ToolLaunchResult Launch(string target, string? arguments, bool elevated);

    void ShowDesktop();

    void OpenSearch();

    void OpenRunDialog();
}

/// <summary>
/// Menü öğesine tıklanınca yapılacak iş. Hiçbir hata dışarı çıkmaz: dock bir başlatma hatasıyla çökmemeli.
/// </summary>
public sealed class StartMenuActions(IStartMenuHost host, ISystemCommandService session, Action<string, Exception>? onError = null)
{
    private const string WindowsTerminal = "wt.exe";
    private const string PowerShell = "powershell.exe";

    public IReadOnlyList<StartMenuGroup> BuildMenu()
    {
        try
        {
            return StartMenuModel.Build(host.HasBattery);
        }
        catch (Exception ex)
        {
            // Pil sorgusu patlarsa menü yine açılsın: pilsiz varsayılır.
            onError?.Invoke("Başlat menüsü kurulumu", ex);
            return StartMenuModel.Build(false);
        }
    }

    public void Execute(StartMenuItem item)
    {
        try
        {
            switch (item.Kind)
            {
                case StartMenuActionKind.OpenTarget when item.Target is { Length: > 0 } target:
                    host.Launch(target, null, elevated: false);
                    break;
                case StartMenuActionKind.Terminal:
                    LaunchTerminal(elevated: false);
                    break;
                case StartMenuActionKind.TerminalAdmin:
                    LaunchTerminal(elevated: true);
                    break;
                case StartMenuActionKind.Session when item.Session is { } command:
                    session.Execute(command);
                    break;
                case StartMenuActionKind.ShowDesktop:
                    host.ShowDesktop();
                    break;
                case StartMenuActionKind.Search:
                    host.OpenSearch();
                    break;
                case StartMenuActionKind.Run:
                    host.OpenRunDialog();
                    break;
            }
        }
        catch (Exception ex)
        {
            onError?.Invoke("Başlat menüsü: " + item.Id, ex);
        }
    }

    // Reddedilen UAC istemi sessizce biter; başka her başarısızlıkta PowerShell'e düşülür.
    private void LaunchTerminal(bool elevated)
    {
        var result = host.Launch(WindowsTerminal, null, elevated);
        if (result is ToolLaunchResult.Started or ToolLaunchResult.Declined)
            return;

        host.Launch(PowerShell, null, elevated);
    }
}
