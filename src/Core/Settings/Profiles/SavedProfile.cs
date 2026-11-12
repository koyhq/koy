namespace Koy.Core.Settings.Profiles;

/// <summary>
/// Kayıtlı bir profil: kalıcı kimlik (yeniden adlandırmada değişmez; kurallar bunu tutar), görünen ad ve
/// <see cref="SettingsProfileCodec"/> metni. Metin okunurken her seferinde yeniden doğrulanır; dosya elle bozulsa da
/// uygulanmadan reddedilir.
/// </summary>
public sealed record SavedProfile(string Id, string Name, string Json);
