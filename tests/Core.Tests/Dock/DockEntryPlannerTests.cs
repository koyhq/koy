using Koy.Core.Dock;

namespace Koy.Core.Tests.Dock;

public class DockEntryPlannerTests
{
    private static DockItem Pinned(string lnk) => Model($"C:\\Pins\\{lnk}", pinned: true);

    private static DockItem Model(string id, bool pinned) =>
        new(id, Path.GetFileNameWithoutExtension(id), id, null, null, pinned, false, false, []);

    private static string[] Ids(DockEntryPlan plan) =>
        plan.Entries.Select(e => e switch
        {
            PlannedItem i => i.Model.Id,
            PlannedFolder f => "folder:" + f.Folder.Id,
            _ => "?"
        }).ToArray();

    [Fact]
    public void Pinned_items_follow_the_saved_order_then_unpinned_windows()
    {
        var models = new[] { Pinned("B.lnk"), Pinned("A.lnk"), Model("C:\\x\\run.exe:1", pinned: false) };

        var plan = DockEntryPlanner.Plan(models, ["A.lnk", "B.lnk"], []);

        Assert.Equal(["C:\\Pins\\A.lnk", "C:\\Pins\\B.lnk", "C:\\x\\run.exe:1"], Ids(plan));
        Assert.Equal(["A.lnk", "B.lnk"], plan.EffectiveOrder);
    }

    [Fact]
    public void Apps_inside_a_folder_are_not_listed_separately()
    {
        var models = new[] { Pinned("A.lnk"), Pinned("B.lnk"), Pinned("C.lnk") };
        var folder = new DockFolder("f1", "Tools", ["A.lnk", "B.lnk"]);

        var plan = DockEntryPlanner.Plan(models, ["A.lnk", PinnedOrder.ToFolderToken("f1"), "C.lnk"], [folder]);

        Assert.Equal(["folder:f1", "C:\\Pins\\C.lnk"], Ids(plan));
        var planned = Assert.IsType<PlannedFolder>(plan.Entries[0]);
        Assert.Equal(["A.lnk", "B.lnk"], planned.Members.Select(m => m.ItemId));
        Assert.All(planned.Members, m => Assert.False(m.IsFallback));
    }

    [Fact]
    public void Folder_members_without_a_model_get_a_fallback_built_from_the_file_name()
    {
        // Hiç model yokken (sabit liste henüz okunmadı) Reconcile kayıtlı sırayı olduğu gibi tutar.
        var folder = new DockFolder("f1", "Tools", ["Gone.lnk", "Away.lnk"]);

        var plan = DockEntryPlanner.Plan([], [PinnedOrder.ToFolderToken("f1")], [folder]);

        var planned = Assert.IsType<PlannedFolder>(Assert.Single(plan.Entries));
        Assert.All(planned.Members, m => Assert.True(m.IsFallback));
        Assert.Equal(["Gone", "Away"], planned.Members.Select(m => m.Model.Title));
        Assert.All(planned.Members, m => Assert.True(m.Model.IsPinned && !m.Model.IsRunning));
    }

    [Fact]
    public void Unpinned_window_whose_app_is_in_a_folder_is_hidden()
    {
        var running = Model("C:\\x\\A.lnk", pinned: false);
        var folder = new DockFolder("f1", "F", ["A.lnk", "B.lnk"]);

        var plan = DockEntryPlanner.Plan([running, Pinned("A.lnk"), Pinned("B.lnk")], [PinnedOrder.ToFolderToken("f1")], [folder]);

        Assert.Equal(["folder:f1"], Ids(plan));
    }

    [Fact]
    public void Item_models_use_the_lookup_for_new_views_and_the_listed_model_for_updates()
    {
        var running = Model("C:\\x\\run.exe:1", pinned: false);

        var plan = DockEntryPlanner.Plan([running], [], []);

        var item = Assert.IsType<PlannedItem>(Assert.Single(plan.Entries));
        Assert.Same(running, item.Model);
        Assert.Same(running, item.UpdateModel);
    }

    [Fact]
    public void Empty_input_plans_nothing()
    {
        var plan = DockEntryPlanner.Plan([], [], []);

        Assert.Empty(plan.Entries);
        Assert.Empty(plan.EffectiveOrder);
    }

    // --- DockListSync ---

    private static List<string> Apply(List<string> current, string[] desired)
    {
        var list = current.ToList();
        foreach (var op in DockListSync.Plan(current, desired))
        {
            switch (op.Kind)
            {
                case DockListOpKind.Remove: list.RemoveAt(op.Index); break;
                case DockListOpKind.Move:
                    var moved = list[op.Index];
                    list.RemoveAt(op.Index);
                    list.Insert(op.ToIndex, moved);
                    break;
                case DockListOpKind.Insert: list.Insert(op.ToIndex, desired[op.ToIndex]); break;
                case DockListOpKind.Add: list.Add(desired[op.ToIndex]); break;
            }
        }

        return list;
    }

    [Theory]
    [InlineData("a,b,c", "a,b,c")]
    [InlineData("a,b,c", "c,b,a")]
    [InlineData("a,b,c", "b")]
    [InlineData("", "a,b")]
    [InlineData("a,b", "")]
    [InlineData("a,b,c,d", "d,x,a,y")]
    [InlineData("a,b,c", "x,a,b,c,y")]
    public void Applying_the_plan_yields_the_desired_order(string current, string desired)
    {
        var from = current.Length == 0 ? [] : current.Split(',').ToList();
        var to = desired.Length == 0 ? [] : desired.Split(',');

        Assert.Equal(to, Apply(from, to));
    }

    [Fact]
    public void Same_order_needs_no_operations() =>
        Assert.Empty(DockListSync.Plan(["a", "b"], ["a", "b"]));

    [Fact]
    public void Ids_compare_case_insensitively_so_existing_entries_are_kept()
    {
        var ops = DockListSync.Plan(["Chrome.lnk"], ["chrome.lnk"]);

        Assert.Empty(ops);
    }

    [Fact]
    public void Removals_run_from_the_end_so_indexes_stay_valid()
    {
        var ops = DockListSync.Plan(["a", "b", "c", "d"], ["b"]);

        Assert.Equal([3, 2, 0], ops.Where(o => o.Kind == DockListOpKind.Remove).Select(o => o.Index));
    }
}
