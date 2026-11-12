using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Koy.App.Resources;

namespace Koy.App.Features.Settings;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "Tek örnek, süreç boyu yaşar; kapatınca gizlenir (AGENTS.md #4).")]
public partial class KoyWindow : Window
{
    private static KoyWindow? _instance;

    public static KoyWindow Instance => _instance ??= new KoyWindow();

    /// <summary>KoyWindow örneğinin bellekte oluşturulup oluşturulmadığını belirtir.</summary>
    public static bool IsCreated => _instance is not null;

    /// <summary>
    /// Sağ alt menü veya kısayol üzerinden değişen özellikleri açık/gizli KoyWindow örneğine iletir.
    /// Pencere henüz oluşturulmamışsa hiçbir şey yapmaz.
    /// </summary>
    public static void SyncFeatureStates(bool? notchEnabled = null, bool? dockEnabled = null)
    {
        if (_instance is null)
            return;

        if (!_instance.Dispatcher.CheckAccess())
        {
            _instance.Dispatcher.BeginInvoke(() => SyncFeatureStates(notchEnabled, dockEnabled));
            return;
        }

        _instance.ViewModel.SyncExternalState(notchEnabled, dockEnabled);
    }

    public KoyWindowViewModel ViewModel => (KoyWindowViewModel)DataContext;

    // user-theme.json izleyicisi yalnız pencere görünürken yaşar (AGENTS #5); gizlenince bırakılır.
    private UserStyleWatcher? _userStyleWatcher;

    public KoyWindow(KoyWindowViewModel? viewModel = null)
    {
        InitializeComponent();
        DataContext = viewModel ?? new KoyWindowViewModel();
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        ViewModel.SettingsSaved += ApplyChangedMarks;
        // Tek örnek, hiç yok edilmez: olay aboneliği sızıntı yapmaz.
        ThemeManager.Applied += ApplyUiScale;
        ApplyUiScale();
        IsVisibleChanged += OnIsVisibleChanged;
    }

    // --- Ayar araması ve "yalnız değişenler" (issue #69) ---

    private SettingsPageIndex? _index;
    private DispatcherTimer? _highlightTimer;
    private FrameworkElement? _highlighted;

    // Dizin sayfaların kendisinden kurulur; dil değişince metinler yeni dilde okunsun diye yeniden kurulur.
    private SettingsPageIndex Index
    {
        get
        {
            if (_index is null || _index.Culture != System.Globalization.CultureInfo.CurrentUICulture.Name)
            {
                _index = SettingsPageIndex.Build(PagesHost);
                _sectionRows = null;
                _sectionExtras = null;
                _infoBoxes = null;
            }
            return _index;
        }
    }

    private void RunSearch()
    {
        var query = ViewModel.SettingsSearchQuery;
        if (string.IsNullOrWhiteSpace(query))
        {
            ViewModel.SettingsSearchResults = [];
            return;
        }

        var rows = Index.Rows;
        var byKey = rows.ToDictionary(r => r.Entry.Key);
        ViewModel.SettingsSearchResults = Koy.Core.Settings.SettingsSearch.Search(rows.Select(r => r.Entry), query, limit: 12)
            .Select(h => new SettingsSearchResult(h.Entry.Title, h.Entry.Page, byKey[h.Entry.Key]))
            .ToList();
    }

    private void SearchResult_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is SettingsSearchResult result)
            OpenResult(result);
    }

    private void SettingsSearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && ViewModel.IsSettingsSearching)
        {
            ViewModel.SettingsSearchQuery = "";
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && ViewModel.SettingsSearchResults.Count > 0)
        {
            OpenResult(ViewModel.SettingsSearchResults[0]);
            e.Handled = true;
        }
    }

    private void OpenResult(SettingsSearchResult result)
    {
        ViewModel.SettingsSearchQuery = "";
        // Süzgeç satırı gizliyorsa önce kapatılır; yoksa gidilen satır görünmezdi.
        if (ViewModel.ShowChangedOnly && SettingsRowState.GetIsFilteredOut(result.Row.Element))
            ViewModel.ShowChangedOnly = false;

        ViewModel.SelectPage(result.Row.PageIndex);
        ContentScrollViewer?.ScrollToTop();
        // Sayfa görünür olup ölçüldükten sonra satıra kaydırılır ve ~1,5 sn vurgulanır.
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => Highlight(result.Row.Element));
    }

    private void Highlight(FrameworkElement row)
    {
        ClearHighlight();
        row.BringIntoView();
        SettingsRowState.SetIsHighlighted(row, true);
        _highlighted = row;

        // Tek atımlık: süre dolunca vurgu kalkar ve zamanlayıcı durur (AGENTS #5).
        _highlightTimer ??= new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = TimeSpan.FromMilliseconds(1500) };
        _highlightTimer.Tick -= OnHighlightTick;
        _highlightTimer.Tick += OnHighlightTick;
        _highlightTimer.Stop();
        _highlightTimer.Start();
    }

    private void OnHighlightTick(object? sender, EventArgs e) => ClearHighlight();

    private void ClearHighlight()
    {
        _highlightTimer?.Stop();
        if (_highlighted is not null)
            SettingsRowState.SetIsHighlighted(_highlighted, false);
        _highlighted = null;
    }

    private void ChangedOnlyLabel_Click(object sender, MouseButtonEventArgs e)
    {
        // Kutunun kendisine tıklama zaten çevirir; yalnız metne tıklama buraya gelir.
        if (e.OriginalSource is System.Windows.Controls.TextBlock)
            ViewModel.ShowChangedOnly = !ViewModel.ShowChangedOnly;
    }

    private Dictionary<FrameworkElement, List<SettingsRow>>? _sectionRows;
    private Dictionary<FrameworkElement, List<FrameworkElement>>? _sectionExtras;
    private List<FrameworkElement>? _infoBoxes;

    private static IEnumerable<DependencyObject> WalkTree(DependencyObject root)
    {
        var stack = new Stack<DependencyObject>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            yield return current;
            var children = LogicalTreeHelper.GetChildren(current).OfType<DependencyObject>().Reverse();
            foreach (var child in children)
                stack.Push(child);
        }
    }

    private void EnsureSectionMap()
    {
        if (_sectionRows is not null && _sectionExtras is not null && _infoBoxes is not null)
            return;

        _sectionRows = new Dictionary<FrameworkElement, List<SettingsRow>>();
        _sectionExtras = new Dictionary<FrameworkElement, List<FrameworkElement>>();
        _infoBoxes = [];

        var headers = Index.Headers.ToHashSet();
        var rowByElement = Index.Rows.ToDictionary(r => r.Element);
        var infoStyle = Application.Current?.TryFindResource("InfoNoticeStyle") as Style;
        var descStyle = Application.Current?.TryFindResource("SettingDescStyle") as Style;
        var rowStyle = Application.Current?.TryFindResource("SettingRowStyle") as Style;

        for (var page = 0; page < PagesHost.Children.Count; page++)
        {
            var pageElement = PagesHost.Children[page];
            FrameworkElement? currentHeader = null;

            foreach (var obj in WalkTree(pageElement))
            {
                if (obj is not FrameworkElement el)
                    continue;

                // Bilgi kutuları (InfoNoticeStyle)
                if (el is System.Windows.Controls.Border infoBorder && infoBorder.Style == infoStyle && infoStyle is not null)
                {
                    _infoBoxes.Add(infoBorder);
                    continue;
                }

                // Bölüm başlıkları (SectionHeaderStyle)
                if (headers.Contains(el))
                {
                    currentHeader = el;
                    if (!_sectionRows.ContainsKey(currentHeader))
                        _sectionRows[currentHeader] = [];
                    if (!_sectionExtras.ContainsKey(currentHeader))
                        _sectionExtras[currentHeader] = [];
                    continue;
                }

                // Ayar satırları (SettingRowStyle)
                if (rowByElement.TryGetValue(el, out var row))
                {
                    if (currentHeader is not null)
                    {
                        if (!_sectionRows.TryGetValue(currentHeader, out var list))
                            _sectionRows[currentHeader] = list = [];
                        list.Add(row);
                    }
                    continue;
                }

                // Bölüm başlığı altındaki doğrudan başlık altı metinleri veya listeler
                if (currentHeader is not null && !headers.Contains(el) && !rowByElement.ContainsKey(el))
                {
                    // Satır içine gömülü öğeleri atla
                    if (IsInsideStyle(el, rowStyle))
                        continue;

                    if ((el is System.Windows.Controls.TextBlock tb && tb.Style == descStyle && descStyle is not null) ||
                        (el is System.Windows.Controls.ItemsControl ic && ic.Name != "SchemaItems"))
                    {
                        if (!_sectionExtras.TryGetValue(currentHeader, out var extras))
                            _sectionExtras[currentHeader] = extras = [];
                        extras.Add(el);
                    }
                }
            }
        }
    }

    private static bool IsInsideStyle(DependencyObject obj, Style? targetStyle)
    {
        if (targetStyle is null)
            return false;
        var parent = LogicalTreeHelper.GetParent(obj);
        while (parent is not null)
        {
            if (parent is FrameworkElement fe && fe.Style == targetStyle)
                return true;
            parent = LogicalTreeHelper.GetParent(parent);
        }
        return false;
    }

    private bool HasChangedSettingsOnPage(int pageIndex, HashSet<string> changed)
    {
        if (pageIndex < 0 || pageIndex >= PagesHost.Children.Count)
            return false;

        // Sayfa 9 (Gelişmiş): Özel arayüzü olmayan ayarlar
        if (pageIndex == KoyWindowViewModel.AdvancedPageIndex)
        {
            var dedicated = Koy.Core.Settings.SettingsKeyMap.V3ToV4.Values.Select(v => $"{v.Section}.{v.Name}").ToHashSet(StringComparer.Ordinal);
            return changed.Any(k => !dedicated.Contains(k));
        }

        // Sayfa 3 (Ekranlar): Ekran başına ayarlar veya yeni ekran varsayılanı
        if (pageIndex == 3 && changed.Contains("Displays.Monitors"))
            return true;

        // Sayfadaki satırlardan en az birinin ayar anahtarı değişenler kümesinde mi?
        return Index.Rows.Any(r => r.PageIndex == pageIndex && r.SettingKey is not null && changed.Contains(r.SettingKey));
    }

    private void ApplyPageCustomFilterState(bool only, HashSet<string> changed)
    {
        // Sayfa 0 (Genel): Değişen ayar yoksa profil/kural boş durum metinlerini ve alt blokları gizle
        if (PagesHost.Children.Count > 0 && PagesHost.Children[0] is FrameworkElement page0)
        {
            var hasChangedGeneral = HasChangedSettingsOnPage(0, changed);
            var rootPanel = LogicalTreeHelper.GetChildren(page0).OfType<System.Windows.Controls.Panel>().FirstOrDefault();
            if (rootPanel is not null)
            {
                for (var i = 2; i < rootPanel.Children.Count; i++)
                {
                    var child = rootPanel.Children[i] as FrameworkElement;
                    if (child is not null && child.Style != Application.Current?.TryFindResource("SectionHeaderStyle") && child.Style != Application.Current?.TryFindResource("SettingRowStyle"))
                    {
                        if (only && !hasChangedGeneral)
                            child.Visibility = Visibility.Collapsed;
                        else
                            child.ClearValue(VisibilityProperty);
                    }
                }
            }
        }

        // Sayfa 1 (Çentik): Hava durumu ayarı değişmemişse durum bildirim satırını gizle
        if (PagesHost.Children.Count > 1 && PagesHost.Children[1] is FrameworkElement page1)
        {
            var hasChangedWeather = changed.Any(k => k.StartsWith("Weather.", StringComparison.Ordinal));
            if (only && !hasChangedWeather)
            {
                var weatherRows = Index.Rows.Where(r => r.PageIndex == 1 && r.SettingKey is null).Select(r => r.Element);
                foreach (var el in weatherRows)
                    el.Visibility = Visibility.Collapsed;
            }
            else
            {
                var weatherRows = Index.Rows.Where(r => r.PageIndex == 1 && r.SettingKey is null).Select(r => r.Element);
                foreach (var el in weatherRows)
                    el.ClearValue(VisibilityProperty);
            }
        }

        // Sayfa 3 (Ekranlar): Değişen ayar yoksa şemayı ve seçim panelini gizle
        if (PagesHost.Children.Count > 3 && PagesHost.Children[3] is FrameworkElement page3)
        {
            var hasChangedDisplays = HasChangedSettingsOnPage(3, changed);
            var rootPanel = LogicalTreeHelper.GetChildren(page3).OfType<System.Windows.Controls.Panel>().FirstOrDefault();
            if (rootPanel is not null && rootPanel.Children.Count > 2)
            {
                var schemaBorder = rootPanel.Children[1] as FrameworkElement;
                var selectionPanel = rootPanel.Children[2] as FrameworkElement;
                var identifyBtn = (rootPanel.Children[0] as System.Windows.Controls.Grid)?.Children.OfType<System.Windows.Controls.Button>().FirstOrDefault();

                if (only && !hasChangedDisplays)
                {
                    if (schemaBorder is not null) schemaBorder.Visibility = Visibility.Collapsed;
                    if (selectionPanel is not null) selectionPanel.Visibility = Visibility.Collapsed;
                    if (identifyBtn is not null) identifyBtn.Visibility = Visibility.Collapsed;
                }
                else
                {
                    schemaBorder?.ClearValue(VisibilityProperty);
                    selectionPanel?.ClearValue(VisibilityProperty);
                    identifyBtn?.ClearValue(VisibilityProperty);
                }
            }
        }

        // Sayfa 9 (Gelişmiş): Değişen gelişmiş ayar yoksa filtre kutusunu ve uyarıyı gizle
        if (PagesHost.Children.Count > KoyWindowViewModel.AdvancedPageIndex &&
            PagesHost.Children[KoyWindowViewModel.AdvancedPageIndex] is FrameworkElement page9)
        {
            var hasChangedAdvanced = HasChangedSettingsOnPage(KoyWindowViewModel.AdvancedPageIndex, changed);
            var rootPanel = LogicalTreeHelper.GetChildren(page9).OfType<System.Windows.Controls.Panel>().FirstOrDefault();
            if (rootPanel is not null)
            {
                for (var i = 2; i < rootPanel.Children.Count; i++)
                {
                    var child = rootPanel.Children[i] as FrameworkElement;
                    if (only && !hasChangedAdvanced)
                        child?.SetValue(VisibilityProperty, Visibility.Collapsed);
                    else
                        child?.ClearValue(VisibilityProperty);
                }
            }
        }
    }

    private void UpdateEmptyPageState(bool? onlyOverride = null, HashSet<string>? changedOverride = null)
    {
        var only = onlyOverride ?? ViewModel.ShowChangedOnly;
        var selectedPage = ViewModel.SelectedPageIndex;
        var changed = changedOverride ?? ViewModel.ChangedSettingKeys().ToHashSet(StringComparer.Ordinal);
        var hasChangedOnSelected = HasChangedSettingsOnPage(selectedPage, changed);

        if (EmptyChangedState is not null)
        {
            EmptyChangedState.Visibility = only && !hasChangedOnSelected
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        if (ResetPageHost is not null)
        {
            ResetPageHost.Visibility = (!only || hasChangedOnSelected) && ViewModel.CanResetPage
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }

    /// <summary>Değişmiş satırlara nokta koyar; "yalnız değişenler" açıksa diğer satırları ve değişmeyen bölüm başlıklarını gizler.</summary>
    private void ApplyChangedMarks()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(ApplyChangedMarks);
            return;
        }

        var changed = ViewModel.ChangedSettingKeys().ToHashSet(StringComparer.Ordinal);
        var only = ViewModel.ShowChangedOnly;
        foreach (var row in Index.Rows)
        {
            var isChanged = row.SettingKey is not null && changed.Contains(row.SettingKey);
            SettingsRowState.SetIsChanged(row.Element, isChanged);
            SettingsRowState.SetIsFilteredOut(row.Element, only && !isChanged);
        }

        EnsureSectionMap();

        foreach (var header in Index.Headers)
        {
            if (!only)
            {
                SettingsRowState.SetIsFilteredOut(header, false);
                if (_sectionExtras is not null && _sectionExtras.TryGetValue(header, out var extras))
                {
                    foreach (var extra in extras)
                        extra.ClearValue(VisibilityProperty);
                }
                continue;
            }

            var hasChangedRows = _sectionRows is not null &&
                                 _sectionRows.TryGetValue(header, out var rows) &&
                                 rows.Any(r => r.SettingKey is not null && changed.Contains(r.SettingKey));

            SettingsRowState.SetIsFilteredOut(header, !hasChangedRows);
            if (_sectionExtras is not null && _sectionExtras.TryGetValue(header, out var extrasList))
            {
                foreach (var extra in extrasList)
                    extra.Visibility = hasChangedRows ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        if (_infoBoxes is not null)
        {
            foreach (var info in _infoBoxes)
            {
                if (only)
                    info.Visibility = Visibility.Collapsed;
                else
                    info.ClearValue(VisibilityProperty);
            }
        }

        ApplyPageCustomFilterState(only, changed);
        UpdateEmptyPageState(only, changed);
    }

    private const double BaseWidth = 880, BaseHeight = 680, BaseMinHeight = 600;

    // Son uygulanan ölçek; Applied her görünüm değişikliğinde (vurgu rengi vb.) gelir, ölçek aynıysa pencereye dokunulmaz.
    private double _uiScale = double.NaN;

    /// <summary>
    /// Koy ölçeği (issue #71): kök öğeye LayoutTransform, pencere boyu ölçekle; çalışma alanına sığmıyorsa sığacak
    /// kadar küçültülür (FontScalePolicy.FitScale). Çentik tuvali sabit kalır (AGENTS #13), ölçek yalnız bu pencerede.
    /// </summary>
    private void ApplyUiScale()
    {
        var percent = ThemeManager.LastSettings?.Appearance.UiScalePercent ?? Koy.Core.Appearance.FontScalePolicy.DefaultUiScale;
        var work = SystemParameters.WorkArea;
        var scale = Koy.Core.Appearance.FontScalePolicy.FitScale(percent, BaseWidth, BaseHeight, work.Width, work.Height);
        if (scale == _uiScale)
            return;

        if (Content is FrameworkElement root)
            root.LayoutTransform = scale == 1.0 ? System.Windows.Media.Transform.Identity : new System.Windows.Media.ScaleTransform(scale, scale);

        // İlk açılışta taban boy; sonra kullanıcının verdiği boy ölçekle orantılı büyür/küçülür, çalışma alanını aşmaz.
        var ratio = double.IsNaN(_uiScale) ? 0 : scale / _uiScale;
        _uiScale = scale;
        MinWidth = BaseWidth * scale;
        MinHeight = BaseMinHeight * scale;
        Width = ratio > 0 ? Math.Min(Width * ratio, work.Width) : BaseWidth * scale;
        Height = ratio > 0 ? Math.Min(Height * ratio, work.Height) : BaseHeight * scale;
        Width = Math.Max(Width, MinWidth);
        Height = Math.Max(Height, MinHeight);
    }

    // Eklentiler sayfası yalnız pencere görünür ve sayfa seçiliyken canlıdır (indeks ve görseller; AGENTS #5).
    private void SyncExtensionsActive() =>
        ViewModel.Extensions.SetActive(IsVisible && ViewModel.SelectedPageIndex == KoyWindowViewModel.ExtensionsPageIndex);

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        SyncExtensionsActive();
        if (IsVisible)
        {
            if (_userStyleWatcher is not null || Application.Current is not { } app)
                return;

            // Pencere kapalıyken değişmiş dosya burada yakalanır, sonra olay tabanlı izleme başlar.
            ThemeManager.ReloadUserStyleIfChanged(app);
            _userStyleWatcher = new UserStyleWatcher(Dispatcher, () =>
            {
                if (Application.Current is { } current)
                    ThemeManager.ReloadUserStyleIfChanged(current);
            });
        }
        else
        {
            _userStyleWatcher?.Dispose();
            _userStyleWatcher = null;
        }
    }

    /// <summary>Sayfa değişince yeni sayfa kısa bir solmayla girer; hareket kapalıysa anında geçer.</summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(KoyWindowViewModel.SettingsSearchQuery))
        {
            RunSearch();
            return;
        }

        if (e.PropertyName == nameof(KoyWindowViewModel.ShowChangedOnly))
        {
            ApplyChangedMarks();
            return;
        }

        if (e.PropertyName == nameof(KoyWindowViewModel.IsUninstallConfirming) && ViewModel.IsUninstallConfirming && IsVisible)
        {
            // Kart solarak girer; odak güvenli seçeneğe (İptal) verilir.
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
            {
                Motion.PageEnter(UninstallCard);
                UninstallCancelButton.Focus();
            });
            return;
        }

        if (e.PropertyName == nameof(KoyWindowViewModel.IsUpdateOverlayOpen) && ViewModel.IsUpdateOverlayOpen && IsVisible)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
            {
                Motion.PageEnter(UpdateCard);
            });
            return;
        }

        if (e.PropertyName == nameof(KoyWindowViewModel.IsWhatsNewOverlayOpen) && ViewModel.IsWhatsNewOverlayOpen && IsVisible)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
            {
                Motion.PageEnter(WhatsNewCard);
            });
            return;
        }

        if (e.PropertyName == nameof(KoyWindowViewModel.SelectedPageIndex))
        {
            UpdateEmptyPageState();
            SyncExtensionsActive();
            ContentScrollViewer?.ScrollToTop();
            if (!IsVisible)
                return;

            // Hemen başlatılır: Loaded önceliğine ertelenince yeni sayfa bir kare tam opak çizilip sonra sönüyordu (titreme).
            var index = ViewModel.SelectedPageIndex;
            if (index >= 0 && index < PagesHost.Children.Count && PagesHost.Children[index] is FrameworkElement page)
                Motion.PageEnter(page);
            return;
        }
    }

    public static void ShowInstance(int pageIndex = 0)
    {
        var win = Instance;
        win.ViewModel.LoadFromSettings();
        win.ViewModel.SelectPage(pageIndex);
        win.ApplyChangedMarks();
        win.ContentScrollViewer?.ScrollToTop();

        if (win.WindowState == WindowState.Minimized)
        {
            win.WindowState = WindowState.Normal;
        }

        win.Show();
        win.Topmost = true;
        win.Activate();
        win.Topmost = false;
        win.Focus();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // Gerçek bulanıklık, koyu çerçeve ve köşeler DWM'den; tema / malzeme / köşe değişince kendiliğinden yenilenir.
        WindowFrame.Attach(this);
    }

    private void KeepSettingsLabel_Click(object sender, MouseButtonEventArgs e)
    {
        // Kutunun kendisine tıklama zaten çevirir; yalnız metne tıklama buraya gelir.
        if (e.OriginalSource is System.Windows.Controls.TextBlock)
            ViewModel.KeepSettingsOnUninstall = !ViewModel.KeepSettingsOnUninstall;
    }

    private void Navigate(int index)
    {
        ViewModel.SelectPage(index);
        ContentScrollViewer?.ScrollToTop();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        SyncAndSaveOnClose();
        Hide();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Pencereyi kapatmak Koy'u kapatmaz; gizler
        e.Cancel = true;
        SyncAndSaveOnClose();
        Hide();
    }

    private void SyncAndSaveOnClose()
    {
        if (AppUninstaller.InProgress)
            return;

        if (ViewModel.IsWhatsNewOverlayOpen)
        {
            ViewModel.CloseWhatsNewOverlay();
        }

        ViewModel.SyncFromDisk();
        ViewModel.SaveSettingsCommand.Execute(null);
    }

    private void NavGenel_Click(object sender, RoutedEventArgs e) => Navigate(0);
    private void NavCentik_Click(object sender, RoutedEventArgs e) => Navigate(1);
    private void NavDock_Click(object sender, RoutedEventArgs e) => Navigate(2);
    private void NavEkranlar_Click(object sender, RoutedEventArgs e) => Navigate(3);
    private void NavGorunum_Click(object sender, RoutedEventArgs e) => Navigate(4);
    private void NavKisayollar_Click(object sender, RoutedEventArgs e) => Navigate(5);
    private void NavDikte_Click(object sender, RoutedEventArgs e) => Navigate(6);
    private void NavPerformans_Click(object sender, RoutedEventArgs e) => Navigate(7);
    private void NavHakkinda_Click(object sender, RoutedEventArgs e) => Navigate(8);
    private void NavGelismis_Click(object sender, RoutedEventArgs e) => Navigate(KoyWindowViewModel.AdvancedPageIndex);
    private void NavEklentiler_Click(object sender, RoutedEventArgs e) => Navigate(KoyWindowViewModel.ExtensionsPageIndex);

    // --- settings.json canlı yeniden yükleme (issue #69) ---
    // İzleyici yalnız Koy penceresi görünürken vardır; gizlenince bırakılır. Olaylar kısa bir tek atımlık zamanlayıcıyla
    // birleştirilir (kaydetme geçici dosya + taşıma ile birkaç olay üretir); zamanlayıcı her seferinde durur (AGENTS #5).

    private System.IO.FileSystemWatcher? _settingsWatcher;
    private DispatcherTimer? _reloadDebounce;

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == IsVisibleProperty)
        {
            if (IsVisible)
                StartSettingsWatcher();
            else
                StopSettingsWatcher();
        }
    }

    private void StartSettingsWatcher()
    {
        if (_settingsWatcher is not null)
            return;

        var path = ViewModel.SettingsFilePath;
        var dir = System.IO.Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(dir) || !System.IO.Directory.Exists(dir))
            return;

        try
        {
            _settingsWatcher = new System.IO.FileSystemWatcher(dir, System.IO.Path.GetFileName(path))
            {
                NotifyFilter = System.IO.NotifyFilters.LastWrite | System.IO.NotifyFilters.FileName | System.IO.NotifyFilters.Size,
            };
            _settingsWatcher.Changed += OnSettingsFileEvent;
            _settingsWatcher.Created += OnSettingsFileEvent;
            _settingsWatcher.Renamed += OnSettingsFileEvent;
            _settingsWatcher.EnableRaisingEvents = true;
        }
        catch (Exception ex) when (ex is System.IO.IOException or ArgumentException or UnauthorizedAccessException)
        {
            // İzlenemiyorsa canlı yükleme yok; ayarlar penceresi normal çalışır.
            _settingsWatcher?.Dispose();
            _settingsWatcher = null;
        }
    }

    private void StopSettingsWatcher()
    {
        _reloadDebounce?.Stop();
        if (_settingsWatcher is null)
            return;

        _settingsWatcher.EnableRaisingEvents = false;
        _settingsWatcher.Changed -= OnSettingsFileEvent;
        _settingsWatcher.Created -= OnSettingsFileEvent;
        _settingsWatcher.Renamed -= OnSettingsFileEvent;
        _settingsWatcher.Dispose();
        _settingsWatcher = null;
    }

    // İzleyicinin iş parçacığından gelir.
    private void OnSettingsFileEvent(object sender, System.IO.FileSystemEventArgs e) =>
        Dispatcher.BeginInvoke(() =>
        {
            if (_settingsWatcher is null)
                return;

            _reloadDebounce ??= CreateReloadDebounce();
            _reloadDebounce.Stop();
            _reloadDebounce.Start();
        });

    private DispatcherTimer CreateReloadDebounce()
    {
        var timer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = TimeSpan.FromMilliseconds(500) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            ViewModel.ReloadFromDiskIfChanged();
        };
        return timer;
    }
}
