using Koy.Core.Settings;

namespace Koy.Core.Rules;

/// <summary>
/// Kural dosyası (ayar klasöründe <c>rules.json</c>). Bilerek settings.json'ın dışında: profil uygulamak ayar bölümlerini
/// değiştirir, kural listesini değiştiremez (kural → profil → kural döngüsü yapısal olarak yok). Dosya yok = kural yok.
/// Okuma hatasında boş liste, yazma hatasında false (bkz. <see cref="JsonListFile{T}"/>).
/// </summary>
public sealed class RuleStore(string path)
{
    private readonly JsonListFile<RuleDraft> _file = new(path, DataJsonContext.Default.ListRuleDraft);

    public static string DefaultPath { get; } = JsonListFile.InSettingsFolder("rules.json");

    public string Path => _file.Path;

    /// <summary>Ham kayıtlar (düzenleme için). Boş öğeler atılır.</summary>
    public IReadOnlyList<RuleDraft> LoadDrafts() => _file.Load().Where(d => d is not null).ToList();

    public bool Save(IReadOnlyList<RuleDraft> drafts) => _file.Save([.. drafts]);

    /// <summary>Doğrulanmış, kullanıma hazır kurallar (geçersizler atılır, sıra korunur).</summary>
    public IReadOnlyList<AutoRule> LoadRules() => RuleSetValidator.Validate(LoadDrafts()).Rules;
}
