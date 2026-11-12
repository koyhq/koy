using System.IO;
using System.Windows.Media;
using Koy.Core.Appearance;
using Koy.Core.Dock;
using Koy.Core.Settings;

namespace Koy.App.Features.Dock;

/// <summary>
/// Dock öğesinin simgesini <see cref="IconResolver"/> kararına göre yükler: uygulamanın kendi değişimi, simge paketi,
/// yoksa null (çağıran yerleşik simgeyi kullanır). Karar Core'da; burada yalnız dosya listesi ve resim yükleme var.
/// Paket klasörü ilk ihtiyaçta bir kez listelenir ve bu örnek yaşadığı sürece önbellekte kalır; ayar değişince dock
/// ViewModel'i yeniden kurulduğundan (DockSettingsDiff) örnek de yenilenir. Resimler gösterim boyutunda çözülüp dondurulur
/// (DockStartIconLoader.LoadFile). Okunamayan ya da bozuk dosya sessizce yerleşik simgeye düşer.
/// </summary>
internal sealed class DockAppIcons
{
    // Dev bir klasör belleği şişirmesin: ilk bu kadar dosya adı listelenir.
    private const int MaxPackFiles = 5000;

    private readonly IconPack _pack;
    private readonly string _folder;
    private readonly IReadOnlyDictionary<string, string> _overrides;
    private readonly Dictionary<string, ImageSource?> _loaded = new(StringComparer.OrdinalIgnoreCase);
    private string[]? _packFiles;

    public DockAppIcons(AppSettings settings)
    {
        _pack = settings.Appearance.IconPack;
        _overrides = settings.Appearance.AppIconOverrides ?? new ValueDictionary<string>();
        _folder = IconPackFiles.IsSafeFolder(settings.Appearance.IconPackFolder) ? settings.Appearance.IconPackFolder.Trim() : "";
    }

    /// <summary>Hiçbir değişim ve paket yoksa yükleme yolu hiç çalışmaz.</summary>
    public bool IsActive => (_pack == IconPack.Folder && _folder.Length > 0) || _overrides.Count > 0;

    /// <summary>Öğenin ayarlardaki kendi simge değişimi var mı ("Simgeyi sıfırla" bunu gerektirir).</summary>
    public bool HasOverride(DockItem item)
        => AppIconKey.All(item.AppUserModelId, item.Id).Any(k => AppIconOverrides.Lookup(_overrides, k) is not null);

    /// <summary>Öğe için değişim ya da paket simgesi; yoksa ya da yüklenemezse null.</summary>
    public ImageSource? Resolve(DockItem item)
    {
        if (!IsActive)
            return null;

        var choice = Choose(AppIconKey.All(item.AppUserModelId, item.Id));
        var path = choice.Kind switch
        {
            IconSourceKind.AppOverride => ToFullPath(choice.Value),
            IconSourceKind.Pack => _folder.Length > 0 && choice.Value is { } name ? Path.Combine(_folder, name) : null,
            _ => null
        };

        if (path is null || !IsRasterFile(path))
            return null;

        if (!_loaded.TryGetValue(path, out var image))
        {
            image = DockStartIconLoader.LoadFile(path);
            _loaded[path] = image;
        }

        return image;
    }

    // Önce herhangi bir anahtarın kendi değişimi, sonra paket; ikisi de yoksa yerleşik.
    private IconChoice Choose(IReadOnlyList<string> keys)
    {
        foreach (var key in keys)
        {
            var overridden = IconResolver.Resolve(key, _overrides, IconPack.Builtin, null);
            if (overridden.Kind == IconSourceKind.AppOverride)
                return overridden;
        }

        if (_pack != IconPack.Folder || _folder.Length == 0)
            return new IconChoice(IconSourceKind.Default, null);

        foreach (var key in keys)
        {
            var packIcon = IconPackFiles.Find(PackFiles(), key, allowSvg: false);
            var choice = IconResolver.Resolve(key, null, _pack, packIcon);
            if (choice.Kind == IconSourceKind.Pack)
                return choice;
        }

        return new IconChoice(IconSourceKind.Default, null);
    }

    // Değer yalnız dosya adıysa paket klasörüne göre çözülür (Core klasör bilmez).
    private string? ToFullPath(string? value)
    {
        if (value is null)
            return null;

        if (Path.IsPathRooted(value))
            return value;

        return _folder.Length > 0 ? Path.Combine(_folder, value) : null;
    }

    private string[] PackFiles()
    {
        if (_packFiles is not null)
            return _packFiles;

        try
        {
            _packFiles = Directory.EnumerateFiles(_folder)
                .Select(Path.GetFileName)
                .OfType<string>()
                .Take(MaxPackFiles)
                .ToArray();
        }
        catch (Exception)
        {
            // Klasör silinmiş, ağ yolu düşmüş ya da erişim yok: paket boş sayılır.
            _packFiles = [];
        }

        return _packFiles;
    }

    // WPF SVG çizemez (yeni paket eklenmez); seçici de yalnız PNG/ICO sunar.
    private static bool IsRasterFile(string path)
        => Path.GetExtension(path) is { } ext
           && (ext.Equals(".png", StringComparison.OrdinalIgnoreCase) || ext.Equals(".ico", StringComparison.OrdinalIgnoreCase));

}
