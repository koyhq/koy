using System.Windows.Media;

namespace Koy.App.Features.Settings;

/// <summary>Yazı tipi açılır listesinde bir seçenek: kaydedilen değer (boş = varsayılan), görünen ad ve önizleme yazı tipi.</summary>
public sealed record FontOption(string Value, string Label, FontFamily Preview);
