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

    [Fact]
    public void ExtractVersion_SurumYoksaSonSurumeDusmez()
    {
        // Güncelleme penceresi yeni sürümün notunu ister; kurulu sürümün notları yerine null dönmeli.
        var md = "## [0.2.0] (2026-11-07)\r\n\r\nÖne çıkanlar:\r\n- **Eski:** not\r\n";
        Assert.Null(ChangelogParser.ExtractVersion(md, "0.2.1"));
    }

    [Fact]
    public void ExtractVersion_PaketNotuBiciminiOkur()
    {
        // Yayın akışının --releaseNotes ile pakete koyduğu bölüm (CRLF, tek sürüm).
        var md = "## [0.2.1](https://github.com/koyhq/koy/compare/v0.2.0...v0.2.1) (2026-11-09)\r\n\r\nÖne çıkanlar:\r\n- **Yeni:** özellik\r\n\r\n### Düzeltilenler (Fixed)\r\n- bir şey\r\n";
        var notes = ChangelogParser.ExtractVersion(md, "0.2.1");
        Assert.NotNull(notes);
        Assert.NotEmpty(notes!.Highlights);
    }

    [Theory]
    [InlineData("**dock:** sağ tık menüsüne atlama listesini ekle ([#32](https://github.com/koyhq/koy/issues/32)) ([9aa22ea](https://github.com/koyhq/koy/commit/9aa22ea9))",
                "Sağ tık menüsüne atlama listesini ekle")]
    [InlineData("gizlilik ipucu metinleri ([#24](https://x/24), [#27](https://x/27), [#29](https://x/29)) ([890cf03](https://x/c))",
                "Gizlilik ipucu metinleri")]
    [InlineData("**updater:** indirme ([#27](https://x/27))", "İndirme")]
    [InlineData("**Atlama listesi:** dock'ta sağ tık", "Atlama listesi: dock'ta sağ tık")]
    [InlineData("**Depo taşındı:** Koy artık [github.com/koyhq/koy](https://github.com/koyhq/koy) adresinde.", "Depo taşındı: Koy artık github.com/koyhq/koy adresinde.")]
    [InlineData("`Ayarlar` sayfası", "Ayarlar sayfası")]
    public void CleanItem_DuzMetneCevirir(string input, string expected)
    {
        Assert.Equal(expected, ChangelogParser.CleanItem(input));
    }

    [Fact]
    public void CleanItem_SiteKapsaminiGizler()
    {
        Assert.Null(ChangelogParser.CleanItem("**site:** hero çentiğini aç ([#33](https://x/33)) ([00708b3](https://x/c))"));
    }

    [Fact]
    public void ExtractVersion_ReleasePleaseMaddeleriniTemizler()
    {
        var md = "## [0.2.1](https://github.com/koyhq/koy/compare/v0.2.0...v0.2.1) (2026-11-08)\r\n\r\nÖne çıkanlar:\r\n- **Hep havada:** kapsül havada kalır.\r\n\r\n\r\n### Eklenenler\r\n\r\n* **dock:** yüzen kapsüle Hep havada modunu ekle ([#31](https://x/31)) ([3c828bd](https://x/c))\r\n* **site:** hero çentiği ([#33](https://x/33))\r\n";
        var notes = ChangelogParser.ExtractVersion(md, "0.2.1")!;
        Assert.Equal(["Hep havada: kapsül havada kalır."], notes.Highlights);
        var added = Assert.Single(notes.Sections);
        Assert.Equal(["Yüzen kapsüle Hep havada modunu ekle"], added.Items);
    }
}
