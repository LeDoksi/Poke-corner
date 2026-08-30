namespace PokeCorner.Core.Audio;

/// <summary>
/// Converts the stored <see cref="AppSettings.CrySoundVolume"/> percentage into the 0.0-1.0
/// scale <c>System.Windows.Media.MediaPlayer.Volume</c> takes.
///
/// The setting is a percent because that is what a slider and a "70%" readout want; the player
/// wants a fraction. Keeping the conversion here — rather than inline at the one call site in
/// App.PlayCry, which cannot be unit-tested without an audio device — is what makes the
/// clamping assertable: a hand-edited state.json carrying 900 or -5 must come out as full
/// volume and silence, never as an out-of-range value the player would throw on.
/// </summary>
public static class CryVolume
{
    public const int MinPercent = 0;
    public const int MaxPercent = 100;

    /// Zero maps to exactly 0.0, which MediaPlayer treats as genuine silence rather than as
    /// "very quiet" — the reason the volume slider can bottom out at "off".
    public static double ToPlayerVolume(int percent) =>
        Math.Clamp(percent, MinPercent, MaxPercent) / 100.0;
}
