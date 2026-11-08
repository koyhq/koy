using Koy.Core.Updates;

namespace Koy.Core.Tests.Updates;

public class ChangelogParserTests
{
    private const string SampleChangelog = """
        # Değişiklik Günlüğü (Changelog)

        Bu projedeki tüm önemli değişiklikler bu dosyada belgelenecektir.

        ---

        ## [0.2.0](https://github.com/koyhq/koy/compare/v0.1.1...v0.2.0) (2026-11-07)

        Öne çıkanlar:
        - **Dock klasörleri:** sabitli bir uygulamayı başka birinin üstüne sürükleyince klasör olur.
        - **Tam genişlik dock:** 48 px Windows görev çubuğu ölçüsünde.
        - **Küçük tepsi:** tepsi düğmesi dar bir ok olur.

        ### Eklenenler (Added)
        - Dock klasörleri ve cam panel.
        - Pil ve Bluetooth bildirimleri.

        ### Düzeltmeler (Fixed)
        - Büyütülmüş pencereler yeniden boyutlanmıyor.
        - Açık uygulamalar kaybolmuyor.

        ## [0.1.1](https://github.com/koyhq/koy/compare/v0.1.0...v0.1.1) (2026-11-06)

        0.1.0'dan sonraki ilk büyük sürüm. Öne çıkanlar:
        - Yeni çentik tasarımı: 7 sekme.
        - Gözcü süreç ve görev çubuğu güvencesi.

        ### Düzeltmeler (Fixed)
        - İkinci ekran çökmesi düzeltildi.
        """;

    [Fact]
    public void ExtractVersion_MevcutSurumuDogruAyiklar()
    {
        var notes = ChangelogParser.ExtractVersion(SampleChangelog, "0.2.0");

        Assert.NotNull(notes);
        Assert.Equal("0.2.0", notes.Version);
        Assert.Equal("2026-11-07", notes.Date);
        Assert.Equal("https://github.com/koyhq/koy/compare/v0.1.1...v0.2.0", notes.CompareUrl);
        Assert.Equal(3, notes.Highlights.Count);
        Assert.Contains("Dock klasörleri", notes.Highlights[0]);
        Assert.Equal(2, notes.Sections.Count);
        Assert.Equal("Eklenenler (Added)", notes.Sections[0].Title);
        Assert.Equal(2, notes.Sections[0].Items.Count);
        Assert.Equal("Düzeltmeler (Fixed)", notes.Sections[1].Title);
        Assert.Equal(2, notes.Sections[1].Items.Count);
    }

    [Fact]
    public void ExtractVersion_VHarfliVeOnEkliSurumleriEsler()
    {
        var notes = ChangelogParser.ExtractVersion(SampleChangelog, "v0.1.1+build123");

        Assert.NotNull(notes);
        Assert.Equal("0.1.1", notes.Version);
        Assert.Equal("2026-11-06", notes.Date);
        Assert.Equal(2, notes.Highlights.Count);
    }

    [Fact]
    public void ExtractVersion_BulunmayanSurumdeNullDondurur()
    {
        var notes = ChangelogParser.ExtractVersion(SampleChangelog, "9.9.9");
        Assert.Null(notes);
    }

    [Fact]
    public void ParseLatest_EnUsttekiSurumuVerir()
    {
        var latest = ChangelogParser.ParseLatest(SampleChangelog);

        Assert.NotNull(latest);
        Assert.Equal("0.2.0", latest.Version);
    }

    [Fact]
    public void GetAllVersions_TumSurumleriSiraliVerir()
    {
        var versions = ChangelogParser.GetAllVersions(SampleChangelog);

        Assert.Equal(2, versions.Count);
        Assert.Equal("0.2.0", versions[0]);
        Assert.Equal("0.1.1", versions[1]);
    }

    [Fact]
    public void ReadEmbeddedChangelog_GomuluKaynagiBasariylaOkur()
    {
        var markdown = ChangelogParser.ReadEmbeddedChangelog();
        Assert.NotNull(markdown);
        Assert.Contains("Değişiklik Günlüğü", markdown);

        var notes = ChangelogParser.ParseLatest(markdown);
        Assert.NotNull(notes);
        Assert.NotEmpty(notes.Version);
    }
}
