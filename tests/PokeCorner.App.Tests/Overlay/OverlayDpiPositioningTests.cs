using System.Windows;
using PokeCorner.App.Overlay;
using PokeCorner.App.Tests.Theme;

namespace PokeCorner.App.Tests.Overlay;

/// The overlay was completely invisible on a 150%-scaled laptop because screen geometry from
/// System.Windows.Forms.Screen (PHYSICAL pixels) was assigned straight to WPF's Left/Top/
/// Width/Height (DIPs). The two agree only at 100%, which is the dev machine's scaling —
/// so the arithmetic below is the part that has to be proven at 1.5x, not just at 1.0x.
///
/// The round-trip assertions are the real test: they take the DIP coordinate the code
/// produces, re-multiply it by the scale factor exactly as Windows does when it places the
/// window, and check the result lands inside the physical screen the pet was aimed at.
public class OverlayDpiPositioningTests
{
    private static readonly System.Drawing.Rectangle FullHdWorkArea = new(0, 0, 1920, 1032); // 1080 minus a 48px taskbar

    [Fact]
    public void ToDips_AtOneHundredPercent_IsIdentity()
    {
        var dips = OverlayWindow.ToDips(FullHdWorkArea, new DpiScale(1.0, 1.0));

        Assert.Equal(new Rect(0, 0, 1920, 1032), dips);
    }

    [Fact]
    public void ToDips_AtOneHundredFiftyPercent_ShrinksPhysicalPixelsToDips()
    {
        var dips = OverlayWindow.ToDips(FullHdWorkArea, new DpiScale(1.5, 1.5));

        Assert.Equal(1280, dips.Width, 3);
        Assert.Equal(688, dips.Height, 3);
    }

    [Fact]
    public void ToDips_OffsetSecondMonitor_ConvertsTheOriginToo()
    {
        // A second monitor to the right of a 1920-wide primary starts at physical x=1920.
        var dips = OverlayWindow.ToDips(new System.Drawing.Rectangle(1920, 0, 1920, 1032), new DpiScale(1.5, 1.5));

        Assert.Equal(1280, dips.Left, 3);
        Assert.Equal(2560, dips.Right, 3);
    }

    /// The exact reported failure: bottom-right on a 1920x1080 laptop at 150%.
    [Fact]
    public void CornerPosition_BottomRightAtOneHundredFiftyPercent_LandsOnScreenNotPastIt()
    {
        var dpi = new DpiScale(1.5, 1.5);
        const int size = 96;

        var position = OverlayWindow.CornerPosition(
            ScreenCorner.BottomRight, OverlayWindow.ToDips(FullHdWorkArea, dpi), size);

        // What Windows will actually do with those DIPs.
        var physicalLeft = position.X * dpi.DpiScaleX;
        var physicalTop = position.Y * dpi.DpiScaleY;
        var physicalSize = size * dpi.DpiScaleX;

        Assert.InRange(physicalLeft, 0, FullHdWorkArea.Right - physicalSize);
        Assert.InRange(physicalTop, 0, FullHdWorkArea.Bottom - physicalSize);

        // ...and specifically snug in the corner, one margin (12 DIPs = 18 physical px) off.
        Assert.Equal(FullHdWorkArea.Right - physicalSize - 18, physicalLeft, 3);
        Assert.Equal(FullHdWorkArea.Bottom - physicalSize - 18, physicalTop, 3);
    }

    /// The pre-fix behaviour, pinned so a regression is unmistakable: assigning the raw
    /// physical corner as if it were DIPs put the pet ~950px past the right edge.
    [Fact]
    public void CornerPosition_TheOldPhysicalPixelMath_WouldHaveLandedOffScreen()
    {
        var dpi = new DpiScale(1.5, 1.5);
        const int size = 96;

        var wrongLeftAsIfDips = FullHdWorkArea.Right - size - 12; // what the old code assigned
        var wouldRenderAtPhysical = wrongLeftAsIfDips * dpi.DpiScaleX;

        Assert.True(wouldRenderAtPhysical > FullHdWorkArea.Right,
            "the old math must be off-screen, otherwise this test is not pinning the reported bug");
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(2.0)]
    public void CornerPosition_EveryCornerAtEveryScale_StaysInsideThePhysicalWorkArea(double scale)
    {
        var dpi = new DpiScale(scale, scale);
        const int size = 128;
        var workAreaDips = OverlayWindow.ToDips(FullHdWorkArea, dpi);

        foreach (var corner in Enum.GetValues<ScreenCorner>())
        {
            var position = OverlayWindow.CornerPosition(corner, workAreaDips, size);

            Assert.InRange(position.X * scale, 0, FullHdWorkArea.Right - size * scale);
            Assert.InRange(position.Y * scale, 0, FullHdWorkArea.Bottom - size * scale);
        }
    }

    [Fact]
    public void IsOnWorkArea_RememberedDipPosition_IsJudgedAgainstConvertedBounds()
    {
        var dpi = new DpiScale(1.5, 1.5);
        var workAreaDips = OverlayWindow.ToDips(FullHdWorkArea, dpi); // 1280 x 688 DIPs

        // A pet parked near the bottom-right by hand: a legitimate, still-visible DIP spot.
        Assert.True(OverlayWindow.IsOnWorkArea(1170, 580, 96, workAreaDips));

        // And a stale one from a disconnected monitor, which must be rejected so the corner
        // setting takes over instead of stranding the pet.
        Assert.False(OverlayWindow.IsOnWorkArea(2400, 580, 96, workAreaDips));
    }

    /// The guard's own regression: 1400 is a physical-pixel value that sits inside the raw
    /// 1920-wide WorkingArea but outside the 1280-wide DIP one. Comparing DIPs against
    /// unconverted physical bounds — the pre-fix code — accepted exactly this and left the
    /// pet off-screen; the converted comparison rejects it.
    [Fact]
    public void IsOnWorkArea_PositionBeyondTheDipWidth_IsRejectedEvenThoughItFitsThePhysicalWidth()
    {
        var workAreaDips = OverlayWindow.ToDips(FullHdWorkArea, new DpiScale(1.5, 1.5));

        Assert.False(OverlayWindow.IsOnWorkArea(1400, 100, 96, workAreaDips));
        Assert.True(1400 <= FullHdWorkArea.Right, "guard premise: this value does fit the physical bounds");
    }

    /// The end-to-end wiring check, on a real OverlayWindow against the REAL monitor this
    /// suite happens to be running on: whatever PositionAt assigns must, once the OS
    /// re-multiplies it by the scale factor, land the pet inside that monitor's physical
    /// working area. Correct pure helpers that PositionAt forgot to call would still fail it.
    ///
    /// It asserts on SpriteBounds, not on the window rect, and the difference matters: the
    /// window is deliberately SpriteScaleHeadroom times the pet size so a scale-up reaction
    /// has room to grow into, and its origin therefore sits one headroom-pad OUTSIDE the
    /// corner the pet is anchored to. The window rect being slightly off the working area is
    /// correct and invisible (that band is transparent and click-through); the sprite inside
    /// it being off would be the actual bug. Asserting on the sprite also pins the headroom
    /// offset itself — drop the `- headroomPad` from PositionAt and this fails.
    ///
    /// Honest limitation, stated here because it is the whole reason the DPI bug shipped: on a
    /// 100%-scaled machine the scale factor is 1.0, so the DIP conversion passes with or
    /// without it and proves only that nothing regressed. It is written against the live DPI
    /// on purpose — run the suite on the 150% laptop and this is the gold-standard proof,
    /// failing hard on the pre-fix code.
    [Fact]
    public void PositionAt_OnTheRealPrimaryMonitor_PutsTheVisibleSpriteInsideItsPhysicalWorkArea()
    {
        var workArea = System.Windows.Forms.Screen.PrimaryScreen!.WorkingArea;

        foreach (var corner in Enum.GetValues<ScreenCorner>())
        {
            WpfTestHost.Run(() =>
            {
                var window = new OverlayWindow();
                try
                {
                    const int size = 96;
                    window.PositionAt(corner, monitorIndex: -1, sizePixels: size); // -1 => PrimaryScreen

                    var sprite = window.SpriteBounds;
                    Assert.Equal(size, sprite.Width, 3);
                    Assert.Equal(size, sprite.Height, 3);

                    var dpi = OverlayWindow.DpiFor(window);
                    var physicalLeft = sprite.Left * dpi.DpiScaleX;
                    var physicalTop = sprite.Top * dpi.DpiScaleY;

                    Assert.InRange(physicalLeft, workArea.Left, workArea.Right - size * dpi.DpiScaleX);
                    Assert.InRange(physicalTop, workArea.Top, workArea.Bottom - size * dpi.DpiScaleY);
                }
                finally
                {
                    window.Close();
                }
            });
        }
    }

    /// The clipping fix's own guard: the window must be strictly bigger than the sprite by
    /// enough for the LARGEST reaction scale, or a bounce gets shaved off at the window edge
    /// again. PlayEvolutionFlash peaks at 1.4 and the sprite scales about its own centre, so
    /// the clearance on each side has to cover (peak - 1) / 2 of the sprite.
    [Fact]
    public void PositionAt_LeavesEnoughHeadroomForTheLargestReactionScale()
    {
        WpfTestHost.Run(() =>
        {
            var window = new OverlayWindow();
            try
            {
                const int size = 96;
                window.PositionAt(ScreenCorner.BottomRight, monitorIndex: -1, sizePixels: size);

                const double largestReactionPeakScale = 1.4; // PlayEvolutionFlash
                var clearancePerSide = (window.Width - size) / 2;
                var neededPerSide = size * (largestReactionPeakScale - 1) / 2;

                // Epsilon because both sides are the same quantity reached by different
                // multiplications (1.4x then halve, vs 0.4x then halve), which land a few
                // ulps apart — 19.19999999999999 against 19.199999999999996.
                Assert.True(clearancePerSide >= neededPerSide - 1e-9,
                    $"a {largestReactionPeakScale}x scale needs {neededPerSide} DIPs of clearance per side, window leaves {clearancePerSide}");
                Assert.Equal(window.Width, window.Height, 3);
            }
            finally
            {
                window.Close();
            }
        });
    }
}
