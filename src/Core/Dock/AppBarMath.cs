using Koy.Core.Shell;

namespace Koy.Core.Dock;

/// <summary>
/// AppBar'ın alt kenardaki son alanı. ABM_QUERYPOS, önerilen alanı alt kenardaki öteki çubukların (Gezgin'in 48 px'lik
/// görev çubuğu) üstüne iter. Windows görev çubuğu gizliyken alt kenar Koy'undur; itilen alan kabul edilirse pay 94 px
/// (46 + 48) olur ve çalışma alanında boşluk kalır (Gezgin henüz otomatik gizlemeye geçmemişken kayıt yapılınca olur).
/// </summary>
public static class AppBarMath
{
    /// <param name="monitor">Monitörün tam sınırları (piksel).</param>
    /// <param name="heightPx">AppBar yüksekliği (piksel).</param>
    /// <param name="queried">ABM_QUERYPOS'un döndürdüğü alan.</param>
    /// <param name="ownsBottomEdge">Windows görev çubuğu gizli: alt kenar Koy'un, Gezgin'in itmesi yok sayılır.</param>
    public static WindowRect FinalRect(WindowRect monitor, int heightPx, WindowRect queried, bool ownsBottomEdge)
    {
        if (ownsBottomEdge)
            return new WindowRect(monitor.Left, monitor.Bottom - heightPx, monitor.Right, monitor.Bottom);

        // Görev çubuğu görünürken: Windows'un önerdiği alt sınırın hemen üstüne yığılır.
        return new WindowRect(queried.Left, queried.Bottom - heightPx, queried.Right, queried.Bottom);
    }
}
