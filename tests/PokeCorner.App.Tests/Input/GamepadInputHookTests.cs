using PokeCorner.App.Input;
using Xunit;

namespace PokeCorner.App.Tests.Input;

/// Covers the pure state-diffing logic — the part that decides whether a controller state change
/// is a discrete event or just continuous held state — plus the "no controller connected" path
/// against the real XInputGetState P/Invoke. Actually pressing a physical button cannot be
/// simulated here; that path is only exercised by real hardware.
public class GamepadInputHookTests
{
    private const ushort ButtonA = 0x1000;
    private const ushort ButtonB = 0x2000;

    private static GamepadSnapshot Snapshot(
        ushort buttons = 0, byte leftTrigger = 0, byte rightTrigger = 0,
        short thumbLX = 0, short thumbLY = 0, short thumbRX = 0, short thumbRY = 0)
        => new()
        {
            Buttons = buttons,
            LeftTrigger = leftTrigger,
            RightTrigger = rightTrigger,
            ThumbLX = thumbLX,
            ThumbLY = thumbLY,
            ThumbRX = thumbRX,
            ThumbRY = thumbRY,
        };

    [Fact]
    public void CountDiscreteEvents_NothingChanged_CountsZero()
        => Assert.Equal(0, GamepadInputHook.CountDiscreteEvents(Snapshot(), Snapshot()));

    [Fact]
    public void CountDiscreteEvents_ButtonPressed_CountsOnce()
        => Assert.Equal(1, GamepadInputHook.CountDiscreteEvents(Snapshot(), Snapshot(buttons: ButtonA)));

    [Fact]
    public void CountDiscreteEvents_ButtonStillHeld_CountsZero()
        => Assert.Equal(0, GamepadInputHook.CountDiscreteEvents(Snapshot(buttons: ButtonA), Snapshot(buttons: ButtonA)));

    [Fact]
    public void CountDiscreteEvents_ButtonReleased_CountsZero()
        => Assert.Equal(0, GamepadInputHook.CountDiscreteEvents(Snapshot(buttons: ButtonA), Snapshot()));

    [Fact]
    public void CountDiscreteEvents_TwoButtonsPressedAtOnce_CountsBoth()
        => Assert.Equal(2, GamepadInputHook.CountDiscreteEvents(Snapshot(), Snapshot(buttons: ButtonA | ButtonB)));

    [Fact]
    public void CountDiscreteEvents_SecondButtonAddedWhileFirstHeld_CountsOnlyTheNewOne()
        => Assert.Equal(1, GamepadInputHook.CountDiscreteEvents(
            Snapshot(buttons: ButtonA), Snapshot(buttons: ButtonA | ButtonB)));

    [Fact]
    public void CountDiscreteEvents_TriggerCrossesThreshold_CountsOnce()
        => Assert.Equal(1, GamepadInputHook.CountDiscreteEvents(Snapshot(), Snapshot(leftTrigger: 200)));

    [Fact]
    public void CountDiscreteEvents_TriggerHeldDownAcrossPolls_CountsZeroMore()
        => Assert.Equal(0, GamepadInputHook.CountDiscreteEvents(
            Snapshot(leftTrigger: 200), Snapshot(leftTrigger: 255)));

    [Fact]
    public void CountDiscreteEvents_TriggerBelowThreshold_CountsZero()
        => Assert.Equal(0, GamepadInputHook.CountDiscreteEvents(
            Snapshot(), Snapshot(leftTrigger: GamepadInputHook.TriggerThreshold)));

    [Fact]
    public void CountDiscreteEvents_StickLeavesDeadzone_CountsOnce()
        => Assert.Equal(1, GamepadInputHook.CountDiscreteEvents(Snapshot(), Snapshot(thumbLX: 32000)));

    [Fact]
    public void CountDiscreteEvents_StickHeldDeflectedAcrossPolls_CountsZeroMore()
        => Assert.Equal(0, GamepadInputHook.CountDiscreteEvents(
            Snapshot(thumbLX: 32000), Snapshot(thumbLX: 31000, thumbLY: 4000)));

    [Fact]
    public void CountDiscreteEvents_StickDriftInsideDeadzone_CountsZero()
        => Assert.Equal(0, GamepadInputHook.CountDiscreteEvents(
            Snapshot(), Snapshot(thumbLX: 5000, thumbLY: 5000)));

    [Fact]
    public void CountDiscreteEvents_StickReturnsToCentreThenDeflectsAgain_CountsEachDeflection()
    {
        var centred = Snapshot();
        var deflected = Snapshot(thumbRX: -30000);

        Assert.Equal(1, GamepadInputHook.CountDiscreteEvents(centred, deflected));
        Assert.Equal(0, GamepadInputHook.CountDiscreteEvents(deflected, centred));
        Assert.Equal(1, GamepadInputHook.CountDiscreteEvents(centred, deflected));
    }

    [Fact]
    public void CountDiscreteEvents_ButtonTriggerAndStickAtOnce_CountsAllThree()
        => Assert.Equal(3, GamepadInputHook.CountDiscreteEvents(
            Snapshot(), Snapshot(buttons: ButtonA, rightTrigger: 255, thumbLY: -32000)));

    /// Real P/Invoke against real slots. On a machine with no controller every slot answers
    /// ERROR_DEVICE_NOT_CONNECTED, which must come back as a quiet `false` rather than an
    /// exception — and if a controller IS attached, `true` with a readable state is equally fine.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void TryReadState_AnySlot_DoesNotThrow(int userIndex)
    {
        var exception = Record.Exception(() => GamepadInputHook.TryReadState(userIndex, out _));

        Assert.Null(exception);
    }

    [Fact]
    public void StartStop_WithNoControllerConnected_RecordsNothingAndDisposesCleanly()
    {
        var counter = new InputEventCounter();
        using var hook = new GamepadInputHook(counter);

        hook.Start();
        Thread.Sleep(500); // several poll intervals (125ms each)
        hook.Stop();

        // Nothing is touching a controller during a test run, so an untouched (or absent)
        // controller must contribute exactly zero units.
        Assert.Equal(0, counter.DrainUnits());
    }
}
