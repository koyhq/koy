using Koy.Core.JumpList;
using static Koy.Core.Tests.JumpList.CustomDestinationsBuilder;

namespace Koy.Core.Tests.JumpList;

public class JumpListLayoutTests
{
    private static JumpListEntry E(string title) =>
        new(title, new JumpListLaunch(@"C:\app\" + title + ".exe"));

    private static JumpListSection Section(JumpListSectionKind kind, string? name, params JumpListEntry[] entries) =>
        new(kind, name, entries);

    private static JumpListSection Many(JumpListSectionKind kind, string? name, int count) =>
        Section(kind, name, Enumerable.Range(1, count).Select(i => E($"{kind}{i}")).ToArray());

    [Fact]
    public void Sections_are_ordered_custom_frequent_recent_tasks()
    {
        var result = JumpListLayout.Arrange([
            Section(JumpListSectionKind.Tasks, null, E("t")),
            Section(JumpListSectionKind.Recent, null, E("r")),
            Section(JumpListSectionKind.Frequent, null, E("f")),
            Section(JumpListSectionKind.Custom, "Kategori A", E("a")),
            Section(JumpListSectionKind.Custom, "Kategori B", E("b")),
        ]);

        Assert.Equal(
            [JumpListSectionKind.Custom, JumpListSectionKind.Custom, JumpListSectionKind.Frequent, JumpListSectionKind.Recent, JumpListSectionKind.Tasks],
            result.Select(s => s.Kind));
        Assert.Equal(["Kategori A", "Kategori B"], result.Where(s => s.Kind == JumpListSectionKind.Custom).Select(s => s.Name));
    }

    [Fact]
    public void Empty_sections_and_nameless_categories_are_dropped()
    {
        var result = JumpListLayout.Arrange([
            Section(JumpListSectionKind.Recent, null),
            Section(JumpListSectionKind.Custom, "  ", E("a")),
            Section(JumpListSectionKind.Custom, null, E("a")),
            Section(JumpListSectionKind.Tasks, null, JumpListEntry.Separator),
            Section(JumpListSectionKind.Tasks, null, E("t")),
        ]);

        var only = Assert.Single(result);
        Assert.Equal(JumpListSectionKind.Tasks, only.Kind);
    }

    [Fact]
    public void Separators_are_normalised()
    {
        var sep = JumpListEntry.Separator;
        var result = JumpListLayout.Arrange([
            Section(JumpListSectionKind.Tasks, null, sep, E("a"), sep, sep, E("b"), sep),
        ]);

        var entries = Assert.Single(result).Entries;
        Assert.Equal(3, entries.Count);
        Assert.False(entries[0].IsSeparator);
        Assert.True(entries[1].IsSeparator);
        Assert.False(entries[2].IsSeparator);
    }

    [Fact]
    public void Per_section_caps_apply()
    {
        var result = JumpListLayout.Arrange([
            Many(JumpListSectionKind.Custom, "k", 50),
            Many(JumpListSectionKind.Tasks, null, 50),
        ]);

        Assert.True(result.Single(s => s.Kind == JumpListSectionKind.Custom).Entries.Count <= JumpListLimits.MaxPerCustomSection);
        Assert.True(result.Single(s => s.Kind == JumpListSectionKind.Tasks).Entries.Count <= JumpListLimits.MaxTasks);
    }

    [Fact]
    public void Individual_caps_are_exact_when_total_is_not_exceeded()
    {
        var result = JumpListLayout.Arrange([
            Many(JumpListSectionKind.Recent, null, 20),
            Many(JumpListSectionKind.Tasks, null, 20),
        ]);

        Assert.Equal(JumpListLimits.MaxRecent, result.Single(s => s.Kind == JumpListSectionKind.Recent).Entries.Count);
        Assert.Equal(JumpListLimits.MaxTasks, result.Single(s => s.Kind == JumpListSectionKind.Tasks).Entries.Count);
    }

    [Fact]
    public void Custom_section_count_is_capped_in_file_order()
    {
        var sections = Enumerable.Range(1, 8).Select(i => Section(JumpListSectionKind.Custom, "K" + i, E("x" + i))).ToArray();

        var result = JumpListLayout.Arrange(sections);

        Assert.Equal(JumpListLimits.MaxCustomSections, result.Count);
        Assert.Equal(["K1", "K2", "K3"], result.Select(s => s.Name));
    }

    [Fact]
    public void Total_cap_trims_frequent_then_recent_and_keeps_tasks()
    {
        var result = JumpListLayout.Arrange([
            Many(JumpListSectionKind.Custom, "A", 6),
            Many(JumpListSectionKind.Custom, "B", 6),
            Many(JumpListSectionKind.Custom, "C", 6),
            Many(JumpListSectionKind.Frequent, null, 3),
            Many(JumpListSectionKind.Recent, null, 5),
            Many(JumpListSectionKind.Tasks, null, 6),
        ]);

        var total = result.Sum(s => JumpListLayout.CountEntries(s.Entries));
        Assert.True(total <= JumpListLimits.MaxTotalEntries, $"toplam {total}");

        // Görevler (en sonda kırpılır) sınırından fazla küçülmemeli; her bölüm en az MinPerSection girdi korur.
        Assert.All(result, s => Assert.True(JumpListLayout.CountEntries(s.Entries) >= JumpListLimits.MinPerSectionWhenTrimming));
        var tasks = result.Single(s => s.Kind == JumpListSectionKind.Tasks);
        var recent = result.Single(s => s.Kind == JumpListSectionKind.Recent);
        Assert.True(JumpListLayout.CountEntries(tasks.Entries) >= JumpListLayout.CountEntries(recent.Entries));
    }

    [Fact]
    public void Total_cap_never_goes_below_minimum_per_section()
    {
        var all = new List<JumpListSection>();
        for (var i = 0; i < 20; i++)
            all.Add(Many(JumpListSectionKind.Custom, "K" + i, 6));
        all.Add(Many(JumpListSectionKind.Frequent, null, 9));
        all.Add(Many(JumpListSectionKind.Recent, null, 9));
        all.Add(Many(JumpListSectionKind.Tasks, null, 9));

        var result = JumpListLayout.Arrange(all);

        Assert.True(result.Sum(s => JumpListLayout.CountEntries(s.Entries)) <= JumpListLimits.MaxTotalEntries);
        Assert.Equal(6, result.Count);
    }

    [Fact]
    public void Trimming_does_not_leave_trailing_separators()
    {
        var sep = JumpListEntry.Separator;
        var result = JumpListLayout.Arrange([
            Section(JumpListSectionKind.Tasks, null, E("1"), E("2"), E("3"), E("4"), E("5"), E("6"), sep, E("7")),
        ]);

        var entries = Assert.Single(result).Entries;
        Assert.False(entries[^1].IsSeparator);
    }

    [Fact]
    public void Titles_in_category_name_are_sanitised()
    {
        var result = JumpListLayout.Arrange([Section(JumpListSectionKind.Custom, "Son\r\nkullanılan\t  klasörler", E("a"))]);

        Assert.Equal("Son kullanılan klasörler", Assert.Single(result).Name);
    }

    // --- Son / sık kullanılanlar hangi durumda gösterilir ---

    [Fact]
    public void Recent_is_shown_when_the_app_has_no_usable_list()
    {
        Assert.True(JumpListLayout.ShowsRecent(null));
        Assert.True(JumpListLayout.ShowsRecent(CustomDestinationsFile.Empty));
        Assert.False(JumpListLayout.ShowsFrequent(null));
        Assert.False(JumpListLayout.ShowsFrequent(CustomDestinationsFile.Empty));
    }

    [Fact]
    public void Valid_file_with_only_an_unrecognised_known_category_does_not_default_to_recent()
    {
        var file = CustomDestinationsReader.Parse(new CustomDestinationsBuilder().Known(-1).Build());

        Assert.True(file.IsValid);
        Assert.True(file.IsEmpty);
        Assert.False(JumpListLayout.ShowsRecent(file));
        Assert.False(JumpListLayout.ShowsFrequent(file));
    }

    [Fact]
    public void Known_categories_follow_what_the_app_declared()
    {
        var tasksOnly = CustomDestinationsReader.Parse(new CustomDestinationsBuilder().Tasks(Link()).Build());
        Assert.False(JumpListLayout.ShowsRecent(tasksOnly));
        Assert.False(JumpListLayout.ShowsFrequent(tasksOnly));

        var recent = CustomDestinationsReader.Parse(new CustomDestinationsBuilder().Tasks(Link()).Known(2).Build());
        Assert.True(JumpListLayout.ShowsRecent(recent));
        Assert.False(JumpListLayout.ShowsFrequent(recent));

        var frequent = CustomDestinationsReader.Parse(new CustomDestinationsBuilder().Known(1).Build());
        Assert.False(JumpListLayout.ShowsRecent(frequent));
        Assert.True(JumpListLayout.ShowsFrequent(frequent));
    }
}
