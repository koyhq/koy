using Koy.Core.Voice;

namespace Koy.Core.Tests;

public class HallucinationFilterTests
{
    [Theory]
    [InlineData("Thanks for watching!")]
    [InlineData("  thank you for watching.  ")]
    [InlineData("İzlediğiniz için teşekkürler")]
    [InlineData("Abone olmayı unutmayın")]
    [InlineData("Altyazı M.K.")]
    [InlineData("Çeviri ve Altyazı M.K.")]
    [InlineData("Altyazı ve Çeviri M.K.")]
    public void Stock_phrases_from_a_short_clip_are_invented(string text)
    {
        Assert.True(HallucinationFilter.LooksInvented(text, 2));
    }

    [Theory]
    [InlineData("evet")]
    [InlineData("bye")]
    [InlineData("tamam")]
    [InlineData("Altyazı")]
    [InlineData("Toplantıya abone olmayı unutmayın ve notları paylaşın")]
    [InlineData("")]
    [InlineData("   ")]
    public void Real_speech_and_single_words_are_kept(string text)
    {
        Assert.False(HallucinationFilter.LooksInvented(text, 2));
    }

    [Fact]
    public void A_long_clip_is_never_judged()
    {
        Assert.False(HallucinationFilter.LooksInvented("Thanks for watching", HallucinationFilter.MaxClipSeconds + 1));
    }

    [Theory]
    [InlineData("İzlediğiniz", "izlediginiz")]
    [InlineData("Altyazı M.K.", "altyazi m k")]
    [InlineData("ISIK ışık", "isik isik")]
    public void Fold_lowercases_strips_accents_and_punctuation(string input, string expected)
    {
        Assert.Equal(expected, HallucinationFilter.Fold(input));
    }
}
