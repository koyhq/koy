using Koy.Core.Voice;

namespace Koy.Core.Tests;

public class VoiceCommandTests
{
    [Fact]
    public void VoiceCommandParser_ClaudePreset_ResolvesCorrectly()
    {
        var resolved = VoiceCommandParser.Resolve(VoiceHarnessType.Claude, "unit test yaz");
        Assert.Equal("claude", resolved.Executable);
        Assert.Equal(["-p", "--", "unit test yaz"], resolved.Arguments);
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

    private sealed class FakeVoiceRecognitionService : IVoiceRecognitionService
    {
        public bool IsListening { get; private set; }
        public bool StartResult { get; set; } = true;
        public int StartCallCount { get; private set; }
        public int StopCallCount { get; private set; }

        public event EventHandler<string>? HypothesisReceived;
        public event EventHandler<string>? SpeechRecognized;
        public event EventHandler<VoiceRecognitionError>? RecognitionFailed;

        public Action? StartHook { get; set; }

        public Task<bool> StartListeningAsync()
        {
            StartCallCount++;
            StartHook?.Invoke();
            IsListening = StartResult;
            return Task.FromResult(StartResult);
        }

        public Task StopListeningAsync()
        {
            StopCallCount++;
            IsListening = false;
            return Task.CompletedTask;
        }

        public void EmitHypothesis(string text) => HypothesisReceived?.Invoke(this, text);
        public void EmitRecognized(string text) => SpeechRecognized?.Invoke(this, text);
        public void EmitFailed(VoiceRecognitionError error) => RecognitionFailed?.Invoke(this, error);

        public void Dispose() { }
    }

    private sealed class FakeHarnessRunnerService : IHarnessRunnerService
    {
        public HarnessExecutionResult ResultToReturn { get; set; } =
            new(Success: true, ExitCode: 0, Output: "OK", Error: "", Duration: TimeSpan.FromSeconds(1));

        public HarnessExecutionRequest? LastRequest { get; private set; }

        public Task<HarnessExecutionResult> ExecuteAsync(HarnessExecutionRequest request, CancellationToken ct)
        {
            LastRequest = request;
            if (ct.IsCancellationRequested)
                return Task.FromCanceled<HarnessExecutionResult>(ct);

            return Task.FromResult(ResultToReturn);
        }
    }

    [Fact]
    public async Task VoiceSessionController_FullLifecycle_ManualConfirmation()
    {
        var fakeRec = new FakeVoiceRecognitionService();
        var fakeRunner = new FakeHarnessRunnerService();
        var options = new VoiceSessionOptions(
            HarnessType: VoiceHarnessType.Claude,
            AutoConfirm: false);

        using var controller = new VoiceSessionController(fakeRec, fakeRunner, () => options);

        Assert.Equal(VoiceState.Idle, controller.State);

        // 1. Kısayol: Dinlemeye başla
        await controller.TriggerShortcutAsync();
        Assert.Equal(VoiceState.Listening, controller.State);
        Assert.Equal(1, fakeRec.StartCallCount);

        // Ara hipotezler
        fakeRec.EmitHypothesis("test");
        Assert.Equal("test", controller.CurrentHypothesis);

        // 2. Kısayol: Konuşma bitti (erken durdurma)
        await controller.TriggerShortcutAsync();
        Assert.Equal(VoiceState.Transcribing, controller.State);
        Assert.Equal(1, fakeRec.StopCallCount);

        // Tanıma tamamlandı
        fakeRec.EmitRecognized("testleri çalıştır");
        Assert.Equal(VoiceState.AwaitingConfirmation, controller.State);
        Assert.Equal("testleri çalıştır", controller.RecognizedPrompt);

        // Kullanıcı onayladı
        await controller.ConfirmAsync();
        Assert.Equal(VoiceState.Completed, controller.State);
        Assert.NotNull(controller.LastResult);
        Assert.True(controller.LastResult.Success);
        Assert.Equal("claude", fakeRunner.LastRequest?.HarnessType.ToString().ToLowerInvariant());
    }

    [Fact]
    public async Task VoiceSessionController_AutoConfirm_ExecutesDirectly()
    {
        var fakeRec = new FakeVoiceRecognitionService();
        var fakeRunner = new FakeHarnessRunnerService();
        var options = new VoiceSessionOptions(
            HarnessType: VoiceHarnessType.Codex,
            AutoConfirm: true);

        using var controller = new VoiceSessionController(fakeRec, fakeRunner, () => options);

        await controller.StartListeningAsync();
        Assert.Equal(VoiceState.Listening, controller.State);

        // Konuşma algılandığı anda otomatik olarak çalıştırmaya geçmeli
        fakeRec.EmitRecognized("commit mesajı oluştur");

        // Görevin bitmesini bekle
        Assert.Equal(VoiceState.Completed, controller.State);
        Assert.Equal("commit mesajı oluştur", fakeRunner.LastRequest?.Prompt);
    }

    [Fact]
    public async Task VoiceSessionController_RunnerFailure_SetsFailedState()
    {
        var fakeRec = new FakeVoiceRecognitionService();
        var fakeRunner = new FakeHarnessRunnerService
        {
            ResultToReturn = new(Success: false, ExitCode: 1, Output: "", Error: "Command failed", Duration: TimeSpan.FromSeconds(1))
        };

        var options = new VoiceSessionOptions(AutoConfirm: false);
        using var controller = new VoiceSessionController(fakeRec, fakeRunner, () => options);

        await controller.StartListeningAsync();
        fakeRec.EmitRecognized("hatalı komut");
        await controller.ConfirmAsync();

        Assert.Equal(VoiceState.Failed, controller.State);
        Assert.False(controller.LastResult?.Success);
        Assert.Equal(VoiceFailure.ExitCode, controller.Failure);
        Assert.Equal("Command failed", controller.LastResult?.Error);
    }

    [Fact]
    public async Task VoiceSessionController_Cancel_ResetsToIdle()
    {
        var fakeRec = new FakeVoiceRecognitionService();
        var fakeRunner = new FakeHarnessRunnerService();
        var options = new VoiceSessionOptions();
        using var controller = new VoiceSessionController(fakeRec, fakeRunner, () => options);

        await controller.StartListeningAsync();
        Assert.Equal(VoiceState.Listening, controller.State);

        controller.Cancel();
        Assert.Equal(VoiceState.Idle, controller.State);
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

    [Fact]
    public async Task VoiceSessionController_TriggerShortcut_WhileAwaitingConfirmation_ConfirmsExecution()
    {
        var fakeRec = new FakeVoiceRecognitionService();
        var fakeRunner = new FakeHarnessRunnerService();
        var options = new VoiceSessionOptions(AutoConfirm: false);
        using var controller = new VoiceSessionController(fakeRec, fakeRunner, () => options);

        await controller.StartListeningAsync();
        fakeRec.EmitRecognized("testi onayla");
        Assert.Equal(VoiceState.AwaitingConfirmation, controller.State);

        // Kısayola tekrar basılınca onaylayıp çalıştırmalı
        await controller.TriggerShortcutAsync();
        Assert.Equal(VoiceState.Completed, controller.State);
        Assert.NotNull(fakeRunner.LastRequest);
        Assert.Equal("testi onayla", fakeRunner.LastRequest.Prompt);
    }

    [Fact]
    public async Task VoiceSessionController_TriggerShortcut_WhileTranscribing_CancelsSession()
    {
        var fakeRec = new FakeVoiceRecognitionService();
        var fakeRunner = new FakeHarnessRunnerService();
        var options = new VoiceSessionOptions();
        using var controller = new VoiceSessionController(fakeRec, fakeRunner, () => options);

        await controller.StartListeningAsync();
        await controller.StopListeningAsync();
        Assert.Equal(VoiceState.Transcribing, controller.State);

        // Çevrilirken kısayola basılırsa iptal edilip boşa dönmeli
        await controller.TriggerShortcutAsync();
        Assert.Equal(VoiceState.Idle, controller.State);
    }

    [Fact]
    public async Task VoiceSessionController_SpeechRecognized_ImmediatelyStopsRecognition()
    {
        var fakeRec = new FakeVoiceRecognitionService();
        var fakeRunner = new FakeHarnessRunnerService();
        var options = new VoiceSessionOptions(AutoConfirm: false);
        using var controller = new VoiceSessionController(fakeRec, fakeRunner, () => options);

        await controller.StartListeningAsync();
        Assert.Equal(0, fakeRec.StopCallCount);

        // Konuşma algılandığı anda mikrofon dinlemesi durdurulmalı
        fakeRec.EmitRecognized("mikrofonu kapat");
        Assert.Equal(1, fakeRec.StopCallCount);
        Assert.Equal(VoiceState.AwaitingConfirmation, controller.State);
    }

    [Fact]
    public async Task VoiceSessionController_EmptySpeechRecognized_TransitionsToIdleWithoutError()
    {
        var fakeRec = new FakeVoiceRecognitionService();
        var fakeRunner = new FakeHarnessRunnerService();
        var options = new VoiceSessionOptions();
        using var controller = new VoiceSessionController(fakeRec, fakeRunner, () => options);

        await controller.StartListeningAsync();
        await controller.StopListeningAsync();
        Assert.Equal(VoiceState.Transcribing, controller.State);

        // Boş konuşma sonucu geldiğinde Idle'a dönmeli
        fakeRec.EmitRecognized("");
        Assert.Equal(VoiceState.Idle, controller.State);
        Assert.Equal(VoiceFailure.NoSpeech, controller.Failure);
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

    [Fact]
    public async Task VoiceSessionController_ResultArrivingAfterCancel_DoesNotOverrideIdle()
    {
        var fakeRec = new FakeVoiceRecognitionService();
        var runner = new PendingRunner();
        using var controller = new VoiceSessionController(fakeRec, runner, () => new VoiceSessionOptions());

        await controller.StartListeningAsync();
        fakeRec.EmitRecognized("uzun iş");
        var running = controller.ConfirmAsync();
        Assert.Equal(VoiceState.Running, controller.State);

        controller.Cancel();
        runner.Pending.SetResult(new(Success: true, ExitCode: 0, Output: "", Error: "", Duration: TimeSpan.Zero));
        await running;

        Assert.Equal(VoiceState.Idle, controller.State);
        Assert.Null(controller.LastResult);
    }

    [Fact]
    public async Task VoiceSessionController_StartFailsAfterServiceReportedReason_KeepsThatReason()
    {
        var fakeRec = new FakeVoiceRecognitionService { StartResult = false };
        using var controller = new VoiceSessionController(fakeRec, new FakeHarnessRunnerService(), () => new VoiceSessionOptions());

        // Gerçek servis izin hatasını olayla bildirir, sonra false döner.
        fakeRec.StartHook = () => fakeRec.EmitFailed(new VoiceRecognitionError(VoiceFailure.MicrophonePermission));
        await controller.StartListeningAsync();

        Assert.Equal(VoiceState.Failed, controller.State);
        Assert.Equal(VoiceFailure.MicrophonePermission, controller.Failure);
    }

    [Fact]
    public async Task VoiceSessionController_InvalidCustomCommand_ReportsInvalidCommand()
    {
        var fakeRec = new FakeVoiceRecognitionService();
        var runner = new ThrowingRunner(new VoiceCommandException("boş"));
        using var controller = new VoiceSessionController(fakeRec, runner, () => new VoiceSessionOptions());

        await controller.StartListeningAsync();
        fakeRec.EmitRecognized("bir şey yap");
        await controller.ConfirmAsync();

        Assert.Equal(VoiceState.Failed, controller.State);
        Assert.Equal(VoiceFailure.InvalidCommand, controller.Failure);
    }
}
