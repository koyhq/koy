using Koy.Core.Voice;

namespace Koy.Core.Tests;

public class VoiceCommandTests
{
    [Fact]
    public void VoiceCommandParser_ClaudePreset_ResolvesCorrectly()
    {
        var resolved = VoiceCommandParser.Resolve(VoiceHarnessType.Claude, "unit test yaz");
        Assert.Equal("claude", resolved.Executable);
        // Varsayılan salt okunur: izin bayrağı yok, dikte yanlış duyulsa da onaysız değişiklik olmaz.
        Assert.Equal(["-p", "--", "unit test yaz"], resolved.Arguments);
    }

    [Theory]
    [InlineData(VoiceClaudePermission.Auto, new[] { "-p", "--permission-mode", "auto", "--", "x" })]
    [InlineData(VoiceClaudePermission.AcceptEdits, new[] { "-p", "--permission-mode", "acceptEdits", "--", "x" })]
    [InlineData(VoiceClaudePermission.Default, new[] { "-p", "--", "x" })]
    public void VoiceCommandParser_ClaudePermission_AddsModeBeforeDoubleDash(VoiceClaudePermission mode, string[] expected)
    {
        var resolved = VoiceCommandParser.Resolve(VoiceHarnessType.Claude, "x", claudePermission: mode);
        Assert.Equal(expected, resolved.Arguments);
    }

    [Fact]
    public void VoiceCommandParser_ClaudeModel_AddsModelFlagBeforeDoubleDash()
    {
        var resolved = VoiceCommandParser.Resolve(VoiceHarnessType.Claude, "x", claudePermission: VoiceClaudePermission.Auto, model: "opus");
        Assert.Equal(["-p", "--model", "opus", "--permission-mode", "auto", "--", "x"], resolved.Arguments);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("default")]
    [InlineData("DEFAULT")]
    public void VoiceCommandParser_ClaudeModel_DefaultOrEmpty_AddsNoModelFlag(string? model)
    {
        var resolved = VoiceCommandParser.Resolve(VoiceHarnessType.Claude, "x", claudePermission: VoiceClaudePermission.Default, model: model);
        Assert.Equal(["-p", "--", "x"], resolved.Arguments);
    }

    [Theory]
    [InlineData("--dangerously-skip-permissions")]
    [InlineData("-x")]
    [InlineData("opus; calc")]
    [InlineData("a b")]
    public void VoiceCommandParser_ClaudeModel_InvalidName_Throws(string model)
    {
        var ex = Assert.Throws<VoiceCommandException>(() => VoiceCommandParser.Resolve(VoiceHarnessType.Claude, "x", model: model));
        // Kullanıcı metnini App seçer: Core yalnız kodu ve modelin adını taşır.
        Assert.Equal(VoiceFailure.InvalidModel, ex.Failure);
        Assert.Equal(model, ex.Detail);
    }

    [Fact]
    public void VoiceCommandParser_Model_DoesNotAffectOtherHarnesses()
    {
        Assert.Equal(["exec", "--", "x"], VoiceCommandParser.Resolve(VoiceHarnessType.Codex, "x", model: "opus").Arguments);
    }

    [Fact]
    public void VoiceCommandParser_ClaudePermission_DoesNotAffectOtherHarnesses()
    {
        Assert.Equal(["exec", "--", "x"], VoiceCommandParser.Resolve(VoiceHarnessType.Codex, "x", claudePermission: VoiceClaudePermission.Auto).Arguments);
        Assert.Equal(["-p", "x"], VoiceCommandParser.Resolve(VoiceHarnessType.Agy, "x", claudePermission: VoiceClaudePermission.Auto).Arguments);
    }

    [Fact]
    public void VoiceCommandParser_CodexPreset_ResolvesCorrectly()
    {
        var resolved = VoiceCommandParser.Resolve(VoiceHarnessType.Codex, "refactor et");
        Assert.Equal("codex", resolved.Executable);
        Assert.Equal(["exec", "--", "refactor et"], resolved.Arguments);
    }

    [Fact]
    public void VoiceCommandParser_AgyPreset_UsesPrintFlagWithPromptAsValue()
    {
        var resolved = VoiceCommandParser.Resolve(VoiceHarnessType.Agy, "durumu raporla");
        Assert.Equal("agy", resolved.Executable);
        Assert.Equal(["-p", "durumu raporla"], resolved.Arguments);
    }

    [Fact]
    public void VoiceCommandParser_DashPrompt_StaysAPromptForEveryPreset()
    {
        // Claude ve Codex "--" ile, agy ise -p değeri olarak istemi seçenek sanmaz.
        Assert.Equal(["-p", "--", "--version"], VoiceCommandParser.Resolve(VoiceHarnessType.Claude, "--version").Arguments);
        Assert.Equal(["exec", "--", "--version"], VoiceCommandParser.Resolve(VoiceHarnessType.Codex, "--version").Arguments);
        Assert.Equal(["-p", "--version"], VoiceCommandParser.Resolve(VoiceHarnessType.Agy, "--version").Arguments);
    }

    [Fact]
    public void VoiceCommandParser_CustomWithPlaceholder_ReplacesPlaceholder()
    {
        var resolved = VoiceCommandParser.Resolve(
            VoiceHarnessType.Custom,
            "hızlı özet",
            customExecutable: "my-tool.exe",
            customArgumentsTemplate: "--prompt \"{prompt}\" --verbose");

        Assert.Equal("my-tool.exe", resolved.Executable);
        Assert.Equal(["--prompt", "hızlı özet", "--verbose"], resolved.Arguments);
    }

    [Fact]
    public void VoiceCommandParser_CustomWithoutPlaceholder_AppendsPromptToEnd()
    {
        var resolved = VoiceCommandParser.Resolve(
            VoiceHarnessType.Custom,
            "testleri çalıştır",
            customExecutable: "runner",
            customArgumentsTemplate: "run --mode quick");

        Assert.Equal("runner", resolved.Executable);
        Assert.Equal(["run", "--mode", "quick", "testleri çalıştır"], resolved.Arguments);
    }

    [Theory]
    [InlineData("cmd.exe")]
    [InlineData("cmd")]
    [InlineData("powershell.exe")]
    [InlineData("pwsh")]
    [InlineData("bash")]
    [InlineData("C:\\Windows\\System32\\cmd.exe")]
    public void VoiceCommandParser_BlocksShellExecutables(string shellExe)
    {
        Assert.Throws<VoiceCommandException>(() =>
            VoiceCommandParser.Resolve(VoiceHarnessType.Custom, "test", customExecutable: shellExe));
    }

    [Theory]
    [InlineData("tool & del /f *")]
    [InlineData("tool | calc")]
    [InlineData("tool > out.txt")]
    [InlineData("tool; malicious")]
    public void VoiceCommandParser_BlocksMetacharactersInExecutable(string dangerousExe)
    {
        Assert.Throws<VoiceCommandException>(() =>
            VoiceCommandParser.Resolve(VoiceHarnessType.Custom, "test", customExecutable: dangerousExe));
    }

    [Fact]
    public void VoiceCommandParser_DangerousPromptInjection_TreatedAsSingleLiteralArgument()
    {
        // Komut enjeksiyonu denemesi: tırnaklar, borular, ampersand'lar
        const string maliciousPrompt = "hello & rm -rf / ; dir | calc.exe && echo \"pwned\"";
        
        var resolved = VoiceCommandParser.Resolve(VoiceHarnessType.Claude, maliciousPrompt);
        
        Assert.Equal("claude", resolved.Executable);
        Assert.Equal(3, resolved.Arguments.Count);
        Assert.Equal("-p", resolved.Arguments[0]);
        Assert.Equal("--", resolved.Arguments[1]);
        // Argüman parçalanmadan tam olarak tek bir öğe olarak kalmalı
        Assert.Equal(maliciousPrompt, resolved.Arguments[2]);
    }

    [Fact]
    public void VoiceCommandParser_SingleCustomCommandLine_ParsesExeAndArgumentsWithPlaceholder()
    {
        var resolved = VoiceCommandParser.Resolve(
            VoiceHarnessType.Custom,
            "kod yaz",
            customExecutable: "gh copilot suggest \"{prompt}\"");

        Assert.Equal("gh", resolved.Executable);
        Assert.Equal(["copilot", "suggest", "kod yaz"], resolved.Arguments);
    }

    [Fact]
    public void VoiceCommandParser_SingleCustomCommandLine_WithQuotesInExe_ParsesCorrectly()
    {
        var resolved = VoiceCommandParser.Resolve(
            VoiceHarnessType.Custom,
            "soru sor",
            customExecutable: "\"C:\\My Tools\\custom.exe\" --ask \"{prompt}\"");

        Assert.Equal(@"C:\My Tools\custom.exe", resolved.Executable);
        Assert.Equal(["--ask", "soru sor"], resolved.Arguments);
    }

    [Fact]
    public void VoiceCommandParser_SingleCustomCommandLine_WithoutPlaceholder_AppendsPrompt()
    {
        var resolved = VoiceCommandParser.Resolve(
            VoiceHarnessType.Custom,
            "fix bug",
            customExecutable: "git commit -m");

        Assert.Equal("git", resolved.Executable);
        Assert.Equal(["commit", "-m", "fix bug"], resolved.Arguments);
    }

    private sealed class FakeHarnessRunnerService : IHarnessRunnerService
    {
        public HarnessExecutionResult ResultToReturn { get; set; } =
            new(Success: true, ExitCode: 0, Output: "OK", Error: "", Duration: TimeSpan.FromSeconds(1));

        public HarnessExecutionRequest? LastRequest { get; private set; }
        public int CallCount { get; private set; }

        public Task<HarnessExecutionResult> ExecuteAsync(HarnessExecutionRequest request, CancellationToken ct)
        {
            CallCount++;
            LastRequest = request;
            if (ct.IsCancellationRequested)
                return Task.FromCanceled<HarnessExecutionResult>(ct);

            return Task.FromResult(ResultToReturn);
        }
    }

    private sealed class PendingRunner : IHarnessRunnerService
    {
        public TaskCompletionSource<HarnessExecutionResult> Pending { get; } = new();

        // İptal belirtecini bilerek yok sayar: süreç iptalden sonra yine de sonuç döndürebilir.
        public Task<HarnessExecutionResult> ExecuteAsync(HarnessExecutionRequest request, CancellationToken ct) => Pending.Task;
    }

    private sealed class ThrowingRunner(Exception ex) : IHarnessRunnerService
    {
        public Task<HarnessExecutionResult> ExecuteAsync(HarnessExecutionRequest request, CancellationToken ct) =>
            Task.FromException<HarnessExecutionResult>(ex);
    }

    private static VoiceSessionController Controller(IHarnessRunnerService runner, VoiceSessionOptions? options = null) =>
        new(runner, () => options ?? new VoiceSessionOptions());

    [Fact]
    public async Task Shortcut_opens_box_then_runs_typed_prompt()
    {
        var runner = new FakeHarnessRunnerService();
        using var controller = Controller(runner, new VoiceSessionOptions(HarnessType: VoiceHarnessType.Claude));

        await controller.TriggerShortcutAsync();
        Assert.Equal(VoiceState.Editing, controller.State);

        // Windows sesle yazma kutuya parça parça yazar.
        controller.UpdatePrompt("testleri");
        controller.UpdatePrompt("testleri çalıştır ");

        await controller.TriggerShortcutAsync();

        Assert.Equal(VoiceState.Completed, controller.State);
        Assert.Equal("testleri çalıştır", runner.LastRequest?.Prompt);
        Assert.Equal(VoiceHarnessType.Claude, runner.LastRequest?.HarnessType);
    }

    [Fact]
    public async Task Enter_on_empty_box_does_nothing_and_keeps_box_open()
    {
        var runner = new FakeHarnessRunnerService();
        using var controller = Controller(runner);

        controller.Start();
        controller.UpdatePrompt("   ");
        await controller.ConfirmAsync();

        Assert.Equal(VoiceState.Editing, controller.State);
        Assert.Equal(0, runner.CallCount);
    }

    [Fact]
    public async Task Shortcut_on_empty_box_closes_it()
    {
        using var controller = Controller(new FakeHarnessRunnerService());

        controller.Start();
        await controller.TriggerShortcutAsync();

        Assert.Equal(VoiceState.Idle, controller.State);
    }

    [Fact]
    public void Text_outside_the_box_is_ignored()
    {
        using var controller = Controller(new FakeHarnessRunnerService());

        controller.UpdatePrompt("geç gelen yazı");

        Assert.Equal(VoiceState.Idle, controller.State);
        Assert.Equal("", controller.Prompt);
    }

    [Fact]
    public async Task Reopening_box_clears_previous_prompt_and_result()
    {
        using var controller = Controller(new FakeHarnessRunnerService());

        controller.Start();
        controller.UpdatePrompt("ilk komut");
        await controller.ConfirmAsync();
        Assert.Equal(VoiceState.Completed, controller.State);

        await controller.TriggerShortcutAsync();

        Assert.Equal(VoiceState.Editing, controller.State);
        Assert.Equal("", controller.Prompt);
        Assert.Null(controller.LastResult);
    }

    [Fact]
    public async Task Runner_failure_sets_failed_with_exit_code()
    {
        var runner = new FakeHarnessRunnerService
        {
            ResultToReturn = new(Success: false, ExitCode: 1, Output: "", Error: "Command failed", Duration: TimeSpan.FromSeconds(1))
        };
        using var controller = Controller(runner);

        controller.Start();
        controller.UpdatePrompt("hatalı komut");
        await controller.ConfirmAsync();

        Assert.Equal(VoiceState.Failed, controller.State);
        Assert.Equal(VoiceFailure.ExitCode, controller.Failure);
        Assert.Equal("Command failed", controller.LastResult?.Error);
    }

    [Fact]
    public async Task Runner_failure_code_and_detail_are_passed_on()
    {
        var runner = new FakeHarnessRunnerService
        {
            ResultToReturn = new(Success: false, ExitCode: -1, Output: "", Error: "", Duration: TimeSpan.Zero,
                Failure: VoiceFailure.CommandNotFound, Detail: "agy")
        };
        using var controller = Controller(runner);

        controller.Start();
        controller.UpdatePrompt("durum");
        await controller.ConfirmAsync();

        Assert.Equal(VoiceFailure.CommandNotFound, controller.Failure);
        Assert.Equal("agy", controller.FailureDetail);
    }

    [Fact]
    public async Task Invalid_custom_command_reports_invalid_command()
    {
        using var controller = Controller(new ThrowingRunner(new VoiceCommandException("boş")));

        controller.Start();
        controller.UpdatePrompt("bir şey yap");
        await controller.ConfirmAsync();

        Assert.Equal(VoiceState.Failed, controller.State);
        Assert.Equal(VoiceFailure.InvalidCommand, controller.Failure);
    }

    [Fact]
    public async Task Result_arriving_after_cancel_does_not_override_idle()
    {
        var runner = new PendingRunner();
        using var controller = Controller(runner);

        controller.Start();
        controller.UpdatePrompt("uzun iş");
        var running = controller.ConfirmAsync();
        Assert.Equal(VoiceState.Running, controller.State);

        await controller.TriggerShortcutAsync();
        Assert.Equal(VoiceState.Idle, controller.State);

        runner.Pending.SetResult(new(Success: true, ExitCode: 0, Output: "", Error: "", Duration: TimeSpan.Zero));
        await running;

        Assert.Equal(VoiceState.Idle, controller.State);
        Assert.Null(controller.LastResult);
    }

    [Fact]
    public void Cancel_closes_box_and_clears_prompt()
    {
        using var controller = Controller(new FakeHarnessRunnerService());

        controller.Start();
        controller.UpdatePrompt("vazgeçtim");
        controller.Cancel();

        Assert.Equal(VoiceState.Idle, controller.State);
        Assert.Equal("", controller.Prompt);
    }
}
