using Koy.Core.JumpList;
using static Koy.Core.Tests.JumpList.CustomDestinationsBuilder;

namespace Koy.Core.Tests.JumpList;

public class CustomDestinationsReaderTests
{
    [Fact]
    public void Reads_tasks_custom_and_known_categories_in_file_order()
    {
        var data = new CustomDestinationsBuilder()
            .Tasks(Link(), Link(["a"]))
            .Custom("Son klasörler", Link(), Link(), Link())
            .Known(2)
            .Build();

        var file = CustomDestinationsReader.Parse(data);

        Assert.Collection(file.Categories,
            c =>
            {
                Assert.Equal(CustomDestinationsCategoryKind.Tasks, c.Kind);
                Assert.Equal(2, c.Items.Count);
            },
            c =>
            {
                Assert.Equal(CustomDestinationsCategoryKind.Custom, c.Kind);
                Assert.Equal("Son klasörler", c.Name);
                Assert.Equal(3, c.Items.Count);
            },
            c =>
            {
                Assert.Equal(CustomDestinationsCategoryKind.Known, c.Kind);
                Assert.Equal(JumpListKnownCategory.Recent, c.Known);
                Assert.Empty(c.Items);
            });
        Assert.True(file.Declares(JumpListKnownCategory.Recent));
        Assert.False(file.Declares(JumpListKnownCategory.Frequent));
    }

    [Fact]
    public void Item_bytes_are_the_link_stream_without_the_clsid()
    {
        var link = Link(["--yeni"]);
        var file = CustomDestinationsReader.Parse(new CustomDestinationsBuilder().Tasks(link).Build());

        Assert.Equal(link, file.Categories[0].Items[0]);
    }

    [Fact]
    public void Frequent_known_category_is_recognised()
    {
        var file = CustomDestinationsReader.Parse(new CustomDestinationsBuilder().Known(1).Build());

        Assert.True(file.Declares(JumpListKnownCategory.Frequent));
    }

    [Fact]
    public void Unknown_known_category_id_is_skipped_but_file_stays_valid()
    {
        var file = CustomDestinationsReader.Parse(new CustomDestinationsBuilder().Known(-1).Tasks(Link()).Build());

        var only = Assert.Single(file.Categories);
        Assert.Equal(CustomDestinationsCategoryKind.Tasks, only.Kind);
    }

    [Fact]
    public void Measures_links_with_idlist_linkinfo_ansi_strings_and_extra_blocks()
    {
        var data = new CustomDestinationsBuilder()
            .Tasks(
                Link(["gorev"], 0x04 | 0x20, unicode: false, idListBytes: 10, linkInfoBytes: 20, extraBlocks: 2),
                Link(["ad", "yol", "klasor", "arg", "simge"], 0x04 | 0x08 | 0x10 | 0x20 | 0x40, idListBytes: 0, linkInfoBytes: 0, extraBlocks: 1),
                Link(strings: [], stringFlags: 0))
            .Build();

        var file = CustomDestinationsReader.Parse(data);

        Assert.Equal(3, Assert.Single(file.Categories).Items.Count);
    }

    [Fact]
    public void Custom_category_with_empty_name_is_read()
    {
        var file = CustomDestinationsReader.Parse(new CustomDestinationsBuilder().Custom("", Link()).Build());

        Assert.Equal(string.Empty, Assert.Single(file.Categories).Name);
    }

    // --- Bozuk / kısaltılmış / bilinmeyen biçim: istisna yok, boş sonuç ---

    [Fact]
    public void Empty_and_tiny_inputs_are_empty()
    {
        Assert.True(CustomDestinationsReader.Parse([]).IsEmpty);
        Assert.True(CustomDestinationsReader.Parse(new byte[5]).IsEmpty);
        Assert.True(CustomDestinationsReader.Parse(new byte[12]).IsEmpty);
    }

    [Fact]
    public void Unknown_version_is_empty()
    {
        var data = new CustomDestinationsBuilder().Tasks(Link()).Build(version: 3);

        Assert.True(CustomDestinationsReader.Parse(data).IsEmpty);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(JumpListLimits.MaxCategories + 1)]
    public void Bad_category_count_is_empty(int count)
    {
        var data = new CustomDestinationsBuilder().Tasks(Link()).Build(categoryCount: count);

        Assert.True(CustomDestinationsReader.Parse(data).IsEmpty);
    }

    [Fact]
    public void Every_truncation_of_a_valid_file_is_empty_and_never_throws()
    {
        var data = new CustomDestinationsBuilder()
            .Tasks(Link(), Link(["x", "y"], 0x04 | 0x20, idListBytes: 6, linkInfoBytes: 8, extraBlocks: 1))
            .Custom("Kategori", Link())
            .Known(2)
            .Build();

        Assert.False(CustomDestinationsReader.Parse(data).IsEmpty); // sağlam hâli okunur

        for (var length = 0; length < data.Length; length++)
        {
            var truncated = data.AsSpan(0, length).ToArray();
            Assert.True(CustomDestinationsReader.Parse(truncated).IsEmpty, $"uzunluk {length}");
        }
    }

    [Fact]
    public void Missing_footer_is_empty()
    {
        var data = new CustomDestinationsBuilder().Tasks(Link()).Build();
        data[^1] ^= 0xFF;

        Assert.True(CustomDestinationsReader.Parse(data).IsEmpty);
    }

    [Fact]
    public void Item_that_is_not_a_shell_link_is_empty()
    {
        var data = new CustomDestinationsBuilder().Tasks(Link()).Build();
        data[12 + 4 + 4] ^= 0xFF; // ilk öğenin CLSID'inin ilk baytı

        Assert.True(CustomDestinationsReader.Parse(data).IsEmpty);
    }

    [Fact]
    public void Unknown_category_type_is_empty()
    {
        var data = new CustomDestinationsBuilder().Tasks(Link()).Build();
        data[12] = 7;

        Assert.True(CustomDestinationsReader.Parse(data).IsEmpty);
    }

    [Fact]
    public void Link_header_with_wrong_size_is_empty()
    {
        var link = Link();
        link[0] = 0x10;

        Assert.True(CustomDestinationsReader.Parse(new CustomDestinationsBuilder().Tasks(link).Build()).IsEmpty);
    }

    [Fact]
    public void Extra_block_size_pointing_past_the_end_is_empty()
    {
        var link = Link(extraBlocks: 1);
        // ek bloğun boyutunu dosyadan büyük yap
        var blockOffset = link.Length - 4 - 12;
        BitConverter.GetBytes(0x7FFFFFF0u).CopyTo(link, blockOffset);

        Assert.True(CustomDestinationsReader.Parse(new CustomDestinationsBuilder().Tasks(link).Build()).IsEmpty);
    }

    [Fact]
    public void Link_info_size_below_four_is_empty()
    {
        var link = Link(linkInfoBytes: 0);
        var infoOffset = 0x4C;
        BitConverter.GetBytes(2u).CopyTo(link, infoOffset);

        Assert.True(CustomDestinationsReader.Parse(new CustomDestinationsBuilder().Tasks(link).Build()).IsEmpty);
    }

    [Fact]
    public void Negative_item_count_is_empty()
    {
        var data = new CustomDestinationsBuilder().Tasks(Link()).Build();
        BitConverter.GetBytes(-5).CopyTo(data, 12 + 4);

        Assert.True(CustomDestinationsReader.Parse(data).IsEmpty);
    }

    [Fact]
    public void Item_count_over_the_limit_is_empty()
    {
        var items = Enumerable.Range(0, JumpListLimits.MaxItemsPerCategory + 1).Select(_ => Link()).ToArray();

        Assert.True(CustomDestinationsReader.Parse(new CustomDestinationsBuilder().Tasks(items).Build()).IsEmpty);
    }

    [Fact]
    public void Item_count_at_the_limit_is_read()
    {
        var items = Enumerable.Range(0, JumpListLimits.MaxItemsPerCategory).Select(_ => Link()).ToArray();

        var file = CustomDestinationsReader.Parse(new CustomDestinationsBuilder().Tasks(items).Build());

        Assert.Equal(JumpListLimits.MaxItemsPerCategory, Assert.Single(file.Categories).Items.Count);
    }

    [Fact]
    public void Huge_declared_item_count_does_not_allocate_or_throw()
    {
        var data = new CustomDestinationsBuilder().Tasks(Link()).Build();
        BitConverter.GetBytes(int.MaxValue).CopyTo(data, 12 + 4);

        Assert.True(CustomDestinationsReader.Parse(data).IsEmpty);
    }

    [Fact]
    public void Total_item_count_over_the_limit_is_empty()
    {
        var builder = new CustomDestinationsBuilder();
        // 5 kategori x 64 öğe = 320 > 256
        for (var c = 0; c < 5; c++)
            builder.Custom("k" + c, Enumerable.Range(0, JumpListLimits.MaxItemsPerCategory).Select(_ => Link()).ToArray());

        Assert.True(CustomDestinationsReader.Parse(builder.Build()).IsEmpty);
    }

    [Fact]
    public void Category_name_over_the_limit_is_empty()
    {
        var name = new string('a', JumpListLimits.MaxCategoryNameChars + 1);

        Assert.True(CustomDestinationsReader.Parse(new CustomDestinationsBuilder().Custom(name, Link()).Build()).IsEmpty);
    }

    [Fact]
    public void Negative_name_length_is_empty()
    {
        var data = new CustomDestinationsBuilder().Custom("ab", Link()).Build();
        BitConverter.GetBytes((short)-3).CopyTo(data, 12 + 4);

        Assert.True(CustomDestinationsReader.Parse(data).IsEmpty);
    }

    [Fact]
    public void File_over_the_size_limit_is_empty()
    {
        var data = new CustomDestinationsBuilder().Tasks(Link()).Build();
        var big = new byte[JumpListLimits.MaxFileBytes + 1];
        data.CopyTo(big, 0);

        Assert.True(CustomDestinationsReader.Parse(big).IsEmpty);
    }

    [Fact]
    public void Single_item_over_the_size_limit_is_empty()
    {
        var link = Link(idListBytes: 60000, linkInfoBytes: 10000);

        Assert.True(CustomDestinationsReader.Parse(new CustomDestinationsBuilder().Tasks(link).Build()).IsEmpty);
    }

    [Fact]
    public void Random_garbage_never_throws()
    {
        var rng = new Random(12345);
        var valid = new CustomDestinationsBuilder().Tasks(Link(), Link()).Custom("k", Link()).Known(2).Build();

        for (var round = 0; round < 3000; round++)
        {
            var data = (byte[])valid.Clone();
            var flips = rng.Next(1, 6);
            for (var i = 0; i < flips; i++)
                data[rng.Next(data.Length)] = (byte)rng.Next(256);

            // Sonuç boş ya da geçerli olabilir; önemli olan istisna fırlatmaması ve sınırları aşmaması.
            var file = CustomDestinationsReader.Parse(data);
            Assert.True(file.Categories.Count <= JumpListLimits.MaxCategories);
            Assert.True(file.Categories.Sum(c => c.Items.Count) <= JumpListLimits.MaxTotalItems);
        }

        var noise = new byte[4096];
        rng.NextBytes(noise);
        Assert.True(CustomDestinationsReader.Parse(noise).IsEmpty);
    }

    [Fact]
    public void Header_encoding_is_utf16()
    {
        var file = CustomDestinationsReader.Parse(new CustomDestinationsBuilder().Custom("İşlem ✓", Link()).Build());

        Assert.Equal("İşlem ✓", file.Categories[0].Name);
    }
}
