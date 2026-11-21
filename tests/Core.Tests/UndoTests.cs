using Koy.Core.Appearance;
using Koy.Core.Calendar;
using Koy.Core.Clipboard;
using Koy.Core.Notch;
using Koy.Core.Tasks;
using Koy.Core.Undo;

namespace Koy.Core.Tests;

// Paket: çentik erişilebilirliği ve güvenli silme (geri al, form hataları, Yüksek Karşıtlık tablosu).
public class UndoBufferTests
{
    [Fact]
    public void Undo_runs_the_action_once_and_clears()
    {
        var clock = new FakeClock();
        var buffer = new UndoBuffer(clock);
        var count = 0;
        buffer.Push(UndoKind.ShelfCleared, 3, () => count++);

        Assert.Equal(UndoKind.ShelfCleared, buffer.Current!.Kind);
        Assert.Equal(3, buffer.Current.Count);
        Assert.True(buffer.TryUndo());
        Assert.Equal(1, count);
        Assert.Null(buffer.Current);
        Assert.False(buffer.TryUndo());
        Assert.Equal(1, count);
    }

    [Fact]
    public void Entry_expires_after_the_window_and_cannot_be_undone()
    {
        var clock = new FakeClock();
        var buffer = new UndoBuffer(clock);
        var undone = false;
        buffer.Push(UndoKind.EventDeleted, 1, () => undone = true);

        clock.Advance(UndoBuffer.DefaultWindow - TimeSpan.FromMilliseconds(1));
        Assert.False(buffer.ExpireIfDue());
        Assert.True(buffer.Remaining > TimeSpan.Zero);

        clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(TimeSpan.Zero, buffer.Remaining);
        Assert.True(buffer.ExpireIfDue());
        Assert.Null(buffer.Current);
        Assert.False(buffer.TryUndo());
        Assert.False(undone);
    }

    [Fact]
    public void Late_undo_after_expiry_without_timer_is_refused()
    {
        var clock = new FakeClock();
        var buffer = new UndoBuffer(clock);
        var undone = false;
        buffer.Push(UndoKind.TaskDeleted, 1, () => undone = true);

        clock.Advance(TimeSpan.FromSeconds(6));

        Assert.False(buffer.TryUndo());
        Assert.False(undone);
        Assert.Null(buffer.Current);
    }

    [Fact]
    public void New_deletion_replaces_the_previous_one()
    {
        var buffer = new UndoBuffer(new FakeClock());
        var first = false;
        var second = false;
        buffer.Push(UndoKind.ClipboardCleared, 5, () => first = true);
        buffer.Push(UndoKind.ShelfCleared, 2, () => second = true);

        Assert.True(buffer.TryUndo());
        Assert.False(first);
        Assert.True(second);
    }

    [Fact]
    public void Nothing_deleted_opens_no_entry_and_clear_resets()
    {
        var buffer = new UndoBuffer(new FakeClock());
        var changes = 0;
        buffer.Changed += (_, _) => changes++;

        buffer.Push(UndoKind.ClipboardCleared, 0, () => { });
        Assert.Null(buffer.Current);
        Assert.Equal(0, changes);

        buffer.Push(UndoKind.ClipboardCleared, 1, () => { });
        buffer.Clear();
        Assert.Null(buffer.Current);
        Assert.Equal(2, changes);
        buffer.Clear();
        Assert.Equal(2, changes);
    }

    [Fact]
    public void Changed_fires_on_push_undo_and_expiry()
    {
        var clock = new FakeClock();
        var buffer = new UndoBuffer(clock);
        var changes = 0;
        buffer.Changed += (_, _) => changes++;

        buffer.Push(UndoKind.EventDeleted, 1, () => { });
        buffer.TryUndo();
        buffer.Push(UndoKind.EventDeleted, 1, () => { });
        clock.Advance(TimeSpan.FromSeconds(5));
        buffer.ExpireIfDue();

        Assert.Equal(4, changes);
    }
}

public class RestoreTests
{
    [Fact]
    public void EventBook_restores_a_deleted_local_event_with_same_id_in_order()
    {
        var book = new EventBook();
        var day = new DateOnly(2026, 11, 21);
        var early = book.Add("sabah", day, new TimeOnly(8, 0))!;
        var late = book.Add("akşam", day, new TimeOnly(20, 0))!;
        book.Remove(early.Id);
        Assert.Single(book.LocalEvents);

        Assert.True(book.Restore(early));

        Assert.Equal([early.Id, late.Id], book.LocalEvents.Select(e => e.Id));
        Assert.False(book.Restore(early)); // zaten var
    }

    [Fact]
    public void EventBook_does_not_restore_external_events()
    {
        var book = new EventBook();
        var external = new CalendarEvent("g1", "Harici", new DateTime(2026, 11, 21, 9, 0, 0), 30, CalendarEventSource.Google);
        Assert.False(book.Restore(external));
    }

    [Fact]
    public void TaskService_restores_a_removed_task_with_done_state()
    {
        var dir = Path.Combine(Path.GetTempPath(), "koy-undo-" + Guid.NewGuid().ToString("N"));
        try
        {
            var now = new DateTimeOffset(2026, 11, 21, 10, 0, 0, TimeSpan.Zero);
            var tasks = new TaskService(Path.Combine(dir, "tasks.json"), () => now);
            var item = tasks.Add("rapor", new TimeOnly(15, 0))!;
            tasks.SetDone(item.Id, true);
            var snapshot = tasks.GetToday().Single();

            Assert.True(tasks.Remove(item.Id));
            Assert.Empty(tasks.GetToday());
            Assert.True(tasks.Restore(snapshot));

            var back = tasks.GetToday().Single();
            Assert.Equal(item.Id, back.Id);
            Assert.True(back.Done);
            Assert.False(tasks.Restore(snapshot));
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void ClipboardHistory_restores_cleared_items_in_time_order_without_duplicates()
    {
        var t = new DateTimeOffset(2026, 11, 21, 10, 0, 0, TimeSpan.Zero);
        var history = new ClipboardHistory(() => t);
        history.Add("bir");
        t = t.AddSeconds(1);
        history.Add("iki");
        t = t.AddSeconds(1);
        var pinned = history.Add("sabit")!;
        history.SetPinned(pinned.Id, true);

        var removed = history.Query().Where(i => !i.Pinned).ToList();
        Assert.Equal(2, history.Clear());
        t = t.AddSeconds(1);
        history.Add("yeni");

        Assert.Equal(2, history.Restore(removed));
        Assert.Equal(0, history.Restore(removed)); // ikinci geri alma bir şey eklemez

        // En yeni başta: yeni, sabit, iki, bir.
        Assert.Equal(["yeni", "sabit", "iki", "bir"], history.Query().Select(i => i.Text));
    }

    [Fact]
    public void ClipboardHistory_restore_skips_text_that_was_copied_again()
    {
        var history = new ClipboardHistory();
        var first = history.Add("aynı")!;
        history.Remove(first.Id);
        history.Add("aynı");

        Assert.Equal(0, history.Restore([first]));
        Assert.Single(history.Query());
    }
}

public class QuickAddValidationTests
{
    [Theory]
    [InlineData("Toplantı", "09:30", QuickAddError.None)]
    [InlineData("Toplantı", "0930", QuickAddError.None)]
    [InlineData("", "09:30", QuickAddError.TitleEmpty)]
    [InlineData("   ", "xx", QuickAddError.TitleEmpty)]
    [InlineData("Toplantı", "25:00", QuickAddError.TimeInvalid)]
    [InlineData("Toplantı", "", QuickAddError.TimeInvalid)]
    public void Event_form(string title, string time, QuickAddError expected) =>
        Assert.Equal(expected, QuickAddValidation.ForEvent(title, time));

    [Theory]
    [InlineData("rapor", "", QuickAddError.None)]
    [InlineData("rapor", "14:30", QuickAddError.None)]
    [InlineData("", "14:30", QuickAddError.TitleEmpty)]
    [InlineData("rapor", "99:99", QuickAddError.TimeInvalid)]
    [InlineData("rapor", "abc", QuickAddError.TimeInvalid)]
    public void Task_form_time_is_optional(string title, string time, QuickAddError expected) =>
        Assert.Equal(expected, QuickAddValidation.ForTask(title, time));
}

public class NotchHighContrastMapTests
{
    [Fact]
    public void Every_key_is_mapped_once()
    {
        var keys = NotchHighContrastMap.Entries.Select(e => e.Key).ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    [Fact]
    public void Surface_is_background_and_text_is_text_and_borders_are_not_background()
    {
        var map = NotchHighContrastMap.Entries.ToDictionary(e => e.Key, e => e.Role);
        Assert.Equal(HighContrastRole.Background, map["KoyNotchSurface"]);
        Assert.Equal(HighContrastRole.Text, map["KoyNotchText"]);
        Assert.Equal(HighContrastRole.Text, map["KoyNotchFocusRing"]);
        Assert.Equal(HighContrastRole.Border, map["KoyNotchCardBorder"]);
        Assert.Equal(HighContrastRole.Track, map["KoyNotchTrack"]);
    }

    [Fact]
    public void Map_covers_text_surface_and_border_tokens_present_in_tokens_file()
    {
        // Token dosyasında olmayan bir anahtar eşlemede bırakılırsa boşuna sözlük yazılır: gerçek dosyayla karşılaştır.
        var tokens = File.ReadAllText(FindTokens());
        foreach (var (key, _) in NotchHighContrastMap.Entries)
            Assert.Contains($"x:Key=\"{key}\"", tokens);
    }

    private static string FindTokens()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "src", "App", "Resources", "Tokens.xaml")))
            dir = Path.GetDirectoryName(dir);
        return Path.Combine(dir ?? throw new FileNotFoundException("Tokens.xaml bulunamadı"), "src", "App", "Resources", "Tokens.xaml");
    }
}
