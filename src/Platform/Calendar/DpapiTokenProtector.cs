using Koy.Core.Calendar;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.DataProtection;

namespace Koy.Platform.Calendar;

/// <summary>
/// Windows DPAPI (<c>LOCAL=user</c>, geçerli kullanıcı kapsamı) ile OAuth belirteçlerini şifreler ve çözer.
/// Ek NuGet paketi gerektirmez; Windows 11 yerleşik WinRT veri koruma sağlayıcısını kullanır.
/// </summary>
public sealed class DpapiTokenProtector : ITokenProtector
{
    public byte[] Protect(byte[] plainBytes)
    {
        ArgumentNullException.ThrowIfNull(plainBytes);
        var provider = new DataProtectionProvider("LOCAL=user");
        var input = CryptographicBuffer.CreateFromByteArray(plainBytes);
        var output = provider.ProtectAsync(input).AsTask().GetAwaiter().GetResult();
        CryptographicBuffer.CopyToByteArray(output, out var protectedBytes);
        return protectedBytes;
    }

    public byte[] Unprotect(byte[] protectedBytes)
    {
        ArgumentNullException.ThrowIfNull(protectedBytes);
        var provider = new DataProtectionProvider();
        var input = CryptographicBuffer.CreateFromByteArray(protectedBytes);
        var output = provider.UnprotectAsync(input).AsTask().GetAwaiter().GetResult();
        CryptographicBuffer.CopyToByteArray(output, out var plainBytes);
        return plainBytes;
    }
}
