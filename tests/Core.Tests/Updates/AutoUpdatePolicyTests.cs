using Koy.Core.Updates;

namespace Koy.Core.Tests.Updates;

public class AutoUpdatePolicyTests
{
    [Fact]
    public void ShouldAutoStage_returns_true_only_when_all_guards_pass()
    {
        Assert.True(AutoUpdatePolicy.ShouldAutoStage(
            autoCheckUpdates: true,
            autoInstallUpdates: true,
            hasAvailableUpdate: true,
            alreadyReady: false,
            isEnergySaverOn: false));
    }

    [Theory]
    [InlineData(false, true, true, false, false)]
    [InlineData(true, false, true, false, false)]
    [InlineData(true, true, false, false, false)]
    [InlineData(true, true, true, true, false)]
    [InlineData(true, true, true, false, true)]
    public void ShouldAutoStage_returns_false_when_any_guard_blocks(
        bool autoCheckUpdates,
        bool autoInstallUpdates,
        bool hasAvailableUpdate,
        bool alreadyReady,
        bool isEnergySaverOn)
    {
        Assert.False(AutoUpdatePolicy.ShouldAutoStage(
            autoCheckUpdates,
            autoInstallUpdates,
            hasAvailableUpdate,
            alreadyReady,
            isEnergySaverOn));
    }
}
