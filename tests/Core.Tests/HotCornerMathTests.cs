using Koy.Core.Input;

namespace Koy.Core.Tests;

public class HotCornerMathTests
{
    // Birincil ekran benzeri, sol üstü (0,0) olmayan bir monitör: köşeler ofsetle birlikte hesaplanmalı.
    private static readonly ScreenRect Screen = new(1920, 0, 1920, 1080);

    [Fact]
    public void HitTest_DortKoseyiBulur()
    {
        Assert.Equal(ScreenCorner.TopLeft, HotCornerMath.HitTest(1920, 0, Screen, 4));
        Assert.Equal(ScreenCorner.TopRight, HotCornerMath.HitTest(3839, 0, Screen, 4));
        Assert.Equal(ScreenCorner.BottomLeft, HotCornerMath.HitTest(1920, 1079, Screen, 4));
        Assert.Equal(ScreenCorner.BottomRight, HotCornerMath.HitTest(3839, 1079, Screen, 4));
    }

    [Fact]
    public void HitTest_KoseBoyununDisindaNull()
    {
        // Sol üst köşe 4 px; 4. piksel (indeks 4) köşenin dışındadır.
        Assert.Null(HotCornerMath.HitTest(1924, 0, Screen, 4));
        Assert.Null(HotCornerMath.HitTest(1920, 4, Screen, 4));
        Assert.Equal(ScreenCorner.TopLeft, HotCornerMath.HitTest(1923, 3, Screen, 4));
    }

    [Fact]
    public void HitTest_KenarOrtasiKoseDegil()
    {
        Assert.Null(HotCornerMath.HitTest(2880, 0, Screen, 4));
        Assert.Null(HotCornerMath.HitTest(1920, 540, Screen, 4));
    }

    [Fact]
    public void HitTest_EkranDisiNull()
    {
        Assert.Null(HotCornerMath.HitTest(1919, 0, Screen, 4));
        Assert.Null(HotCornerMath.HitTest(3840, 0, Screen, 4));
        Assert.Null(HotCornerMath.HitTest(1920, -1, Screen, 4));
        Assert.Null(HotCornerMath.HitTest(1920, 1080, Screen, 4));
    }

    [Fact]
    public void HitTest_GecersizBoyutNull()
    {
        Assert.Null(HotCornerMath.HitTest(1920, 0, Screen, 0));
        Assert.Null(HotCornerMath.HitTest(1920, 0, Screen with { Width = 0 }, 4));
    }

    [Fact]
    public void HitTest_BuyukKoseIkiKoseyiCakistirmaz()
    {
        // Köşe boyu ekranın yarısından büyük: nokta en yakın kenara göre seçilir.
        var small = new ScreenRect(0, 0, 10, 10);

        Assert.Equal(ScreenCorner.TopLeft, HotCornerMath.HitTest(2, 2, small, 8));
        Assert.Equal(ScreenCorner.TopRight, HotCornerMath.HitTest(7, 2, small, 8));
        Assert.Equal(ScreenCorner.BottomLeft, HotCornerMath.HitTest(2, 7, small, 8));
        Assert.Equal(ScreenCorner.BottomRight, HotCornerMath.HitTest(7, 7, small, 8));
    }

    [Fact]
    public void IsDwellComplete_SureDolunca_True()
    {
        Assert.False(HotCornerMath.IsDwellComplete(1000, 1299, 300));
        Assert.True(HotCornerMath.IsDwellComplete(1000, 1300, 300));
        Assert.True(HotCornerMath.IsDwellComplete(1000, 1000, 0));
    }

    [Fact]
    public void IsDwellComplete_GeriyeDonenZamandaFalse()
    {
        Assert.False(HotCornerMath.IsDwellComplete(2000, 1000, 0));
    }

    [Fact]
    public void HitTest_NegatifKoordinatliSolMonitor()
    {
        var left = new ScreenRect(-1920, 0, 1920, 1080);

        Assert.Equal(ScreenCorner.TopLeft, HotCornerMath.HitTest(-1920, 0, left, 4));
        Assert.Equal(ScreenCorner.BottomRight, HotCornerMath.HitTest(-1, 1079, left, 4));
        Assert.Null(HotCornerMath.HitTest(0, 0, left, 4));
    }

    [Fact]
    public void HitTest_CokEkran_OrtakKenardakiKoseSayilmaz()
    {
        ScreenRect[] screens = [new(-1920, 0, 1920, 1080), new(0, 0, 1920, 1080)];

        // Ortak kenar: imleç komşuya geçebilir, köşe değil.
        Assert.Null(HotCornerMath.HitTest(-1, 0, screens, 4));
        Assert.Null(HotCornerMath.HitTest(0, 1079, screens, 4));

        // Dış köşeler çalışır ve doğru ekranı bildirir.
        Assert.Equal(new CornerHit(0, ScreenCorner.TopLeft), HotCornerMath.HitTest(-1920, 0, screens, 4));
        Assert.Equal(new CornerHit(1, ScreenCorner.BottomRight), HotCornerMath.HitTest(1919, 1079, screens, 4));
    }

    [Fact]
    public void HitTest_CokEkran_KaymisKomsuIcinOrtakOlmayanKoseCalisir()
    {
        // Sağdaki monitör 500 px aşağıda: soldakinin sağ üst köşesi komşuya açık değil.
        ScreenRect[] screens = [new(0, 0, 1920, 1080), new(1920, 500, 1920, 1080)];

        Assert.Equal(new CornerHit(0, ScreenCorner.TopRight), HotCornerMath.HitTest(1919, 0, screens, 4));
        Assert.Null(HotCornerMath.HitTest(1919, 1079, screens, 4));
    }

    [Fact]
    public void HitTest_CokEkran_UstUsteKomsuAltKenar()
    {
        ScreenRect[] screens = [new(0, 0, 1920, 1080), new(0, 1080, 1920, 1080)];

        Assert.Null(HotCornerMath.HitTest(0, 1079, screens, 4));
        Assert.Null(HotCornerMath.HitTest(0, 1080, screens, 4));
        Assert.Equal(new CornerHit(1, ScreenCorner.BottomRight), HotCornerMath.HitTest(1919, 2159, screens, 4));
    }

    [Fact]
    public void HitTest_CokEkran_BosListeVeSifirBoyutluEkran()
    {
        Assert.Null(HotCornerMath.HitTest(0, 0, Array.Empty<ScreenRect>(), 4));

        // Sıfır boyutlu ekran komşu sayılmaz.
        ScreenRect[] screens = [new(0, 0, 1920, 1080), new(1920, 0, 0, 0)];
        Assert.Equal(new CornerHit(0, ScreenCorner.TopRight), HotCornerMath.HitTest(1919, 0, screens, 4));
    }

    [Fact]
    public void IsDwellComplete_NegatifSureSifirGibi()
    {
        Assert.True(HotCornerMath.IsDwellComplete(1000, 1000, -50));
    }
}
