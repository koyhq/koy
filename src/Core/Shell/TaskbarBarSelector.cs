namespace Koy.Core.Shell;

/// <summary>Bir görev çubuğu penceresi: tutamacı ve bulunduğu monitörün tutamacı (MonitorFromWindow).</summary>
public readonly record struct TaskbarBar(nint Handle, nint MonitorHandle);

/// <summary>
/// Hangi Windows görev çubuğunun gizleneceği (saf mantık). Dock yalnız bazı monitörlerde açıksa dock'suz
/// monitörün çubuğu görünür kalır; orada açık pencereleri yönetecek başka bir şey yoktur.
/// Not: AUTOHIDE (ABM_SETSTATE) Windows'ta bütün çubuklar için ortaktır; görünür bırakılan çubuk otomatik gizleme
/// davranışını gösterebilir. Bu mantık yalnız Koy'un SW_HIDE ile gizlediği çubukları seçer.
/// </summary>
public static class TaskbarBarSelector
{
    /// <summary>Gizlenecek çubukların tutamaçları. Monitörü bilinmeyen (0) çubuk güvenli tarafta görünür kalır.</summary>
    public static IReadOnlyList<nint> SelectBarsToHide(IReadOnlyList<TaskbarBar> bars, IReadOnlyCollection<nint> dockMonitors)
    {
        var result = new List<nint>(bars.Count);
        foreach (var bar in bars)
        {
            if (bar.MonitorHandle != 0 && dockMonitors.Contains(bar.MonitorHandle))
                result.Add(bar.Handle);
        }

        return result;
    }
}
