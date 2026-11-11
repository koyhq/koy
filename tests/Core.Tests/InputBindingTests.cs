using Koy.Core.Input;
using Koy.Core.Shortcuts;

namespace Koy.Core.Tests;

public class InputBindingTests
{
    private static InputBindingParseResult Parse(string? action, string? kind, string? trigger, int? dwell = null, int? pushBack = null, string? customId = null) =>
        InputBindingParser.Parse(new InputBindingData(action, kind, trigger, dwell, pushBack, customId));

    [Fact]
    public void Parse_KisayolBagiCozulur()
    {
        var result = Parse("OpenSearch", "Shortcut", "Ctrl+Alt+N");

        Assert.True(result.IsValid);
        Assert.Equal(InputAction.OpenSearch, result.Binding!.Action);
        Assert.Equal(new ShortcutTrigger(new Hotkey(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x4E)), result.Binding.Trigger);
    }

    [Fact]
    public void Parse_BilinmeyenEylemDusurulur()
    {
        var result = Parse("ShowMoonbase", "Shortcut", "Ctrl+Alt+N");

        Assert.False(result.IsValid);
        Assert.Equal(InputBindingDropReason.UnknownAction, result.DropReason);
    }

    [Fact]
    public void Parse_BilinmeyenTetikleyiciTuruDusurulur()
    {
        var result = Parse("ShowDesktop", "Gesture", "Wheel");

        Assert.False(result.IsValid);
        Assert.Equal(InputBindingDropReason.UnknownKind, result.DropReason);
    }

    [Fact]
    public void Parse_SayisalMetinAdKabulEdilmez()
    {
        Assert.Equal(InputBindingDropReason.UnknownAction, Parse("3", "Shortcut", "Ctrl+Alt+N").DropReason);
    }

    [Fact]
    public void Parse_AdBuyukKucukHarfDuyarsiz()
    {
        Assert.True(Parse("showdesktop", "SHORTCUT", "Win+D").IsValid);
    }

    [Fact]
    public void Parse_BozukKisayolDusurulur()
    {
        var result = Parse("OpenSearch", "Shortcut", "Ctrl+Alt+Bilinmeyen");

        Assert.Equal(InputBindingDropReason.InvalidTrigger, result.DropReason);
    }

    [Fact]
    public void Parse_KoseVarsayilanlariKullanir()
    {
        var result = Parse("OpenNotch", "HotCorner", "TopLeft");

        var trigger = Assert.IsType<HotCornerTrigger>(result.Binding!.Trigger);
        Assert.Equal(ScreenCorner.TopLeft, trigger.Corner);
        Assert.Equal(InputBindingParser.DefaultDwellMs, trigger.DwellMs);
        Assert.Equal(InputBindingParser.DefaultPushBackPx, trigger.PushBackPx);
    }

    [Fact]
    public void Parse_KoseAralikDisiSureyiDusurur()
    {
        Assert.Equal(InputBindingDropReason.InvalidParameter, Parse("OpenNotch", "HotCorner", "TopLeft", dwell: -1).DropReason);
        Assert.Equal(InputBindingDropReason.InvalidParameter, Parse("OpenNotch", "HotCorner", "TopLeft", dwell: InputBindingParser.MaxDwellMs + 1).DropReason);
        Assert.Equal(InputBindingDropReason.InvalidParameter, Parse("OpenNotch", "HotCorner", "TopLeft", pushBack: InputBindingParser.MaxPushBackPx + 1).DropReason);
    }

    [Fact]
    public void Parse_FareBagiSurfaceVeJestIkiParcalidir()
    {
        var result = Parse("ToggleWifi", "MouseButton", "Dock.MiddleClick");

        Assert.Equal(new MouseTrigger(InputSurface.Dock, MouseGesture.MiddleClick), result.Binding!.Trigger);
    }

    [Fact]
    public void Parse_FareBagiTekParcaDusurulur()
    {
        Assert.Equal(InputBindingDropReason.InvalidTrigger, Parse("ToggleWifi", "MouseButton", "Wheel").DropReason);
        Assert.Equal(InputBindingDropReason.InvalidTrigger, Parse("ToggleWifi", "MouseButton", "Notch.Scroll").DropReason);
    }

    [Fact]
    public void Parse_OzelEylemKimligiZorunlu()
    {
        Assert.Equal(InputBindingDropReason.InvalidParameter, Parse("RunCustomAction", "Shortcut", "Ctrl+Alt+N").DropReason);
        Assert.Equal("build", Parse("RunCustomAction", "Shortcut", "Ctrl+Alt+N", customId: " build ").Binding!.CustomActionId);
    }

    [Fact]
    public void Parse_OzelKimlikBaskaEylemdeTutulmaz()
    {
        var result = Parse("OpenSearch", "Shortcut", "Ctrl+Alt+N", customId: "build");

        Assert.Null(result.Binding!.CustomActionId);
    }

    [Fact]
    public void Validate_AyniEylemAyniTetikleyiciDuplicate()
    {
        var kisayol = new ShortcutTrigger(new Hotkey(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x4E));
        var bindings = new[]
        {
            new InputBinding(InputAction.OpenSearch, kisayol),
            new InputBinding(InputAction.OpenSearch, kisayol)
        };

        var report = InputBindingValidator.Validate(bindings, new HashSet<string>());

        Assert.Single(report.Accepted);
        var issue = Assert.Single(report.Issues);
        Assert.Equal(InputIssueKind.Duplicate, issue.Kind);
        Assert.Equal(1, issue.Index);
        Assert.Equal(0, issue.ConflictsWithIndex);
    }

    [Fact]
    public void Validate_FarkliEylemAyniTetikleyiciConflict()
    {
        var kose = new HotCornerTrigger(ScreenCorner.TopRight, 300, 4);
        var bindings = new[]
        {
            new InputBinding(InputAction.ShowDesktop, kose),
            new InputBinding(InputAction.OpenSearch, kose with { DwellMs = 800 })
        };

        var report = InputBindingValidator.Validate(bindings, new HashSet<string>());

        Assert.Equal(InputAction.ShowDesktop, Assert.Single(report.Accepted).Action);
        Assert.Equal(InputIssueKind.Conflict, Assert.Single(report.Issues).Kind);
    }

    [Fact]
    public void Validate_BirEylemBirden_FazlaKisayolAlabilir()
    {
        var bindings = new[]
        {
            new InputBinding(InputAction.ShowDesktop, new ShortcutTrigger(new Hotkey(HotkeyModifiers.Win, 0x44))),
            new InputBinding(InputAction.ShowDesktop, new HotCornerTrigger(ScreenCorner.BottomLeft, 300, 4)),
            new InputBinding(InputAction.ShowDesktop, new MouseTrigger(InputSurface.Notch, MouseGesture.DoubleClick))
        };

        var report = InputBindingValidator.Validate(bindings, new HashSet<string>());

        Assert.Equal(3, report.Accepted.Count);
        Assert.Empty(report.Issues);
    }

    [Fact]
    public void Validate_SistemKisayoluReddedilir()
    {
        var bindings = new[]
        {
            new InputBinding(InputAction.OpenSearch, new ShortcutTrigger(new Hotkey(HotkeyModifiers.Win, 0x4C)))
        };

        var report = InputBindingValidator.Validate(bindings, new HashSet<string>());

        Assert.Empty(report.Accepted);
        Assert.Equal(InputIssueKind.ReservedBySystem, Assert.Single(report.Issues).Kind);
    }

    [Fact]
    public void Validate_BilinmeyenOzelEylemReddedilir()
    {
        var bindings = new[]
        {
            new InputBinding(InputAction.RunCustomAction, new MouseTrigger(InputSurface.Dock, MouseGesture.RightClick), "silinmis"),
            new InputBinding(InputAction.RunCustomAction, new MouseTrigger(InputSurface.Dock, MouseGesture.Wheel), "build")
        };

        var report = InputBindingValidator.Validate(bindings, new HashSet<string>(["build"]));

        Assert.Equal(bindings[1], Assert.Single(report.Accepted));
        Assert.Equal(InputIssueKind.UnknownCustomAction, Assert.Single(report.Issues).Kind);
    }

    [Fact]
    public void Validate_BosListeBosRapor()
    {
        var report = InputBindingValidator.Validate([], new HashSet<string>());

        Assert.Empty(report.Accepted);
        Assert.Empty(report.Issues);
    }
}
