using System.Text;

namespace PokeCorner.Core.Audio;

/// <summary>
/// Writes a canonical 44-byte-header RIFF/WAVE file holding 16-bit PCM.
///
/// This exists because System.Media.SoundPlayer — the only zero-dependency way to play a
/// sound on a stock Windows install — plays WAV and nothing else, while the upstream
/// species cries are Ogg Vorbis. The conversion happens ONCE, in the offline catalog crawl
/// that seeds Data\cries, so the shipped app never decodes anything and never depends on an
/// OS codec being present (the same reason sprites, animated GIFs and the catalog are all
/// pre-fetched and bundled rather than fetched or decoded on the target machine).
///
/// Deliberately only the canonical PCM subset of WAV: no fact chunk, no WAVE_FORMAT_EXTENSIBLE,
/// no float samples. That is the shape every Windows audio path has understood since 1995.
/// </summary>
public static class PcmWavWriter
{
    /// Samples are interleaved by channel and expected in [-1, 1]; anything outside that is
    /// clamped rather than allowed to wrap around into a loud click.
    public static void Write(Stream destination, float[] interleavedSamples, int sampleRate, int channels)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(interleavedSamples);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(channels);

        var dataBytes = interleavedSamples.Length * 2;
        var writer = new BinaryWriter(destination, Encoding.ASCII, leaveOpen: true);

        writer.Write("RIFF"u8);
        writer.Write(36 + dataBytes);          // everything after this field
        writer.Write("WAVE"u8);

        writer.Write("fmt "u8);
        writer.Write(16);                      // PCM fmt chunk size
        writer.Write((short)1);                // WAVE_FORMAT_PCM
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * 2); // byte rate
        writer.Write((short)(channels * 2));     // block align
        writer.Write((short)16);                 // bits per sample

        writer.Write("data"u8);
        writer.Write(dataBytes);
        foreach (var sample in interleavedSamples)
        {
            // 32767, not 32768: scaling by 32768 lets a sample of exactly 1.0 overflow to
            // -32768 and pop.
            writer.Write((short)(Math.Clamp(sample, -1f, 1f) * short.MaxValue));
        }

        writer.Flush();
    }
}
