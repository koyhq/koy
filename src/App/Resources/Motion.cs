using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Koy.Core.Appearance;
using Koy.Core.Power;
using Koy.Core.Settings;

namespace Koy.App.Resources;

/// <summary>
/// Koy penceresinin hareketi: sayfa geçişi, hover/basma geçişleri, anahtar yayı, liste girişi.
/// Bütün süreler tek bir çarpandan geçer (<see cref="MotionPolicy"/>): Animasyon ayarı, Windows "animasyonları azalt"
/// ve pil tasarrufu. Çarpan 0 ise değerler anında atanır. Animasyonlar yalnız tetiklenince çalışır; biten
/// animasyon saat bırakmaz (FillBehavior.Stop), boştayken kare çizilmez.
/// Şablonlar süreyi XAML'e gömmez; tetikleyici bu dosyadaki ekli özellikleri (FadeTo, ScaleTo, KnobOn) değiştirir.
/// </summary>
public static class Motion
{
    private static readonly IEasingFunction EaseOut = new CubicEase { EasingMode = EasingMode.EaseOut };
    private static readonly IEasingFunction Spring = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.45 };

    /// <summary>Süre çarpanı; 0 = animasyon yok.</summary>
    public static double Scale { get; private set; } = 1.0;

    public static bool Enabled => Scale > 0;

    // Animasyon başına anahtarlar (issue #79); Refresh ile ayarlardan gelir.
    private static MotionSettings _toggles = new();

    /// <summary>Bir animasyon türünün çarpanı: kendi anahtarı kapalıysa ya da genel çarpan 0 ise 0 (son durum anında).</summary>
    public static double ScaleFor(MotionPolicy.MotionKind kind) => MotionPolicy.ScaleFor(kind, _toggles, Scale);

    /// <summary>Bu animasyon türü kare çizecek mi; değilse çağıran son değeri doğrudan atar, Rendering bağlamaz.</summary>
    public static bool EnabledFor(MotionPolicy.MotionKind kind) => ScaleFor(kind) > 0;

    /// <summary>Çarpanı ayarlardan, Windows sistem ayarından ve pil durumundan yeniden hesaplar.</summary>
    public static void Refresh(AppSettings settings)
    {
        var offline = SystemParameters.PowerLineStatus == PowerLineStatus.Offline;
        var batteryReduce = BatterySaverPolicy.ShouldReduceMotion(
            settings.Appearance.BatterySaverReduceMotion, offline, offline ? BatteryStatus.Discharging : BatteryStatus.Idle);
        Scale = MotionPolicy.Scale(settings.Appearance.AnimationSpeed, settings.Appearance.RespectReduceMotion, SystemParameters.ClientAreaAnimation, batteryReduce);
        _toggles = settings.Appearance.Motion ?? new MotionSettings();
    }

    private static Duration D(double ms) => new(TimeSpan.FromMilliseconds(MotionPolicy.ScaledMilliseconds(ms, Scale)));

    // ---- Ekli özellik: opaklığa yumuşak geçiş (hover katmanları) ----

    public static readonly DependencyProperty FadeToProperty = DependencyProperty.RegisterAttached(
        "FadeTo", typeof(double), typeof(Motion), new PropertyMetadata(0.0, OnFadeToChanged));

    public static double GetFadeTo(DependencyObject o) => (double)o.GetValue(FadeToProperty);
    public static void SetFadeTo(DependencyObject o, double v) => o.SetValue(FadeToProperty, v);

    private static void OnFadeToChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UIElement el)
            AnimateDouble(el, UIElement.OpacityProperty, (double)e.NewValue, 140, EaseOut);
    }

    // ---- Ekli özellik: basılınca küçülme ----

    public static readonly DependencyProperty ScaleToProperty = DependencyProperty.RegisterAttached(
        "ScaleTo", typeof(double), typeof(Motion), new PropertyMetadata(1.0, OnScaleToChanged));

    public static double GetScaleTo(DependencyObject o) => (double)o.GetValue(ScaleToProperty);
    public static void SetScaleTo(DependencyObject o, double v) => o.SetValue(ScaleToProperty, v);

    private static void OnScaleToChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement el)
            return;
        if (el.RenderTransform is not ScaleTransform st || st.IsFrozen)
        {
            st = new ScaleTransform(1, 1);
            el.RenderTransform = st;
            el.RenderTransformOrigin = new Point(0.5, 0.5);
        }

        var to = (double)e.NewValue;
        AnimateTransform(st, ScaleTransform.ScaleXProperty, to, 110, EaseOut);
        AnimateTransform(st, ScaleTransform.ScaleYProperty, to, 110, EaseOut);
    }

    // ---- Ekli özellik: anahtar düğmesi yayı (kapalı 0, açık 18 DIP) ----

    public static readonly DependencyProperty KnobOnProperty = DependencyProperty.RegisterAttached(
        "KnobOn", typeof(bool), typeof(Motion), new PropertyMetadata(false, OnKnobOnChanged));

    public static bool GetKnobOn(DependencyObject o) => (bool)o.GetValue(KnobOnProperty);
    public static void SetKnobOn(DependencyObject o, bool v) => o.SetValue(KnobOnProperty, v);

    private static void OnKnobOnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement el)
            return;
        if (el.RenderTransform is not TranslateTransform tt || tt.IsFrozen)
        {
            tt = new TranslateTransform();
            el.RenderTransform = tt;
        }

        var to = (bool)e.NewValue ? 18.0 : 0.0;
        // Yay hissi: hedefi biraz aşıp geri döner (BackEase). İlk yüklemede animasyonsuz yerleşir.
        AnimateTransform(tt, TranslateTransform.XProperty, to, 260, Spring, animate: (el as FrameworkElement)?.IsLoaded == true);
    }

    // ---- Sayfa geçişi ----

    /// <summary>
    /// Sayfa görününce tek, kısa bir solma: %50'den tam opaklığa 200 ms (Corvus'taki sayfa geçişi gibi).
    /// Kayma ve satır satır kademeli giriş yok: gecikmeli satırlar önce tam görünüp sonra sıfırdan solduğu için
    /// geçiş kesik kesik görünüyordu. Hareket kapalıysa sayfa anında gelir.
    /// </summary>
    public static void PageEnter(FrameworkElement page)
    {
        if (!Enabled)
            return;

        AnimateDouble(page, UIElement.OpacityProperty, 1, 200, EaseOut, from: 0.5);
    }

    // ---- Yardımcılar ----

    private static void AnimateDouble(UIElement el, DependencyProperty prop, double to, double ms, IEasingFunction ease,
        double? from = null, TimeSpan? delay = null)
    {
        var duration = D(ms);
        var current = from ?? (double)el.GetValue(prop);
        el.SetValue(prop, to);
        if (!Enabled || duration.TimeSpan == TimeSpan.Zero && delay is null || current == to)
        {
            el.BeginAnimation(prop, null);
            return;
        }

        el.BeginAnimation(prop, new DoubleAnimation(current, to, duration)
        {
            EasingFunction = ease,
            FillBehavior = FillBehavior.Stop,
            BeginTime = delay
        });
    }

    private static void AnimateTransform(Animatable target, DependencyProperty prop, double to, double ms, IEasingFunction ease,
        double? from = null, TimeSpan? delay = null, bool animate = true)
    {
        var duration = D(ms);
        var current = from ?? (double)target.GetValue(prop);
        target.SetValue(prop, to);
        if (!Enabled || !animate || current == to)
        {
            target.BeginAnimation(prop, null);
            return;
        }

        target.BeginAnimation(prop, new DoubleAnimation(current, to, duration)
        {
            EasingFunction = ease,
            FillBehavior = FillBehavior.Stop,
            BeginTime = delay
        });
    }
}
