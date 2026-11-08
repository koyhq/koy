using Koy.Core.JumpList;

namespace Koy.Core.Tests.JumpList;

public class JumpListAppIdTests
{
    // Windows 11 build 26200'de doğrulanmış vektörler (ROADMAP).
    [Theory]
    [InlineData("Chrome", "5d696d521de238c3")]
    [InlineData("MSEdge", "ccba5a5986c77e43")]
    [InlineData("Microsoft.VisualStudioCode", "1ced32d74a95c7bc")]
    public void Known_vectors_match(string appId, string expected)
    {
        Assert.Equal(expected, JumpListAppId.HashName(appId));
    }

    [Fact]
    public void Hash_ignores_case_because_the_id_is_uppercased()
    {
        Assert.Equal(JumpListAppId.HashName("Chrome"), JumpListAppId.HashName("CHROME"));
        Assert.Equal(JumpListAppId.HashName("Chrome"), JumpListAppId.HashName("chrome"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_id_has_no_hash(string? appId)
    {
        Assert.Null(JumpListAppId.HashName(appId));
    }

    [Fact]
    public void Hash_has_no_leading_zero_padding()
    {
        // Windows %x ile yazar; baştaki sıfırlar atılır (diskte 15 haneli adlar var). Çok sayıda kimlikten biri
        // 1/16 olasılıkla sıfırla başlar; hiçbirinde 16 haneden uzun ad ya da sıfırla başlayan ad çıkmamalı.
        for (var i = 0; i < 400; i++)
        {
            var name = JumpListAppId.HashName("Test.App." + i)!;
            Assert.InRange(name.Length, 1, 16);
            Assert.False(name.Length > 1 && name[0] == '0');
            Assert.Equal(name, name.ToLowerInvariant());
        }
    }
}
