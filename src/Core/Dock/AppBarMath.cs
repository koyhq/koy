using Koy.Core.Display;
using Koy.Core.Shell;

namespace Koy.Core.Dock;

/// <summary>
/// AppBar'ın alt kenardaki son alanı. ABM_QUERYPOS, önerilen alanı alt kenardaki öteki çubukların (Gezgin'in 48 px'lik
/// görev çubuğu) üstüne iter. Windows görev çubuğu gizliyken alt kenar Koy'undur; itilen alan kabul edilirse pay 94 px
/// (46 + 48) olur ve çalışma alanında boşluk kalır (Gezgin henüz otomatik gizlemeye geçmemişken kayıt yapılınca olur).
/// </summary>
public static class AppBarMath
{
    /// <summary>
    /// Windows görev çubuğu gizlenirken ABS_AUTOHIDE durumunda bırakılır; Chromium/Electron/WinUI pencereleri
    /// büyütülünce alt kenarda otomatik gizlenen çubuğu algılayıp çalışma alanının altından 2 px keser
    /// (kAutoHideTaskbarThicknessPx = 2). Bu pay ayrılan alandan düşülünce büyütülmüş pencereler dock'un
    /// üst kenarına boşluksuz dayanır.
    /// </summary>
    public const int AutoHideBottomCompensationPx = 2;

    /// <summary>
    /// AppBar'ın ayıracağı kalınlık (piksel): dock penceresinin kalınlığı ile kenar boşluğu, pencerenin yerleşiminde
    /// (<see cref="WindowPlacementMath.EdgeBar"/>, <see cref="WindowPlacementMath.EdgeCapsule"/>) kullanılan AYNI kuralla
    /// ayrı ayrı piksele çevrilip toplanır. Böylece büyütülmüş pencerenin altı dock'un üst kenarına tam dayanır:
    /// dock boşluk bırakmaz, pencereyi de ezmez (%125, %175 gibi kesirli ölçekte toplamı yuvarlamak 1 px fark üretirdi).
    /// </summary>
    /// <param name="thicknessDip">Dock'un kenara dik kalınlığı (DIP).</param>
    /// <param name="edgeOffsetDip">Dock ile ekran kenarı arasındaki boşluk (DIP).</param>
    /// <param name="dpiScale">Ekranın ölçeği (1,25 = %125).</param>
    public static int ReservedPixels(double thicknessDip, double edgeOffsetDip, double dpiScale) =>
        WindowPlacementMath.ToPixels(thicknessDip, dpiScale)
        + WindowPlacementMath.ToPixels(DockLayoutLimits.ClampEdgeOffsetDip(edgeOffsetDip), dpiScale);

    /// <summary>
    /// Alt kenarda görev çubuğu Koy'dayken (<paramref name="ownsBottomEdge"/>) Windows'taki gizli görev çubuğunun
    /// ABS_AUTOHIDE kayması (<see cref="AutoHideBottomCompensationPx"/>) ayrılan paydan düşülür.
    /// </summary>
    public static int ReservedPixels(double thicknessDip, double edgeOffsetDip, double dpiScale, DockEdge edge, bool ownsBottomEdge)
    {
        var raw = ReservedPixels(thicknessDip, edgeOffsetDip, dpiScale);
        return ownsBottomEdge && edge == DockEdge.Bottom
            ? Math.Max(1, raw - AutoHideBottomCompensationPx)
            : raw;
    }

    /// <param name="edge">Dock'un oturduğu ekran kenarı.</param>
    /// <param name="monitor">Monitörün tam sınırları (piksel).</param>
    /// <param name="thicknessPx">AppBar kalınlığı (piksel; yatayda yükseklik, dikeyde genişlik).</param>
    /// <param name="queried">ABM_QUERYPOS'un döndürdüğü alan.</param>
    /// <param name="ownsBottomEdge">
    /// Windows görev çubuğu gizli (ya da gizlenecek): Gezgin'in çubuğu hiç sayılmaz, alan her kenarda monitörün kendi
    /// kenarından hesaplanır. Aksi halde (görev çubuğu görünür) alan Windows'un önerdiği sınırın üstüne yığılır.
    /// </param>
    public static WindowRect FinalRect(DockEdge edge, WindowRect monitor, int thicknessPx, WindowRect queried, bool ownsBottomEdge)
    {
        if (!Enum.IsDefined(edge)) edge = DockEdge.Bottom;

        // Görev çubuğu bizdeyken ABM_QUERYPOS'un önerisi (Gezgin'in 48 DIP'lik çubuğu yüzünden yukarı itilmiş alt sınır,
        // ya da dikey kenarlarda kısalmış yükseklik) yok sayılır: yoksa pay çalışma alanında boşluk bırakır.
        if (ownsBottomEdge)
        {
            return edge switch
            {
                DockEdge.Top => new WindowRect(monitor.Left, monitor.Top, monitor.Right, monitor.Top + thicknessPx),
                DockEdge.Left => new WindowRect(monitor.Left, monitor.Top, monitor.Left + thicknessPx, monitor.Bottom),
                DockEdge.Right => new WindowRect(monitor.Right - thicknessPx, monitor.Top, monitor.Right, monitor.Bottom),
                _ => new WindowRect(monitor.Left, monitor.Bottom - thicknessPx, monitor.Right, monitor.Bottom)
            };
        }

        return edge switch
        {
            DockEdge.Top => new WindowRect(queried.Left, queried.Top, queried.Right, queried.Top + thicknessPx),
            DockEdge.Left => new WindowRect(queried.Left, queried.Top, queried.Left + thicknessPx, queried.Bottom),
            DockEdge.Right => new WindowRect(queried.Right - thicknessPx, queried.Top, queried.Right, queried.Bottom),
            // Görev çubuğu görünürken: Windows'un önerdiği alt sınırın hemen üstüne yığılır.
            _ => new WindowRect(queried.Left, queried.Bottom - thicknessPx, queried.Right, queried.Bottom)
        };
    }

    /// <param name="monitor">Monitörün tam sınırları (piksel).</param>
    /// <param name="heightPx">AppBar yüksekliği (piksel).</param>
    /// <param name="queried">ABM_QUERYPOS'un döndürdüğü alan.</param>
    /// <param name="ownsBottomEdge">Windows görev çubuğu gizli: alt kenar Koy'un, Gezgin'in itmesi yok sayılır.</param>
    public static WindowRect FinalRect(WindowRect monitor, int heightPx, WindowRect queried, bool ownsBottomEdge)
        => FinalRect(DockEdge.Bottom, monitor, heightPx, queried, ownsBottomEdge);

    /// <summary>
    /// Aynı alanı tekrar ISTEMEK Windows'ta çalışma alanını yeniden hesaplatmaz: SETPOS aynı dikdörtgenle gelirse Gezgin değişiklik
    /// görmez. Gezgin otomatik gizlenen görev çubuğu yüzünden çalışma alanını sıfırladıktan sonra (kayıt, görev çubuğu görünürken
    /// yapılmıştı) payı geri getirmek için önce kalınlığı 1 piksel büyütülmüş geçici bir alan, ardından gerçek alan gönderilir.
    /// Bu fonksiyon geçici alanı üretir: dock'un oturduğu kenar sabit, iç kenar 1 px içeri kayar.
    /// </summary>
    public static WindowRect NudgedRect(DockEdge edge, WindowRect rect) => edge switch
    {
        DockEdge.Top => rect with { Bottom = rect.Bottom + 1 },
        DockEdge.Left => rect with { Right = rect.Right + 1 },
        DockEdge.Right => rect with { Left = rect.Left - 1 },
        _ => rect with { Top = rect.Top - 1 }
    };
}
