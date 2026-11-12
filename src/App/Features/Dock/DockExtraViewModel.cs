using CommunityToolkit.Mvvm.ComponentModel;
using Koy.Core.Dock.Items;

namespace Koy.App.Features.Dock;

/// <summary>
/// Dock şeridindeki ek öğenin (ayırıcı, boşluk, saat, çöp kutusu, özel düğme) görünüm modeli. Yalnız durum taşır:
/// ne zaman çalışacağı, hangi komutun geçerli olduğu ve saat metni Core'da (DockItemLayout, DockCustomCommand, DockClockTick).
/// Tıklamayı DockViewModel.ActivateExtra yürütür.
/// </summary>
public sealed partial class DockExtraViewModel : DockEntryViewModel
{
    private const string TrashGlyph = "";
    private const string CustomGlyph = "";

    private string _title = string.Empty;

    public DockExtraViewModel(DockItemSpec spec, string id, int layoutIndex)
    {
        Spec = spec;
        EntryId = id;
        LayoutIndex = layoutIndex;
    }

    public DockItemSpec Spec { get; private set; }

    public DockItemKind Kind => Spec.Kind;

    /// <summary>Öğenin şeritteki ve kayıtlı düzendeki sırası (taşıma ve kaldırma bununla yapılır).</summary>
    public int LayoutIndex { get; set; }

    private string EntryId { get; }

    public override string Id => EntryId;

    public override string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public override bool IsPinned
    {
        get => false;
        set { }
    }

    public override bool IsRunning
    {
        get => false;
        set { }
    }

    public bool IsSeparator => Kind == DockItemKind.Separator;
    public bool IsSpacer => Kind == DockItemKind.Spacer;
    public bool IsClock => Kind == DockItemKind.Clock;
    public bool IsGlyphButton => Kind is DockItemKind.Trash or DockItemKind.Custom;

    /// <summary>Tıklanabilir mi (ayırıcı ve boşluk değil).</summary>
    public bool IsInteractive => Kind is not (DockItemKind.Separator or DockItemKind.Spacer);

    public string Glyph => Kind == DockItemKind.Trash ? TrashGlyph : CustomGlyph;

    [ObservableProperty]
    private string _timeText = string.Empty;

    [ObservableProperty]
    private string _dateText = string.Empty;

    public void Update(DockItemSpec spec, int layoutIndex)
    {
        Spec = spec;
        LayoutIndex = layoutIndex;
    }
}
