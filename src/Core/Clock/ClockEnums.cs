namespace Koy.Core.Clock;

// Bu dosyadaki enum adları settings.json'a yazılır: yayımlandıktan sonra ADLARI DEĞİŞTİRİLMEZ (yalnız yeni değer eklenir).

/// <summary>Saat biçimi. Auto kültürü izler; Hour12 ve Hour24 zorlar.</summary>
public enum ClockHourFormat
{
    Auto,
    Hour12,
    Hour24
}

/// <summary>Haftanın ilk günü. System kültürün (Windows bölge ayarının) ilk gününü kullanır.</summary>
public enum WeekStartSetting
{
    System,
    Saturday,
    Sunday,
    Monday
}
