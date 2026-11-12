namespace Koy.Core.Voice;

/// <summary>
/// Dikte edilen sesli komutu ve harness yapılandırmasını güvenli bir şekilde
/// ayrıştırarak yürütülebilir dosya ve argüman listesine dönüştürür.
/// ASLA kabuk (cmd.exe, powershell.exe) çalıştırmaz; doğrudan ArgumentList için bağımsız parçalar üretir.
/// </summary>
public static class VoiceCommandParser
{
    private static readonly HashSet<string> BlockedExecutables = new(StringComparer.OrdinalIgnoreCase)
    {
        "cmd", "cmd.exe",
        "powershell", "powershell.exe",
        "pwsh", "pwsh.exe",
        "bash", "bash.exe",
        "sh", "sh.exe",
        "wscript", "wscript.exe",
        "cscript", "cscript.exe"
    };

    private static readonly System.Buffers.SearchValues<char> DangerousShellCharacters =
        System.Buffers.SearchValues.Create(['|', '&', ';', '>', '<', '`', '$']);

    /// <summary>
    /// Harness türüne ve parametrelere göre yürütülecek komut ve argümanları çözer.
    /// </summary>
    public static ResolvedCommand Resolve(
        VoiceHarnessType harnessType,
        string prompt,
        string? customExecutable = null,
        string? customArgumentsTemplate = null,
        VoiceClaudePermission claudePermission = VoiceClaudePermission.Default,
        string? model = null)
    {
        var safePrompt = prompt ?? string.Empty;

        return harnessType switch
        {
            // "--" istemi seçenek olarak okunmasın diye konur: "-p --version" sürüm yazdırır, dikte "--version" olsa da istem olarak gider.
            // İzin bayrağı "--" öncesinde durur; sonrası istem sayılır.
            VoiceHarnessType.Claude => new ResolvedCommand("claude", ClaudeArguments(safePrompt, claudePermission, model)),
            VoiceHarnessType.Codex => new ResolvedCommand("codex", ["exec", "--", safePrompt]),
            // agy (Go) -p değer alır ve sonraki argümanı tire ile başlasa da değer sayar; "--" gerekmez, konursa istem "--" olur.
            VoiceHarnessType.Agy => new ResolvedCommand("agy", ["-p", safePrompt]),
            VoiceHarnessType.Custom => ResolveCustom(safePrompt, customExecutable, customArgumentsTemplate),
            _ => throw new ArgumentOutOfRangeException(nameof(harnessType), $"Bilinmeyen harness türü: {harnessType}")
        };
    }

    /// <summary>Ayarda "model yok" anlamına gelen değer: araç kendi varsayılanını kullanır.</summary>
    public const string DefaultModel = "default";

    // Model adı seçenek gibi okunmasın (ör. "--dangerously...") ve kabuk karakteri taşımasın: yalnız ad karakterleri.
    private static bool IsValidModel(string model) =>
        model.Length <= 80 && char.IsAsciiLetterOrDigit(model[0])
        && model.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-' or ':' or '[' or ']');

    private static List<string> ClaudeArguments(string prompt, VoiceClaudePermission permission, string? model)
    {
        var args = new List<string> { "-p" };

        var trimmed = model?.Trim();
        if (!string.IsNullOrEmpty(trimmed) && !trimmed.Equals(DefaultModel, StringComparison.OrdinalIgnoreCase))
        {
            if (!IsValidModel(trimmed))
                throw new VoiceCommandException("Invalid model name.", VoiceFailure.InvalidModel, trimmed);
            args.Add("--model");
            args.Add(trimmed);
        }

        switch (permission)
        {
            case VoiceClaudePermission.Auto:
                args.Add("--permission-mode");
                args.Add("auto");
                break;
            case VoiceClaudePermission.AcceptEdits:
                args.Add("--permission-mode");
                args.Add("acceptEdits");
                break;
        }

        // "--" istemden önce: seçenekler burada biter.
        args.Add("--");
        args.Add(prompt);
        return args;
    }

    private static ResolvedCommand ResolveCustom(string prompt, string? customExecutable, string? customTemplate)
    {
        if (string.IsNullOrWhiteSpace(customExecutable))
            throw new VoiceCommandException("Özel harness için yürütülebilir dosya adı boş olamaz.");

        if (customExecutable.IndexOfAny(DangerousShellCharacters) >= 0 || (customTemplate is not null && customTemplate.IndexOfAny(DangerousShellCharacters) >= 0))
            throw new VoiceCommandException("Komut kalıbında veya yürütülebilir dosya yolunda kabuk metakarakterleri (| & ; > < ` $) bulunamaz.");

        string exe;
        IReadOnlyList<string> args;

        if (string.IsNullOrWhiteSpace(customTemplate))
        {
            // customExecutable tüm komut satırını içerebilir (örn: 'gh copilot suggest "{prompt}"' veya 'tool.exe')
            var tokens = Tokenize(customExecutable);
            if (tokens.Count == 0)
                throw new VoiceCommandException("Özel harness komutu boş olamaz.");

            exe = tokens[0];
            var templateTokens = tokens.Skip(1).ToList();
            args = BuildArgumentsFromTokens(templateTokens, prompt);
        }
        else
        {
            exe = customExecutable.Trim();
            args = ParseCustomArguments(customTemplate, prompt);
        }

        var fileNameOnly = Path.GetFileName(exe);

        if (BlockedExecutables.Contains(fileNameOnly))
            throw new VoiceCommandException($"Güvenlik kuralı gereğince kabuk yorumlayıcıları ({fileNameOnly}) harness olarak çalıştırılamaz.");

        if (exe.IndexOfAny(DangerousShellCharacters) >= 0)
            throw new VoiceCommandException("Yürütülebilir dosya yolunda kabuk metakarakterleri (| & ; > < ` $) bulunamaz.");

        return new ResolvedCommand(exe, args);
    }

    /// <summary>
    /// Özel argüman kalıbını tırnaklara duyarlı olarak parçalar ve {prompt} yer tutucusunu güvenle yerleştirir.
    /// {prompt} şablonda yoksa istem argüman listesinin sonuna bağımsız bir öğe olarak eklenir.
    /// </summary>
    public static IReadOnlyList<string> ParseCustomArguments(string? template, string prompt)
    {
        if (string.IsNullOrWhiteSpace(template))
            return [prompt];

        var tokens = Tokenize(template);
        return BuildArgumentsFromTokens(tokens, prompt);
    }

    private static List<string> BuildArgumentsFromTokens(List<string> tokens, string prompt)
    {
        var promptReplaced = false;
        var result = new List<string>(tokens.Count + 1);

        foreach (var token in tokens)
        {
            if (token.Contains("{prompt}", StringComparison.OrdinalIgnoreCase))
            {
                // Yer tutucuyu tam olarak prompt metniyle değiştir
                result.Add(token.Replace("{prompt}", prompt, StringComparison.OrdinalIgnoreCase));
                promptReplaced = true;
            }
            else
            {
                result.Add(token);
            }
        }

        if (!promptReplaced)
            result.Add(prompt);

        return result;
    }

    private static List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        var span = text.AsSpan().Trim();
        var i = 0;

        while (i < span.Length)
        {
            while (i < span.Length && char.IsWhiteSpace(span[i]))
                i++;

            if (i >= span.Length)
                break;

            var inQuotes = false;
            var quoteChar = '\0';
            var start = i;
            var currentToken = new System.Text.StringBuilder();

            while (i < span.Length)
            {
                var c = span[i];
                if (!inQuotes && (c is '"' or '\''))
                {
                    inQuotes = true;
                    quoteChar = c;
                    i++;
                    continue;
                }

                if (inQuotes && c == quoteChar)
                {
                    inQuotes = false;
                    quoteChar = '\0';
                    i++;
                    continue;
                }

                if (!inQuotes && char.IsWhiteSpace(c))
                    break;

                currentToken.Append(c);
                i++;
            }

            if (currentToken.Length > 0)
                tokens.Add(currentToken.ToString());
        }

        return tokens;
    }
}
