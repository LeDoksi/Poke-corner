using PokeCorner.Core;
using Xunit;

namespace PokeCorner.Core.Tests;

public class PetSpotMatcherTests
{
    private static readonly MonitorDescriptor Primary = new(@"\\.\DISPLAY1", 0, 0, 2560, 1440);
    private static readonly MonitorDescriptor Second = new(@"\\.\DISPLAY2", 2560, 0, 1920, 1080);
    private static readonly PetSpot OnSecond = new(@"\\.\DISPLAY2", 2560, 0, 1920, 1080, 1700, 900);

    [Fact]
    public void FindsTheSameMonitor()
    {
        Assert.Equal(1, PetSpotMatcher.FindMonitor(OnSecond, [Primary, Second]));
    }

    [Fact]
    public void SecondMonitorNotAttachedYet_AfterReboot_IsNotFound_SoThePetIsNotPinnedToThePrimary()
    {
        Assert.Equal(-1, PetSpotMatcher.FindMonitor(OnSecond, [Primary]));
    }

    [Fact]
    public void GameShrinksThePrimary_SecondMonitorShiftsLeft_StillFoundByName()
    {
        var gamePrimary = Primary with { Width = 1920, Height = 1080 };
        var shiftedSecond = Second with { X = 1920 };
        Assert.Equal(1, PetSpotMatcher.FindMonitor(OnSecond, [gamePrimary, shiftedSecond]));
    }

    [Fact]
    public void WindowsRenumberedTheDisplays_SameRectWinsOverSameName()
    {
        var renamedPrimary = Primary with { DeviceName = @"\\.\DISPLAY2" };
        var renamedSecond = Second with { DeviceName = @"\\.\DISPLAY3" };
        Assert.Equal(1, PetSpotMatcher.FindMonitor(OnSecond, [renamedPrimary, renamedSecond]));
    }

    [Fact]
    public void NameMatchIsCaseInsensitive()
    {
        var lower = Second with { DeviceName = @"\\.\display2", X = 0 };
        Assert.Equal(0, PetSpotMatcher.FindMonitor(OnSecond, [lower]));
    }
}
