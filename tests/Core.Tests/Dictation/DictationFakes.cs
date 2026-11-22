using Koy.Core.Dictation;

namespace Koy.Core.Tests.Dictation;

internal sealed class FakeRecorder : IAudioRecorder
{
    public RecordedAudio? Audio { get; set; }
    public bool FailStart { get; set; }
    public bool Discarded { get; private set; }
    public bool? Paused { get; private set; }
    public bool IsRecording { get; private set; }

    public event Action<double>? LevelChanged;

    public Task StartAsync()
    {
        if (FailStart)
            throw new InvalidOperationException("no microphone");
        IsRecording = true;
        Discarded = false;
        return Task.CompletedTask;
    }

    public Task<RecordedAudio?> StopAsync()
    {
        IsRecording = false;
        return Task.FromResult(Audio);
    }

    public Task SetPausedAsync(bool paused)
    {
        Paused = paused;
        return Task.CompletedTask;
    }

    public Task DiscardAsync()
    {
        IsRecording = false;
        Discarded = true;
        return Task.CompletedTask;
    }

    public void RaiseLevel(double level) => LevelChanged?.Invoke(level);
}

internal sealed class FakeSpeech : ISpeechToText
{
    public Queue<object> Replies { get; } = new();
    public List<TranscriptionRequest> Requests { get; } = [];

    public Task<TranscriptionResult> TranscribeAsync(byte[] wav, TranscriptionRequest request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        var reply = Replies.Count > 0 ? Replies.Dequeue() : new TranscriptionResult("merhaba dünya");
        if (reply is Exception ex)
            throw ex;
        return Task.FromResult((TranscriptionResult)reply);
    }
}

internal sealed class FakeCleaner : ITextCleaner
{
    public Exception? Throw { get; set; }
    public string? Reply { get; set; }
    public int Calls { get; private set; }
    public string SystemPrompt { get; private set; } = "";
    public string User { get; private set; } = "";

    public Task<string> CompleteAsync(string systemPrompt, string userMessage, CancellationToken cancellationToken)
    {
        Calls++;
        SystemPrompt = systemPrompt;
        User = userMessage;
        if (Throw is not null)
            throw Throw;
        return Task.FromResult(Reply ?? "Merhaba dünya.");
    }
}

internal sealed class FakePaste : IPasteService
{
    public List<(string Text, bool Restore)> Pasted { get; } = [];
    public List<string> Copied { get; } = [];
    public Exception? Throw { get; set; }

    public Task PasteAsync(string text, bool restoreClipboard, CancellationToken cancellationToken)
    {
        if (Throw is not null)
            throw Throw;
        Pasted.Add((text, restoreClipboard));
        return Task.CompletedTask;
    }

    public Task CopyAsync(string text, CancellationToken cancellationToken)
    {
        Copied.Add(text);
        return Task.CompletedTask;
    }
}

internal sealed class FakeForeground : IForegroundProgram
{
    public (string Program, string Title) Value { get; set; } = ("", "");

    public (string Program, string Title) Current() => Value;
}

internal sealed class FakeRecordings : IRecordingStore
{
    public Dictionary<string, byte[]> Files { get; } = [];
    private int _next;

    public string Save(byte[] wav)
    {
        var path = "rec-" + ++_next;
        Files[path] = wav;
        return path;
    }

    public byte[]? Load(string path) => Files.TryGetValue(path, out var wav) ? wav : null;

    public void Delete(string path) => Files.Remove(path);
}
