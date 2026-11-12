using Koy.Core.Dock.Items;

namespace Koy.App.Features.Dock;

/// <summary>
/// Özel düğme için hedef seçtirir (dosya iletişim kutusu). Yalnız var olan bir dosya seçilir; sonuç tırnaklı tam yoldur
/// ve kaydedilmeden önce Core'da (DockCustomCommand) doğrulanır. Bağımsız değişken, adres ve klasör hedefleri
/// şimdilik ayar dosyasından elle girilir.
/// </summary>
internal static class DockCustomButtonPicker
{
    public static string? PickCommand()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = Resources.Strings.Get("DockItemCustomPick"),
            CheckFileExists = true,
            Multiselect = false,
        };

        if (dialog.ShowDialog() != true)
            return null;

        var command = "\"" + dialog.FileName + "\"";
        return DockCustomCommand.TryParse(command, out _) ? command : null;
    }
}
