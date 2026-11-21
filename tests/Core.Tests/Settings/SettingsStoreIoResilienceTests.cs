using Koy.Core.Diagnostics;
using Koy.Core.Settings;

namespace Koy.Core.Tests.Settings;

/// <summary>Kilitli, salt okunur ya da geçici olarak tutulan ayar dosyası: kayıt/okuma fırlatmaz, dosya bozulmaz.</summary>
public class SettingsStoreIoResilienceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "koy-io-" + Guid.NewGuid().ToString("N"));

    public SettingsStoreIoResilienceTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try
        {
            foreach (var f in Directory.GetFiles(_dir))
                File.SetAttributes(f, FileAttributes.Normal);
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException) { }
        GC.SuppressFinalize(this);
    }

    private string PathFor() => Path.Combine(_dir, "settings.json");

    private static AppSettings WithNotch(bool enabled) => new AppSettings() with { Notch = new AppSettings().Notch with { Enabled = enabled } };

    [Fact]
    public void TrySave_succeeds_on_a_normal_file_and_leaves_no_temp()
    {
        var store = new SettingsStore(PathFor());

        Assert.True(store.TrySave(WithNotch(false), out var error));
        Assert.Null(error);
        Assert.False(File.Exists(PathFor() + ".tmp"));
        Assert.False(store.Load().Notch.Enabled);
    }

    [Fact]
    public void TrySave_returns_false_for_a_locked_file_keeps_old_content_and_cleans_temp()
    {
        var store = new SettingsStore(PathFor());
        Assert.True(store.TrySave(WithNotch(true), out _));
        var before = File.ReadAllText(PathFor());

        bool ok;
        Exception? error;
        using (new FileStream(PathFor(), FileMode.Open, FileAccess.Read, FileShare.None))
            ok = store.TrySave(WithNotch(false), out error);

        Assert.False(ok);
        Assert.True(error is IOException or UnauthorizedAccessException);
        Assert.Equal(before, File.ReadAllText(PathFor()));
        Assert.False(File.Exists(PathFor() + ".tmp"));
    }

    [Fact]
    public void Save_does_not_throw_when_the_file_is_locked()
    {
        var store = new SettingsStore(PathFor());
        store.Save(WithNotch(true));

        using (new FileStream(PathFor(), FileMode.Open, FileAccess.Read, FileShare.None))
            store.Save(WithNotch(false));

        Assert.True(store.Load().Notch.Enabled);
    }

    [Fact]
    public void TrySave_returns_false_for_a_read_only_file()
    {
        var store = new SettingsStore(PathFor());
        Assert.True(store.TrySave(WithNotch(true), out _));
        File.SetAttributes(PathFor(), FileAttributes.ReadOnly);

        var ok = store.TrySave(WithNotch(false), out var error);

        Assert.False(ok);
        Assert.IsType<UnauthorizedAccessException>(error);
        Assert.False(File.Exists(PathFor() + ".tmp"));
        Assert.True(store.Load().Notch.Enabled);
    }

    [Fact]
    public void TrySave_succeeds_when_the_lock_is_released_during_the_retries()
    {
        var store = new SettingsStore(PathFor());
        Assert.True(store.TrySave(WithNotch(true), out _));

        var held = new FileStream(PathFor(), FileMode.Open, FileAccess.Read, FileShare.None);
        var release = new Thread(() =>
        {
            Thread.Sleep(40);
            held.Dispose();
        });
        release.Start();

        var ok = store.TrySave(WithNotch(false), out var error);
        release.Join();

        Assert.True(ok);
        Assert.Null(error);
        Assert.False(store.Load().Notch.Enabled);
    }

    [Fact]
    public void Load_of_a_locked_file_returns_last_known_settings_and_does_not_touch_the_file()
    {
        var store = new SettingsStore(PathFor());
        Assert.True(store.TrySave(WithNotch(false), out _));
        Assert.False(store.Load().Notch.Enabled);
        var before = File.ReadAllBytes(PathFor());

        AppSettings loaded;
        using (new FileStream(PathFor(), FileMode.Open, FileAccess.Read, FileShare.None))
            loaded = store.Load();

        Assert.False(loaded.Notch.Enabled);
        Assert.Equal(before, File.ReadAllBytes(PathFor()));
    }

    [Fact]
    public void Load_of_a_locked_file_without_history_returns_defaults_without_writing()
    {
        File.WriteAllText(PathFor(), """{ "Version": 4, "Notch": { "Enabled": false } }""");
        var before = File.ReadAllBytes(PathFor());
        var store = new SettingsStore(PathFor());

        AppSettings loaded;
        using (new FileStream(PathFor(), FileMode.Open, FileAccess.Read, FileShare.None))
            loaded = store.Load();

        Assert.Equal(new AppSettings().Notch.Enabled, loaded.Notch.Enabled);
        Assert.Equal(before, File.ReadAllBytes(PathFor()));
        Assert.False(File.Exists(PathFor() + ".tmp"));
    }

    [Fact]
    public void Failure_streak_reports_start_once_and_end_once()
    {
        var streak = new SaveFailureStreak();

        Assert.Equal(SaveFailureStreak.Change.None, streak.Report(success: true));
        Assert.Equal(SaveFailureStreak.Change.Started, streak.Report(success: false));
        Assert.True(streak.IsFailing);
        Assert.Equal(SaveFailureStreak.Change.None, streak.Report(success: false));
        Assert.Equal(SaveFailureStreak.Change.Ended, streak.Report(success: true));
        Assert.False(streak.IsFailing);
        Assert.Equal(SaveFailureStreak.Change.Started, streak.Report(success: false));
    }
}

public class UiExceptionPolicyTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(typeof(OutOfMemoryException))]
    [InlineData(typeof(AccessViolationException))]
    [InlineData(typeof(InvalidProgramException))]
    [InlineData(typeof(BadImageFormatException))]
    public void Fatal_types_are_not_handled(Type type)
    {
        var ex = (Exception)Activator.CreateInstance(type)!;

        Assert.True(UiExceptionPolicy.IsFatal(ex));
        Assert.False(new UiExceptionPolicy().ShouldHandle(ex, T0));
    }

    [Fact]
    public void Fatal_inner_exception_is_found_through_wrappers()
    {
        var ex = new System.Reflection.TargetInvocationException(new AggregateException(new InvalidProgramException()));

        Assert.True(UiExceptionPolicy.IsFatal(ex));
    }

    [Theory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(InvalidOperationException))]
    [InlineData(typeof(NullReferenceException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    public void Ordinary_exceptions_are_handled(Type type)
    {
        var ex = (Exception)Activator.CreateInstance(type)!;

        Assert.False(UiExceptionPolicy.IsFatal(ex));
        Assert.True(new UiExceptionPolicy().ShouldHandle(ex, T0));
    }

    [Fact]
    public void Same_exception_looping_more_than_five_times_in_ten_seconds_is_allowed_to_close()
    {
        var policy = new UiExceptionPolicy();
        var ex = new InvalidOperationException("döngü");

        for (var i = 0; i < 5; i++)
            Assert.True(policy.ShouldHandle(ex, T0.AddSeconds(i)));

        Assert.False(policy.ShouldHandle(ex, T0.AddSeconds(6)));
    }

    [Fact]
    public void Spread_out_repeats_and_different_exceptions_do_not_trip_the_loop_guard()
    {
        var policy = new UiExceptionPolicy();
        var ex = new InvalidOperationException("yavaş");

        for (var i = 0; i < 20; i++)
            Assert.True(policy.ShouldHandle(ex, T0.AddSeconds(i * 11)));

        for (var i = 0; i < 20; i++)
            Assert.True(policy.ShouldHandle(new InvalidOperationException("farklı " + i), T0.AddSeconds(500)));
    }
}
