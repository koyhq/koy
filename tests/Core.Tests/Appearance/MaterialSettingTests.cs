using Koy.Core.Appearance;

namespace Koy.Core.Tests.Appearance;

public class MaterialSettingTests
{
    [Theory]
    [InlineData(MaterialLevel.Solid)]
    [InlineData(MaterialLevel.Frosted)]
    [InlineData(MaterialLevel.Glass)]
    public void ResolveWindowMaterial_ReturnsSolid_WhenBackdropUnsupported(MaterialLevel requested)
    {
        var result = MaterialSetting.ResolveWindowMaterial(requested, supportsBackdrop: false);

        Assert.Equal(MaterialLevel.Solid, result);
    }

    [Theory]
    [InlineData(MaterialLevel.Solid)]
    [InlineData(MaterialLevel.Frosted)]
    [InlineData(MaterialLevel.Glass)]
    public void ResolveWindowMaterial_PreservesRequested_WhenBackdropSupported(MaterialLevel requested)
    {
        var result = MaterialSetting.ResolveWindowMaterial(requested, supportsBackdrop: true);

        Assert.Equal(requested, result);
    }
}
