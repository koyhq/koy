using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Koy.App.Resources;
using Koy.Platform.Shell;

namespace Koy.App.Features.Notch;

/// <summary>
/// Sesli komutun yanıtının tamamını gösteren kart. Çentik tek satır gösterir; yanıt burada kaydırılabilir ve seçilebilir okunur.
/// Odağı çalmaz (kullanıcı yazmaya devam edebilir), kapat düğmesine ya da yeni komuta kadar açık kalır. Animasyonu yoktur.
/// </summary>
public sealed class VoiceReplyWindow : Window
{
    private const double CardWidth = 480;
    private const double CardMaxHeight = 380;
    private static VoiceReplyWindow? _current;

    private nint _hwnd;

    private VoiceReplyWindow(string prompt, string info, string body, Action openFile)
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.Height;
        Width = CardWidth;
        MaxHeight = CardMaxHeight;

        var title = Text(Strings.Get("VoiceReplyTitle"), 12, FontWeights.SemiBold, "KoyTextPrimary");
        var close = Chip(Strings.Get("VoiceReplyClose"), Close);
        var header = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 6) };
        DockPanel.SetDock(close, System.Windows.Controls.Dock.Right);
        header.Children.Add(close);
        header.Children.Add(title);

        var promptLine = Text(prompt, 11, FontWeights.Normal, "KoyTextSecondary");
        promptLine.TextTrimming = TextTrimming.CharacterEllipsis;
        var infoLine = Text(info, 10, FontWeights.Normal, "KoyTextMuted");
        infoLine.Margin = new Thickness(0, 0, 0, 8);

        // Salt okunur TextBox: seçilip kopyalanabilir, uzun yanıtta kaydırılır.
        var reply = new TextBox
        {
            Text = body,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Padding = new Thickness(0),
            FontSize = 12,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 240
        };
        reply.SetResourceReference(Control.ForegroundProperty, "KoyTextPrimary");
        reply.SetResourceReference(System.Windows.Controls.Primitives.TextBoxBase.SelectionBrushProperty, "KoyAccent");

        var footer = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        footer.Children.Add(Chip(Strings.Get("VoiceReplyOpenFile"), openFile));

        var stack = new StackPanel();
        stack.Children.Add(header);
        stack.Children.Add(promptLine);
        stack.Children.Add(infoLine);
        stack.Children.Add(reply);
        stack.Children.Add(footer);

        var card = new Border
        {
            CornerRadius = new CornerRadius(16),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(16, 12, 16, 14),
            Margin = new Thickness(0),
            Child = stack
        };
        card.SetResourceReference(Border.BackgroundProperty, "KoyBgWindow");
        card.SetResourceReference(Border.BorderBrushProperty, "KoyBorderStrong");
        Content = card;

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
                Close();
        };
        Closed += (_, _) =>
        {
            if (ReferenceEquals(_current, this))
                _current = null;
        };
    }

    /// <summary>Yanıtı çentiğin altında gösterir; önceki yanıt kartı varsa yerine geçer.</summary>
    /// <param name="notchCenterX">Çentiğin yatay ortası (DIP).</param>
    /// <param name="top">Kartın üst kenarı (DIP).</param>
    public static void ShowReply(double notchCenterX, double top, string prompt, string info, string body, Action openFile)
    {
        CloseCurrent();
        var window = new VoiceReplyWindow(prompt, info, body, openFile)
        {
            Left = notchCenterX - CardWidth / 2,
            Top = top
        };
        _current = window;
        window.Show();
    }

    public static void CloseCurrent()
    {
        _current?.Close();
        _current = null;
    }

    /// <summary>Kart açıksa öne alır ve odağı verir (çentiğe tıklayınca): Esc ile kapanır.</summary>
    public static bool FocusCurrent()
    {
        if (_current is not { } window)
            return false;

        WindowStyles.SetNoActivate(window._hwnd, false);
        window.Activate();
        return true;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;
        WindowStyles.ApplyToolWindowAndNoActivate(_hwnd);
    }

    private static TextBlock Text(string text, double size, FontWeight weight, string brushKey)
    {
        var block = new TextBlock { Text = text, FontSize = size, FontWeight = weight };
        block.SetResourceReference(TextBlock.ForegroundProperty, brushKey);
        return block;
    }

    private static Border Chip(string label, Action onClick)
    {
        var chip = new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 4, 10, 4),
            Cursor = Cursors.Hand,
            Child = Text(label, 11, FontWeights.SemiBold, "KoyTextPrimary")
        };
        chip.SetResourceReference(Border.BackgroundProperty, "KoyBgControl");
        chip.MouseLeftButtonUp += (_, e) =>
        {
            onClick();
            e.Handled = true;
        };
        return chip;
    }
}
