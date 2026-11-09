namespace Koy.Core.Lyrics;

/// <summary>Zaman damgalı tek söz satırı. Metin boşsa enstrümantal aradır.</summary>
public readonly record struct LyricLine(TimeSpan Time, string Text);
