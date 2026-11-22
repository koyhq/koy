using Koy.Core.Dictation;

namespace Koy.Core.Tests.Dictation;

public class DictationTextTests
{
    [Fact]
    public void The_hint_lists_typed_terms_before_learned_ones_without_duplicates()
    {
        List<DictationTerm> entries =
        [
            new() { Term = "Grafana", Auto = true },
            new() { Term = "Kubernetes" },
            new() { Term = "kubernetes" },
            new() { Term = "  " },
        ];

        Assert.Equal("Kubernetes, Grafana", DictionaryText.Hint(entries));
    }

    [Fact]
    public void The_hint_stops_before_it_gets_too_long()
    {
        var entries = Enumerable.Range(0, 200).Select(i => new DictationTerm { Term = "Terim" + i }).ToList();

        Assert.True(DictionaryText.Hint(entries).Length <= DictionaryText.MaxHintLength);
    }

    [Fact]
    public void The_glossary_shows_misheard_forms_as_arrows()
    {
        List<DictationTerm> entries = [new() { Term = "PyQt", Heard = "pay kut" }, new() { Term = "Koy" }];

        Assert.Equal("pay kut -> PyQt\nKoy", DictionaryText.Glossary(entries));
    }

    [Fact]
    public void A_term_is_found_regardless_of_case_accents_and_turkish_i()
    {
        List<DictationTerm> entries = [new() { Term = "Işık" }];

        Assert.True(DictionaryText.Contains(entries, "ISIK"));
        Assert.False(DictionaryText.Contains(entries, "Ses"));
        Assert.False(DictionaryText.Contains(entries, " "));
    }

    [Fact]
    public void Names_are_candidates_but_sentence_starts_and_plain_words_are_not()
    {
        var found = DictionaryLearner.Candidates("Today I met Alice and Bob at Node.js meetup. Maria wrote it down.");

        Assert.Equal(new[] { "Alice", "Bob", "Node.js" }, found);
    }

    [Fact]
    public void A_name_is_learned_only_after_it_has_been_seen_enough_times()
    {
        var file = new DictionaryFile();

        for (var i = 1; i < DictionaryLearner.Threshold; i++)
        {
            Assert.Empty(DictionaryLearner.Learn(file, "Bugün Grafana açtım."));
            Assert.Empty(file.Entries);
        }

        var added = DictionaryLearner.Learn(file, "Bugün Grafana açtım.");

        Assert.Equal(new[] { "Grafana" }, added);
        var entry = Assert.Single(file.Entries);
        Assert.True(entry.Auto);
        Assert.Empty(file.Pending);
    }

    [Fact]
    public void A_known_term_is_not_counted_again()
    {
        var file = new DictionaryFile { Entries = [new DictationTerm { Term = "Grafana" }] };

        Assert.Empty(DictionaryLearner.Learn(file, "Bugün Grafana açtım."));
        Assert.Empty(file.Pending);
    }

    [Fact]
    public void Pending_names_are_capped()
    {
        var file = new DictionaryFile();
        for (var i = 0; i < DictionaryLearner.MaxPending + 20; i++)
            file.Pending["aday" + i] = 1;

        DictionaryLearner.Learn(file, "Bugün Grafana açtım.");

        Assert.True(file.Pending.Count <= DictionaryLearner.MaxPending);
    }
}

public class ProfileSelectorTests
{
    private static DictationConfig Config() => new()
    {
        DefaultProfile = new WritingProfile { Name = "Default" },
        Profiles =
        [
            new WritingProfile { Id = "work", Name = "Work", Programs = ["slack", "teams"] },
            new WritingProfile { Id = "mail", Name = "Mail", Sites = ["Gmail"] },
        ],
    };

    [Theory]
    [InlineData("Code.EXE", "code")]
    [InlineData("C:\\Apps\\Slack.exe", "slack")]
    [InlineData("  chrome ", "chrome")]
    [InlineData(null, "")]
    public void Program_names_are_reduced_to_a_key(string? input, string expected)
    {
        Assert.Equal(expected, ProfileSelector.ProgramKey(input));
    }

    [Fact]
    public void The_program_in_front_picks_its_profile()
    {
        var choice = ProfileSelector.Choose(Config(), "Slack.exe", "general");

        Assert.Equal("Work", choice.Profile.Name);
        Assert.False(choice.IsDefault);
    }

    [Fact]
    public void A_browser_tab_is_matched_by_a_whole_word_in_its_title()
    {
        Assert.Equal("Mail", ProfileSelector.Choose(Config(), "chrome", "Inbox (3) - Gmail - Google Chrome").Profile.Name);
        Assert.True(ProfileSelector.Choose(Config(), "chrome", "Gmailer tips").IsDefault);
        Assert.True(ProfileSelector.Choose(Config(), "notepad", "Gmail").IsDefault);
    }

    [Fact]
    public void A_manual_choice_beats_everything()
    {
        var config = Config();
        config.ProfileOverride = "mail";

        Assert.Equal("Mail", ProfileSelector.Choose(config, "slack", "").Profile.Name);
    }

    [Fact]
    public void An_unknown_program_falls_back_to_the_default_profile()
    {
        var choice = ProfileSelector.Choose(Config(), "", "");

        Assert.True(choice.IsDefault);
        Assert.Equal("Default", choice.Profile.Name);
    }

    [Fact]
    public void By_app_selection_can_be_switched_off()
    {
        var config = Config();
        config.ProfileByApp = false;

        Assert.True(ProfileSelector.Choose(config, "slack", "").IsDefault);
    }

    [Fact]
    public void By_name_finds_ids_then_names()
    {
        var config = Config();

        Assert.Equal("Work", ProfileSelector.ByName(config, "work")!.Name);
        Assert.Equal("Mail", ProfileSelector.ByName(config, "MAIL")!.Name);
        Assert.Null(ProfileSelector.ByName(config, "nope"));
        Assert.Null(ProfileSelector.ByName(config, ""));
    }
}

public class CleanupPromptTests
{
    [Fact]
    public void A_turkish_transcript_gets_the_turkish_prompt()
    {
        var prompt = CleanupPrompts.Build(new DictationConfig(), "tr", null, false, "");

        Assert.Contains("konuşma-metin", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Any_other_language_gets_the_language_agnostic_prompt()
    {
        var prompt = CleanupPrompts.Build(new DictationConfig(), "de", null, false, "");

        Assert.Contains("speech-to-text", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void A_custom_prompt_replaces_the_built_in_one()
    {
        var prompt = CleanupPrompts.Build(new DictationConfig { CleanupPrompt = "Kendi istemim." }, "tr", null, false, "");

        Assert.StartsWith("Kendi istemim.", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("konuşma-metin", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Style_glossary_and_translation_are_appended()
    {
        var profile = new WritingProfile { TranslateTo = "English", Style = new ProfileStyle { Formality = "formal", Custom = "Hitap: Sayın." } };

        var prompt = CleanupPrompts.Build(new DictationConfig(), "tr", profile, true, "pay kut -> PyQt");

        Assert.Contains("formal, polite", prompt, StringComparison.Ordinal);
        Assert.Contains("Hitap: Sayın.", prompt, StringComparison.Ordinal);
        Assert.Contains("translate the result into English", prompt, StringComparison.Ordinal);
        Assert.Contains("pay kut -> PyQt", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Translating_without_cleanup_asks_for_a_translation_only()
    {
        var profile = new WritingProfile { TranslateTo = "English" };

        var prompt = CleanupPrompts.Build(new DictationConfig { CleanupEnabled = false }, "tr", profile, true, "");

        Assert.Contains("Translate it into English", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Remove filler words", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Unknown_style_values_are_ignored()
    {
        Assert.Equal("", CleanupPrompts.StyleSection(new ProfileStyle { Formality = "rude", Brevity = "??" }));
        Assert.Equal("", CleanupPrompts.StyleSection(null));
    }

    [Fact]
    public void The_fence_uses_a_marker_the_text_cannot_contain()
    {
        var markers = new Queue<string>(["AAA", "BBB"]);

        var fenced = CleanupPrompts.Fence("içinde AAA geçiyor", markers.Dequeue);

        Assert.Contains("BBB\niçinde AAA geçiyor\nBBB", fenced, StringComparison.Ordinal);
    }
}

public class DictationHistoryTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "koy-dictation-history-" + Guid.NewGuid().ToString("N"));
    private readonly DictationHistory _history;

    public DictationHistoryTests() => _history = new DictationHistory(Path.Combine(_dir, "h.json"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    private static DictationHistoryEntry Row(string id, string? recording = null) =>
        new(id, DateTimeOffset.UnixEpoch, "ham", "metin", "tr", "model", "Default", 1.5, DictationHistory.StatusOk, "", recording);

    [Fact]
    public void Rows_are_added_oldest_first_and_trimmed_past_the_limit()
    {
        _history.Add(Row("1", "a.wav"), 2);
        _history.Add(Row("2"), 2);

        var dropped = _history.Add(Row("3"), 2);

        Assert.Equal(new[] { "2", "3" }, _history.Load().Select(r => r.Id));
        Assert.Equal("1", Assert.Single(dropped).Id);
    }

    [Fact]
    public void A_row_with_the_same_id_is_replaced()
    {
        _history.Add(Row("1"), 10);

        _history.Add(Row("1") with { Text = "yeni" }, 10);

        Assert.Equal("yeni", Assert.Single(_history.Load()).Text);
    }

    [Fact]
    public void A_zero_limit_keeps_no_history()
    {
        var dropped = _history.Add(Row("1"), 0);

        Assert.Empty(_history.Load());
        Assert.Single(dropped);
    }

    [Fact]
    public void Rows_can_be_removed_by_id_and_cleared()
    {
        _history.Add(Row("1"), 10);
        _history.Add(Row("2"), 10);

        Assert.Equal("1", Assert.Single(_history.Remove(["1", "nope"])).Id);
        Assert.Equal("2", Assert.Single(_history.Clear()).Id);
        Assert.Empty(_history.Load());
    }

    [Fact]
    public void A_missing_or_broken_file_reads_as_empty()
    {
        Assert.Empty(_history.Load());

        Directory.CreateDirectory(_dir);
        File.WriteAllText(_history.Path, "{ not json");

        Assert.Empty(_history.Load());
    }
}

public class DictationConfigStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "koy-dictation-config-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void A_missing_file_gives_the_defaults_with_dictation_off()
    {
        var config = new DictationConfigStore(Path.Combine(_dir, "d.json")).Load();

        Assert.False(config.Enabled);
        Assert.Equal("auto", config.Language);
        Assert.True(config.CleanupEnabled);
    }

    [Fact]
    public void Settings_profiles_and_dictionary_round_trip()
    {
        var store = new DictationConfigStore(Path.Combine(_dir, "d.json"));
        var config = new DictationConfig
        {
            Enabled = true,
            Language = "tr",
            BackupModel = "b",
            Profiles = [new WritingProfile { Id = "p", Name = "Work", Programs = ["slack"], Style = new ProfileStyle { Formality = "formal" } }],
        };

        Assert.True(store.Save(config));
        var loaded = store.Load();

        Assert.True(loaded.Enabled);
        Assert.Equal("tr", loaded.Language);
        var profile = Assert.Single(loaded.Profiles);
        Assert.Equal("formal", profile.Style.Formality);
        Assert.Equal(new[] { "slack" }, profile.Programs);
    }

    [Fact]
    public void Out_of_range_values_are_pulled_back()
    {
        var config = DictationConfigStore.Normalize(new DictationConfig
        {
            BaseUrl = " ",
            MaxSeconds = 1,
            SilenceDb = 5,
            SpeechMarginDb = 0,
            HistoryLimit = -3,
            Language = "",
        });

        Assert.Equal("https://api.openai.com/v1", config.BaseUrl);
        Assert.Equal(5, config.MaxSeconds);
        Assert.Equal(-20.0, config.SilenceDb);
        Assert.Equal(3.0, config.SpeechMarginDb);
        Assert.Equal(0, config.HistoryLimit);
        Assert.Equal("auto", config.Language);
    }

    [Fact]
    public void The_dictionary_file_round_trips_and_updates_under_one_lock()
    {
        var store = new DictionaryStore(Path.Combine(_dir, "dict.json"));
        store.Save(new DictionaryFile { Entries = [new DictationTerm { Term = "Koy" }] });

        store.Update(file => file.Pending["x"] = 2);

        var loaded = store.Load();
        Assert.Equal("Koy", Assert.Single(loaded.Entries).Term);
        Assert.Equal(2, loaded.Pending["x"]);
    }
}

public class WavAudioTests
{
    [Fact]
    public void The_header_describes_16_khz_mono_16_bit_pcm()
    {
        var pcm = new byte[3200];

        var wav = WavAudio.FromPcm16(pcm);

        Assert.Equal(44 + 3200, wav.Length);
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(wav, 0, 4));
        Assert.Equal("WAVE", System.Text.Encoding.ASCII.GetString(wav, 8, 4));
        Assert.Equal(36 + 3200, BitConverter.ToInt32(wav, 4));
        Assert.Equal(16000, BitConverter.ToInt32(wav, 24));
        Assert.Equal(32000, BitConverter.ToInt32(wav, 28));
        Assert.Equal(3200, BitConverter.ToInt32(wav, 40));
    }

    [Fact]
    public void Seconds_follow_the_byte_count()
    {
        Assert.Equal(1.0, WavAudio.Seconds(32000), 6);
    }

    [Fact]
    public void Levels_of_silence_and_a_full_scale_square_wave()
    {
        Assert.Equal((0.0, 0.0), WavAudio.Levels(new byte[200]));

        var loud = new byte[200];
        for (var i = 0; i < 100; i++)
            BitConverter.TryWriteBytes(loud.AsSpan(i * 2), i % 2 == 0 ? short.MaxValue : short.MinValue);

        var (peak, rms) = WavAudio.Levels(loud);
        Assert.Equal(1.0, peak, 3);
        Assert.Equal(1.0, rms, 3);
    }

    [Fact]
    public void An_empty_block_has_no_level()
    {
        Assert.Equal((0.0, 0.0), WavAudio.Levels([]));
    }
}

public class WavChunkLevelsTests
{
    private static byte[] Tone(int samples, short value)
    {
        var pcm = new byte[samples * 2];
        for (var i = 0; i < samples; i++)
            BitConverter.TryWriteBytes(pcm.AsSpan(i * 2), value);
        return pcm;
    }

    [Fact]
    public void Levels_and_duration_come_out_of_a_wav_file()
    {
        var wav = WavAudio.FromPcm16([.. Tone(16000, 3277), .. Tone(16000, 0)]);

        var (seconds, levels) = WavAudio.ChunkLevels(wav, 0.064);

        Assert.Equal(2.0, seconds, 3);
        Assert.Equal(0.1, levels[0], 2);
        Assert.Equal(0.0, levels[^1], 3);
        Assert.InRange(levels.Count, 31, 32);
    }

    [Fact]
    public void A_stream_written_header_with_zero_sizes_still_reads_to_the_end()
    {
        var wav = WavAudio.FromPcm16(Tone(1600, 3277));
        BitConverter.TryWriteBytes(wav.AsSpan(40), 0); // data boyutu yazılmamış

        var (seconds, levels) = WavAudio.ChunkLevels(wav, 0.064);

        Assert.Equal(0.1, seconds, 3);
        Assert.NotEmpty(levels);
    }

    [Fact]
    public void Anything_that_is_not_a_wav_gives_nothing()
    {
        var (seconds, levels) = WavAudio.ChunkLevels(new byte[] { 1, 2, 3 }, 0.064);

        Assert.Equal(0.0, seconds);
        Assert.Empty(levels);
    }

    [Fact]
    public void A_format_other_than_16_bit_pcm_has_a_duration_but_no_levels()
    {
        var wav = WavAudio.FromPcm16(Tone(1600, 100));
        BitConverter.TryWriteBytes(wav.AsSpan(34), (short)8); // 8 bit

        var (_, levels) = WavAudio.ChunkLevels(wav, 0.064);

        Assert.Empty(levels);
    }
}

public class OpenAiProtocolTests
{
    [Theory]
    [InlineData("https://api.openai.com/v1", "audio/transcriptions", "https://api.openai.com/v1/audio/transcriptions")]
    [InlineData("http://localhost:8080/v1/", "/chat/completions", "http://localhost:8080/v1/chat/completions")]
    public void Endpoints_are_joined_with_one_slash(string baseUrl, string path, string expected)
    {
        Assert.Equal(expected, OpenAiProtocol.EndpointUrl(baseUrl, path));
    }

    [Theory]
    [InlineData("whisper-1", true)]
    [InlineData("whisper-large-v3-turbo", true)]
    [InlineData("gpt-4o-transcribe", false)]
    [InlineData("gpt-4o-mini-transcribe", false)]
    public void Verbose_json_is_asked_only_of_whisper_models(string model, bool expected)
    {
        Assert.Equal(expected, OpenAiProtocol.WantsVerboseJson(model));
    }

    [Fact]
    public void The_chat_request_escapes_text_and_orders_the_messages()
    {
        var json = OpenAiProtocol.BuildChatRequest("m", "sistem \"tırnaklı\"", "satır1\nsatır2");

        using var document = System.Text.Json.JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal("m", root.GetProperty("model").GetString());
        Assert.Equal(0, root.GetProperty("temperature").GetInt32());
        var messages = root.GetProperty("messages");
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal("sistem \"tırnaklı\"", messages[0].GetProperty("content").GetString());
        Assert.Equal("user", messages[1].GetProperty("role").GetString());
        Assert.Equal("satır1\nsatır2", messages[1].GetProperty("content").GetString());
    }

    [Fact]
    public void A_transcription_reply_gives_text_and_a_language_code()
    {
        var result = OpenAiProtocol.ParseTranscription("{\"text\": \" Merhaba dünya \", \"language\": \"turkish\"}");

        Assert.Equal("Merhaba dünya", result.Text);
        Assert.Equal("tr", result.DetectedLanguage);
    }

    [Fact]
    public void A_plain_text_reply_is_the_text_itself()
    {
        Assert.Equal("düz metin", OpenAiProtocol.ParseTranscription("düz metin\n").Text);
    }

    [Fact]
    public void A_chat_reply_is_read_from_a_string_or_from_parts()
    {
        Assert.Equal("Selam.", OpenAiProtocol.ParseChatReply("{\"choices\":[{\"message\":{\"content\":\"Selam.\"}}]}"));
        Assert.Equal("ab", OpenAiProtocol.ParseChatReply("{\"choices\":[{\"message\":{\"content\":[{\"text\":\"a\"},{\"text\":\"b\"}]}}]}"));
        Assert.Equal("", OpenAiProtocol.ParseChatReply("{\"choices\":[{\"message\":{\"content\":null}}]}"));
        Assert.Equal("", OpenAiProtocol.ParseChatReply("{\"choices\":[]}"));
        Assert.Equal("", OpenAiProtocol.ParseChatReply("<html>"));
    }

    [Fact]
    public void Error_messages_use_the_server_text_when_there_is_one()
    {
        Assert.Equal("HTTP 401: Incorrect API key", OpenAiProtocol.ErrorMessage(401, "{\"error\":{\"message\":\"Incorrect API key\"}}"));
        Assert.Equal("HTTP 400: bad", OpenAiProtocol.ErrorMessage(400, "{\"error\":\"bad\"}"));
        Assert.Equal("HTTP 502", OpenAiProtocol.ErrorMessage(502, "<html>"));
        Assert.Equal("HTTP 500", OpenAiProtocol.ErrorMessage(500, null));
    }

    [Theory]
    [InlineData(429, true)]
    [InlineData(408, true)]
    [InlineData(500, true)]
    [InlineData(503, true)]
    [InlineData(400, false)]
    [InlineData(401, false)]
    [InlineData(404, false)]
    public void Only_rate_limits_timeouts_and_server_errors_are_worth_waiting_for(int status, bool expected)
    {
        Assert.Equal(expected, OpenAiProtocol.IsTransient(status));
    }

    [Theory]
    [InlineData("turkish", "tr")]
    [InlineData("English", "en")]
    [InlineData("tr", "tr")]
    [InlineData("", "")]
    public void Language_names_become_codes(string name, string expected)
    {
        Assert.Equal(expected, OpenAiProtocol.LanguageCode(name));
    }
}
