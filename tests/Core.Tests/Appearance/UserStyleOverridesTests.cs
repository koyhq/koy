using System.Text;
using System.Text.RegularExpressions;
using Koy.Core.Appearance;

namespace Koy.Core.Tests.Appearance;

public class UserStyleOverridesTests
{
    private static UserStyleParseResult Parse(string json) => UserStyleParser.Parse(json);

    // --- Geçerli değerler ---

    [Fact]
    public void Valid_brush_colors_in_both_forms_are_accepted()
    {
        var r = Parse("""{ "KoyAccent": "#3B82F6", "KoyScrim": "#80112233" }""");

        Assert.Empty(r.Diagnostics);
        Assert.Equal(UserStyleValue.ForBrush(new UserStyleColor(255, 0x3B, 0x82, 0xF6)), r.ValidEntries["KoyAccent"]);
        Assert.Equal(UserStyleValue.ForBrush(new UserStyleColor(0x80, 0x11, 0x22, 0x33)), r.ValidEntries["KoyScrim"]);
    }

    [Fact]
    public void Hex_digits_are_case_insensitive()
    {
        var r = Parse("""{ "KoyAccent": "#abcdef" }""");
        Assert.Equal(new UserStyleColor(255, 0xAB, 0xCD, 0xEF), r.ValidEntries["KoyAccent"].Color);
    }

    [Fact]
    public void Color_kind_token_yields_a_color_value()
    {
        var r = Parse("""{ "KoyShadowColor": "#000000" }""");
        Assert.Equal(UserStyleKind.Color, r.ValidEntries["KoyShadowColor"].Kind);
    }

    [Theory]
    [InlineData("""{ "KoyRadiusRow": 12 }""", 12, 12, 12, 12)]
    [InlineData("""{ "KoyRadiusRow": "12" }""", 12, 12, 12, 12)]
    [InlineData("""{ "KoyRadiusRow": "8,8,0,0" }""", 8, 8, 0, 0)]
    [InlineData("""{ "KoyRadiusRow": " 1.5 , 2 , 3 , 4 " }""", 1.5, 2, 3, 4)]
    public void Corner_radius_accepts_one_or_four_numbers(string json, double a, double b, double c, double d)
    {
        var r = Parse(json);

        Assert.Empty(r.Diagnostics);
        var v = r.ValidEntries["KoyRadiusRow"];
        Assert.Equal(UserStyleKind.CornerRadius, v.Kind);
        Assert.Equal(new UserStyleQuad(a, b, c, d), v.Quad);
    }

    [Fact]
    public void Numbers_are_accepted_as_json_numbers_or_strings_within_bounds()
    {
        var r = Parse("""{ "KoyFontBody": 14.5, "KoySpace2": "10", "KoyIconStroke": 0.5 }""");

        Assert.Empty(r.Diagnostics);
        Assert.Equal(14.5, r.ValidEntries["KoyFontBody"].Number);
        Assert.Equal(10, r.ValidEntries["KoySpace2"].Number);
        Assert.Equal(0.5, r.ValidEntries["KoyIconStroke"].Number);
    }

    [Fact]
    public void Number_parsing_ignores_the_current_culture()
    {
        var old = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("tr-TR");
            var r = Parse("""{ "KoyFontBody": "13.5" }""");
            Assert.Equal(13.5, r.ValidEntries["KoyFontBody"].Number);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = old;
        }
    }

    [Fact]
    public void Font_family_accepts_plain_names_and_fallback_lists()
    {
        var r = Parse("""{ "KoyFont": "Segoe UI Variable Text, Segoe UI, sans-serif", "KoyFontMono": "Cascadia Code" }""");

        Assert.Empty(r.Diagnostics);
        Assert.Equal("Segoe UI Variable Text, Segoe UI, sans-serif", r.ValidEntries["KoyFont"].FontFamily);
        Assert.Equal(UserStyleKind.FontFamily, r.ValidEntries["KoyFontMono"].Kind);
    }

    [Fact]
    public void Non_latin_font_names_are_accepted()
    {
        var r = Parse("""{ "KoyFont": "Yu Gothic UI" }""");
        Assert.Empty(r.Diagnostics);
        r = Parse("{ \"KoyFont\": \"\u00C7izgi Yaz\u0131\" }");
        Assert.Empty(r.Diagnostics);
    }

    [Fact]
    public void Comments_and_trailing_commas_are_tolerated()
    {
        var r = Parse("""
            {
              // vurgu
              "KoyAccent": "#112233",
            }
            """);

        Assert.Empty(r.Diagnostics);
        Assert.Single(r.ValidEntries);
    }

    [Fact]
    public void Empty_object_is_valid_and_empty()
    {
        var r = Parse("{}");
        Assert.Empty(r.ValidEntries);
        Assert.Empty(r.Diagnostics);
    }

    [Fact]
    public void Valid_entries_survive_next_to_invalid_ones()
    {
        var r = Parse("""{ "KoyAccent": "#112233", "Nope": "#000000", "KoyBgRow": "red" }""");

        Assert.Single(r.ValidEntries);
        Assert.Equal(2, r.Diagnostics.Count);
        Assert.Contains(r.Diagnostics, d => d.Key == "Nope" && d.Code == UserStyleDiagnosticCode.UnknownToken);
        Assert.Contains(r.Diagnostics, d => d.Key == "KoyBgRow" && d.Code == UserStyleDiagnosticCode.BadColor);
    }

    // --- Thickness (şu an izinli listede Thickness anahtarı yok; dışarıdan verilen tabloyla denenir) ---

    [Fact]
    public void Thickness_accepts_one_or_four_non_negative_numbers()
    {
        var tokens = new Dictionary<string, UserStyleToken> { ["Pad"] = new("Pad", UserStyleKind.Thickness, 0, 32) };

        var one = UserStyleParser.Parse("""{ "Pad": "6" }""", tokens);
        Assert.Equal(UserStyleValue.ForThickness(UserStyleQuad.Uniform(6)), one.ValidEntries["Pad"]);

        var four = UserStyleParser.Parse("""{ "Pad": "1,2,3,4" }""", tokens);
        Assert.Equal(new UserStyleQuad(1, 2, 3, 4), four.ValidEntries["Pad"].Quad);
        Assert.Equal(UserStyleKind.Thickness, four.ValidEntries["Pad"].Kind);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("1,2")]
    [InlineData("1,2,3")]
    [InlineData("1,2,3,4,5")]
    [InlineData("1,,3,4")]
    [InlineData("1,2,3,x")]
    [InlineData("33")]
    [InlineData("")]
    public void Thickness_rejects_bad_shapes_and_bounds(string value)
    {
        var tokens = new Dictionary<string, UserStyleToken> { ["Pad"] = new("Pad", UserStyleKind.Thickness, 0, 32) };

        var r = UserStyleParser.Parse($$"""{ "Pad": "{{value}}" }""", tokens);

        Assert.Empty(r.ValidEntries);
        Assert.Single(r.Diagnostics);
    }

    // --- Reddedilen girdiler ---

    [Theory]
    [InlineData("Window")]
    [InlineData("koyaccent")] // büyük-küçük harf duyarlı
    [InlineData("KoyAccent ")]
    [InlineData("System.Windows.Application")]
    [InlineData("{x:Static Application.Current}")]
    [InlineData("")]
    public void Unknown_tokens_are_rejected(string key)
    {
        var r = Parse($$"""{ {{System.Text.Json.JsonSerializer.Serialize(key)}}: "#112233" }""");

        Assert.Empty(r.ValidEntries);
        var d = Assert.Single(r.Diagnostics);
        Assert.Equal(UserStyleDiagnosticCode.UnknownToken, d.Code);
    }

    [Theory]
    [InlineData("""{ "KoyAccent": "123456" }""")] // # yok
    [InlineData("""{ "KoyAccent": "#123" }""")] // kısa biçim yok
    [InlineData("""{ "KoyAccent": "#12345" }""")]
    [InlineData("""{ "KoyAccent": "#1234567" }""")]
    [InlineData("""{ "KoyAccent": "#GGGGGG" }""")]
    [InlineData("""{ "KoyAccent": "red" }""")]
    [InlineData("""{ "KoyAccent": "#1122 33" }""")]
    public void Bad_colors_are_rejected(string json)
    {
        var r = Parse(json);

        Assert.Empty(r.ValidEntries);
        Assert.Equal(UserStyleDiagnosticCode.BadColor, Assert.Single(r.Diagnostics).Code);
    }

    [Fact]
    public void Surrounding_whitespace_in_a_color_is_trimmed()
    {
        var r = Parse("""{ "KoyAccent": "  #112233 " }""");
        Assert.Single(r.ValidEntries);
    }

    [Theory]
    [InlineData("""{ "KoyFontBody": "abc" }""", UserStyleDiagnosticCode.BadNumber)]
    [InlineData("""{ "KoyFontBody": "NaN" }""", UserStyleDiagnosticCode.BadNumber)]
    [InlineData("""{ "KoyFontBody": "Infinity" }""", UserStyleDiagnosticCode.BadNumber)]
    [InlineData("""{ "KoyFontBody": "-Infinity" }""", UserStyleDiagnosticCode.BadNumber)]
    [InlineData("""{ "KoyFontBody": "" }""", UserStyleDiagnosticCode.BadNumber)]
    [InlineData("""{ "KoyFontBody": 7 }""", UserStyleDiagnosticCode.OutOfRange)]
    [InlineData("""{ "KoyFontBody": 41 }""", UserStyleDiagnosticCode.OutOfRange)]
    [InlineData("""{ "KoyFontBody": 1e308 }""", UserStyleDiagnosticCode.OutOfRange)]
    [InlineData("""{ "KoySpace1": -1 }""", UserStyleDiagnosticCode.OutOfRange)]
    [InlineData("""{ "KoyFontBody": "1e999" }""", UserStyleDiagnosticCode.BadNumber)]
    public void Bad_or_out_of_range_numbers_are_rejected(string json, UserStyleDiagnosticCode code)
    {
        var r = Parse(json);

        Assert.Empty(r.ValidEntries);
        Assert.Equal(code, Assert.Single(r.Diagnostics).Code);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("33")]
    [InlineData("1,2")]
    [InlineData("1,2,3")]
    [InlineData("1,2,3,4,5")]
    [InlineData("a")]
    [InlineData("1,2,3,-4")]
    [InlineData("NaN")]
    [InlineData("8;8;8;8")]
    public void Bad_corner_radii_are_rejected(string value)
    {
        var r = Parse($$"""{ "KoyRadiusControl": "{{value}}" }""");

        Assert.Empty(r.ValidEntries);
        Assert.Single(r.Diagnostics);
    }

    [Theory]
    [InlineData("""{ "KoyAccent": 123 }""")] // renk sayı olamaz
    [InlineData("""{ "KoyAccent": true }""")]
    [InlineData("""{ "KoyAccent": null }""")]
    [InlineData("""{ "KoyAccent": ["#112233"] }""")]
    [InlineData("""{ "KoyAccent": { "Color": "#112233" } }""")]
    [InlineData("""{ "KoyFont": 12 }""")] // yazı tipi sayı olamaz
    [InlineData("""{ "KoyFontBody": true }""")]
    [InlineData("""{ "KoyFontBody": null }""")]
    [InlineData("""{ "KoyFontBody": [12] }""")]
    [InlineData("""{ "KoyRadiusRow": { "a": 1 } }""")]
    public void Wrong_json_value_types_are_rejected(string json)
    {
        var r = Parse(json);

        Assert.Empty(r.ValidEntries);
        Assert.Equal(UserStyleDiagnosticCode.WrongValueType, Assert.Single(r.Diagnostics).Code);
    }

    [Fact]
    public void Value_kind_is_fixed_by_the_token_not_by_the_file()
    {
        // Renk beklenen yere sayı, sayı beklenen yere renk, köşe beklenen yere yazı tipi adı.
        var r = Parse("""{ "KoyAccent": "12", "KoyFontBody": "#112233", "KoyRadiusRow": "Arial" }""");

        Assert.Empty(r.ValidEntries);
        Assert.Equal(3, r.Diagnostics.Count);
    }

    [Fact]
    public void Duplicate_keys_keep_the_first_value()
    {
        var r = Parse("""{ "KoyAccent": "#111111", "KoyAccent": "#222222" }""");

        Assert.Equal(new UserStyleColor(255, 0x11, 0x11, 0x11), r.ValidEntries["KoyAccent"].Color);
        Assert.Equal(UserStyleDiagnosticCode.DuplicateToken, Assert.Single(r.Diagnostics).Code);
    }

    // --- Bozuk JSON ve dosya sınırları ---

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{")]
    [InlineData("{ \"KoyAccent\": ")]
    [InlineData("not json")]
    [InlineData("{ 'KoyAccent': '#112233' }")]
    [InlineData("\0\0\0")]
    public void Bad_json_yields_one_file_diagnostic_and_never_throws(string json)
    {
        var r = Parse(json);

        Assert.Empty(r.ValidEntries);
        var d = Assert.Single(r.Diagnostics);
        Assert.Equal(UserStyleParser.FileKey, d.Key);
        Assert.Equal(UserStyleDiagnosticCode.InvalidJson, d.Code);
    }

    [Fact]
    public void Null_content_does_not_throw()
    {
        var r = UserStyleParser.Parse(null, AllowedTokens.All);
        Assert.Empty(r.ValidEntries);
        Assert.Single(r.Diagnostics);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("[{\"KoyAccent\":\"#112233\"}]")]
    [InlineData("\"KoyAccent\"")]
    [InlineData("42")]
    [InlineData("null")]
    [InlineData("true")]
    public void Non_object_roots_are_rejected(string json)
    {
        var r = Parse(json);

        Assert.Empty(r.ValidEntries);
        Assert.Equal(UserStyleDiagnosticCode.NotAnObject, Assert.Single(r.Diagnostics).Code);
    }

    [Fact]
    public void Deeply_nested_json_is_rejected_without_throwing()
    {
        var json = new string('[', 5000) + new string(']', 5000);
        var r = Parse(json);

        Assert.Empty(r.ValidEntries);
        Assert.Single(r.Diagnostics);
    }

    [Fact]
    public void Oversize_input_is_rejected_before_parsing()
    {
        var json = "{ \"KoyAccent\": \"#112233\", \"pad\": \"" + new string('a', UserStyleParser.MaxBytes) + "\" }";
        var r = Parse(json);

        Assert.Empty(r.ValidEntries);
        Assert.Equal(UserStyleDiagnosticCode.FileTooLarge, Assert.Single(r.Diagnostics).Code);
    }

    [Fact]
    public void Multibyte_input_is_measured_in_bytes_not_characters()
    {
        // 30.000 karakter ama ~90.000 bayt.
        var json = "{ \"x\": \"" + new string('\u20AC', 30_000) + "\" }";
        Assert.True(json.Length < UserStyleParser.MaxBytes);
        Assert.True(Encoding.UTF8.GetByteCount(json) > UserStyleParser.MaxBytes);

        Assert.Equal(UserStyleDiagnosticCode.FileTooLarge, Assert.Single(Parse(json).Diagnostics).Code);
    }

    [Fact]
    public void Entry_count_is_capped()
    {
        var sb = new StringBuilder("{");
        for (var i = 0; i < UserStyleParser.MaxEntries + 50; i++)
            sb.Append($"\"k{i}\":\"#000000\",");
        sb.Append("\"KoyAccent\":\"#112233\"}");

        var r = Parse(sb.ToString());

        // Üst sınır aşıldığı için sondaki geçerli girdi okunmaz.
        Assert.Empty(r.ValidEntries);
        Assert.Contains(r.Diagnostics, d => d.Code == UserStyleDiagnosticCode.TooManyEntries);
    }

    [Fact]
    public void Diagnostics_list_is_capped_and_the_rest_is_counted()
    {
        var sb = new StringBuilder("{");
        for (var i = 0; i < 300; i++)
            sb.Append($"\"bad{i}\":\"x\",");
        sb.Length--;
        sb.Append('}');

        var r = Parse(sb.ToString());

        Assert.Equal(UserStyleParser.MaxDiagnostics, r.Diagnostics.Count);
        Assert.Equal(300 - UserStyleParser.MaxDiagnostics, r.OmittedDiagnostics);
    }

    [Fact]
    public void Value_longer_than_the_limit_is_rejected()
    {
        var r = Parse($$"""{ "KoyFont": "{{new string('A', UserStyleParser.MaxValueLength + 1)}}" }""");

        Assert.Empty(r.ValidEntries);
        Assert.Equal(UserStyleDiagnosticCode.ValueTooLong, Assert.Single(r.Diagnostics).Code);
    }

    [Fact]
    public void Echoed_key_is_truncated_and_control_characters_are_removed()
    {
        var longKey = new string('k', 500);
        var r = Parse($$"""{ "{{longKey}}": "x", "a\nb\u001b[31mc": "x" }""");

        Assert.All(r.Diagnostics, d => Assert.True(d.Key.Length <= 64));
        Assert.All(r.Diagnostics, d => Assert.Equal(0, d.Key.Count(char.IsControl)));
    }

    // --- Enjeksiyon benzeri girdiler ---

    [Theory]
    [InlineData("{x:Type Window}")]
    [InlineData("{StaticResource KoyFont}")]
    [InlineData("<Button xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"/>")]
    [InlineData("<ObjectDataProvider/>")]
    [InlineData("Arial\"/><Button")]
    [InlineData("pack://application:,,,/Koy;component/Resources/#Font")]
    [InlineData("C:\\Windows\\Fonts\\arial.ttf")]
    [InlineData("\\\\server\\share\\font.ttf")]
    [InlineData("./fonts/#My Font")]
    [InlineData("file:///etc/passwd")]
    [InlineData("Arial;calc.exe")]
    [InlineData("Arial\u0000Bold")]
    [InlineData("Arial\nBold")]
    [InlineData("Arial\tBold")]
    [InlineData("Arial,")]
    [InlineData(",Arial")]
    [InlineData("Arial,,Segoe UI")]
    [InlineData(",")]
    [InlineData("   ")]
    public void Xaml_markup_paths_and_control_characters_are_never_accepted_as_font_names(string value)
    {
        var json = $$"""{ "KoyFont": {{System.Text.Json.JsonSerializer.Serialize(value)}} }""";
        var r = Parse(json);

        Assert.Empty(r.ValidEntries);
        Assert.Single(r.Diagnostics);
    }

    [Theory]
    [InlineData("KoyAccent")]
    [InlineData("KoyRadiusRow")]
    [InlineData("KoyFontBody")]
    public void Xaml_markup_is_never_accepted_in_non_font_values(string key)
    {
        foreach (var value in new[]
                 {
                     "{x:Static Application.Current}",
                     "<SolidColorBrush Color=\"Red\"/>",
                     "#112233<Window/>",
                     "{Binding Path=Foo}"
                 })
        {
            var json = $$"""{ "{{key}}": {{System.Text.Json.JsonSerializer.Serialize(value)}} }""";
            var r = Parse(json);

            Assert.Empty(r.ValidEntries);
            Assert.Single(r.Diagnostics);
        }
    }

    [Fact]
    public void Type_names_in_keys_do_not_instantiate_anything_and_are_reported_as_unknown()
    {
        var r = Parse("""{ "System.Diagnostics.Process": "calc", "$type": "System.Object", "__proto__": "x" }""");

        Assert.Empty(r.ValidEntries);
        Assert.Equal(3, r.Diagnostics.Count);
        Assert.All(r.Diagnostics, d => Assert.Equal(UserStyleDiagnosticCode.UnknownToken, d.Code));
    }

    [Fact]
    public void Parsed_result_is_immutable_through_casts()
    {
        var r = Parse("""{ "KoyAccent": "#112233" }""");

        var dict = Assert.IsAssignableFrom<IDictionary<string, UserStyleValue>>(r.ValidEntries);
        Assert.Throws<NotSupportedException>(() => dict["KoyBgRow"] = UserStyleValue.ForNumber(1));
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, UserStyleValue>)UserStyleParseResult.Empty.ValidEntries)["x"] = UserStyleValue.ForNumber(1));
    }

    // --- Allow-list ile XAML eşitliği ---

    [Fact]
    public void Allow_list_matches_the_keys_defined_in_the_theme_and_token_dictionaries()
    {
        var expected = new Dictionary<string, UserStyleKind>();
        foreach (var file in new[] { "Theme.Dark.xaml", "Theme.Light.xaml", "Tokens.xaml" })
        {
            var text = File.ReadAllText(Path.Combine(ResourcesDir(), file));
            foreach (Match m in Regex.Matches(text, "<(?<tag>SolidColorBrush|Color|sys:Double|CornerRadius|FontFamily|Thickness)\\s+x:Key=\"(?<key>\\w+)\""))
            {
                var kind = m.Groups["tag"].Value switch
                {
                    "SolidColorBrush" => UserStyleKind.Brush,
                    "Color" => UserStyleKind.Color,
                    "sys:Double" => UserStyleKind.Number,
                    "CornerRadius" => UserStyleKind.CornerRadius,
                    "FontFamily" => UserStyleKind.FontFamily,
                    _ => UserStyleKind.Thickness
                };
                expected[m.Groups["key"].Value] = kind;
            }
        }

        Assert.NotEmpty(expected);
        var actual = AllowedTokens.All.ToDictionary(p => p.Key, p => p.Value.Kind);
        Assert.Equal(expected.OrderBy(p => p.Key), actual.OrderBy(p => p.Key));
    }

    [Fact]
    public void Every_allowed_token_has_a_sane_definition()
    {
        foreach (var (key, token) in AllowedTokens.All)
        {
            Assert.Equal(key, token.Key);
            Assert.StartsWith("Koy", key);
            if (token.Kind is UserStyleKind.Number or UserStyleKind.CornerRadius or UserStyleKind.Thickness)
            {
                Assert.True(token.Min >= 0, key);
                Assert.True(token.Max > token.Min, key);
                Assert.True(token.Max <= 100, key);
            }
        }
    }

    [Fact]
    public void Default_theme_values_fall_inside_their_own_bounds()
    {
        // Varsayılan değer izinli aralıkta olmalı; yoksa "varsayılanı yaz" bile reddedilirdi.
        var text = File.ReadAllText(Path.Combine(ResourcesDir(), "Tokens.xaml"));
        foreach (Match m in Regex.Matches(text, "<sys:Double x:Key=\"(?<key>\\w+)\">(?<v>[\\d.]+)</sys:Double>"))
        {
            var token = AllowedTokens.All[m.Groups["key"].Value];
            var value = double.Parse(m.Groups["v"].Value, System.Globalization.CultureInfo.InvariantCulture);
            Assert.InRange(value, token.Min, token.Max);
        }
    }

    [Fact]
    public void Sample_file_from_the_docs_parses_without_diagnostics()
    {
        var r = Parse("""
            {
              "KoyAccent": "#8B5CF6",
              "KoyAccentHover": "#A78BFA",
              "KoyBgWindow": "#101014",
              "KoyScrim": "#B3000000",
              "KoyRadiusRow": "10",
              "KoyRadiusWindow": "16,16,16,16",
              "KoyFontBody": 14,
              "KoyFont": "Segoe UI Variable Text, Segoe UI, sans-serif"
            }
            """);

        Assert.Empty(r.Diagnostics);
        Assert.Equal(8, r.ValidEntries.Count);
    }

    // --- Ayar ---

    [Fact]
    public void Setting_is_off_by_default_and_survives_a_save_and_load_round_trip()
    {
        Assert.False(new Koy.Core.Settings.AppSettings().Appearance.UserStyleOverridesEnabled);

        var dir = Path.Combine(Path.GetTempPath(), "koy-userstyle-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new Koy.Core.Settings.SettingsStore(Path.Combine(dir, "settings.json"));
            var settings = new Koy.Core.Settings.AppSettings
            {
                Appearance = new Koy.Core.Settings.AppearanceSettings { UserStyleOverridesEnabled = true }
            };

            store.Save(settings);

            Assert.True(store.Load().Appearance.UserStyleOverridesEnabled);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    // --- Dosya okuma ---

    [Fact]
    public void Missing_file_is_an_empty_result_without_diagnostics()
    {
        var r = UserStyleFile.Load(Path.Combine(Path.GetTempPath(), "koy-yok-" + Guid.NewGuid() + ".json"));

        Assert.Empty(r.ValidEntries);
        Assert.Empty(r.Diagnostics);
        Assert.False(UserStyleFile.GetStamp(Path.Combine(Path.GetTempPath(), "koy-yok-" + Guid.NewGuid())).Exists);
    }

    [Fact]
    public void File_with_bom_is_read_and_stamp_changes_when_content_changes()
    {
        var path = TempFile();
        try
        {
            File.WriteAllText(path, "\uFEFF{ \"KoyAccent\": \"#112233\" }", new UTF8Encoding(true));
            var first = UserStyleFile.GetStamp(path);

            var r = UserStyleFile.Load(path);
            Assert.Empty(r.Diagnostics);
            Assert.Single(r.ValidEntries);
            Assert.True(first.Exists);

            File.WriteAllText(path, "{ \"KoyAccent\": \"#112233\", \"KoyBgRow\": \"#000000\" }");
            File.SetLastWriteTimeUtc(path, first.LastWriteUtc.AddSeconds(5));
            Assert.NotEqual(first, UserStyleFile.GetStamp(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Oversize_file_is_rejected_by_length_without_parsing()
    {
        var path = TempFile();
        try
        {
            File.WriteAllText(path, new string(' ', UserStyleParser.MaxBytes + 10));

            var r = UserStyleFile.Load(path);

            Assert.Empty(r.ValidEntries);
            Assert.Equal(UserStyleDiagnosticCode.FileTooLarge, Assert.Single(r.Diagnostics).Code);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Directory_in_place_of_the_file_does_not_throw()
    {
        var dir = Path.Combine(Path.GetTempPath(), "koy-dizin-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        try
        {
            var r = UserStyleFile.Load(dir);
            Assert.Empty(r.ValidEntries);
            Assert.Empty(r.Diagnostics); // FileInfo.Exists dizin için false
        }
        finally
        {
            Directory.Delete(dir);
        }
    }

    [Fact]
    public void Invalid_utf8_bytes_do_not_throw()
    {
        var path = TempFile();
        try
        {
            File.WriteAllBytes(path, [0x7B, 0xFF, 0xFE, 0xFD, 0x7D]);

            var r = UserStyleFile.Load(path);

            Assert.Empty(r.ValidEntries);
            Assert.Single(r.Diagnostics);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // --- Mağaza tema paketleri ---

    public static IEnumerable<object[]> StoreThemes() =>
        new[] { "koyhq.nord-theme", "koyhq.solarized-theme", "koyhq.high-contrast-theme" }.Select(id => new object[] { id });

    [Theory]
    [MemberData(nameof(StoreThemes))]
    public void Store_theme_packs_parse_cleanly_and_paint_window_notch_and_dock(string id)
    {
        var path = Path.Combine(ResourcesDir(), "..", "..", "..", "store", "src", id, "theme.json");
        var result = UserStyleFile.Load(Path.GetFullPath(path));

        Assert.Empty(result.Diagnostics);
        Assert.Contains(result.ValidEntries.Keys, k => k.StartsWith("KoyNotch", StringComparison.Ordinal));
        // Çentik zemini, kartları, yazısı ve dock zemini her pakette verilir; yoksa o yüzey temadan kopuk kalır.
        foreach (var key in new[]
                 {
                     "KoyNotchSurface", "KoyNotchCard", "KoyNotchChip", "KoyNotchText", "KoyNotchTextSecondary", "KoyNotchTextMuted",
                     "KoyNotchHover", "KoyNotchDivider", "KoyNotchMusicCard", "KoyNotchAccentControl", "KoyNotchAccentFocus",
                     "KoyDockSurface", "KoyDockCapsuleSurface", "KoyDockBorder", "KoyDockIndicator", "KoyDockText", "KoyDockHover"
                 })
            Assert.True(result.ValidEntries.ContainsKey(key), $"{id}: {key}");
        Assert.True(result.ValidEntries.Count < UserStyleParser.MaxEntries);
    }

    private static string TempFile() => Path.Combine(Path.GetTempPath(), "koy-user-theme-" + Guid.NewGuid() + ".json");

    private static string ResourcesDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Koy.slnx")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "src", "App", "Resources");
    }
}
