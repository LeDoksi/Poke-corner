using System.Text;
using PokeCorner.Core.Audio;

namespace PokeCorner.Core.Tests.Audio;

/// The cry payload is generated once by an offline crawl and then never re-encoded, so a
/// malformed header would ship silently — nothing at runtime parses these files, it just
/// hands the path to SoundPlayer. These read the bytes back field by field instead.
public class PcmWavWriterTests
{
    private static byte[] Write(float[] samples, int sampleRate = 32728, int channels = 1)
    {
        using var stream = new MemoryStream();
        PcmWavWriter.Write(stream, samples, sampleRate, channels);
        return stream.ToArray();
    }

    [Fact]
    public void Write_ProducesACanonicalRiffWaveHeaderMatchingTheSampleData()
    {
        var samples = new[] { 0f, 0.5f, -0.5f, 0.25f };

        var bytes = Write(samples, sampleRate: 32728, channels: 1);

        Assert.Equal(44 + samples.Length * 2, bytes.Length);
        Assert.Equal("RIFF", Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.Equal(bytes.Length - 8, BitConverter.ToInt32(bytes, 4)); // RIFF size excludes its own 8 bytes
        Assert.Equal("WAVE", Encoding.ASCII.GetString(bytes, 8, 4));
        Assert.Equal("fmt ", Encoding.ASCII.GetString(bytes, 12, 4));
        Assert.Equal(16, BitConverter.ToInt32(bytes, 16));
        Assert.Equal(1, BitConverter.ToInt16(bytes, 20));          // WAVE_FORMAT_PCM
        Assert.Equal(1, BitConverter.ToInt16(bytes, 22));          // channels
        Assert.Equal(32728, BitConverter.ToInt32(bytes, 24));      // sample rate
        Assert.Equal(32728 * 2, BitConverter.ToInt32(bytes, 28));  // byte rate = rate * channels * 2
        Assert.Equal(2, BitConverter.ToInt16(bytes, 32));          // block align
        Assert.Equal(16, BitConverter.ToInt16(bytes, 34));         // bits per sample
        Assert.Equal("data", Encoding.ASCII.GetString(bytes, 36, 4));
        Assert.Equal(samples.Length * 2, BitConverter.ToInt32(bytes, 40));
    }

    [Fact]
    public void Write_RoundTripsSamplesToWithinSixteenBitPrecision()
    {
        var samples = new[] { 0f, 0.5f, -0.5f, 0.25f, -1f, 1f };

        var bytes = Write(samples);

        for (var i = 0; i < samples.Length; i++)
        {
            var decoded = BitConverter.ToInt16(bytes, 44 + i * 2) / (float)short.MaxValue;
            Assert.Equal(samples[i], decoded, tolerance: 1f / short.MaxValue);
        }
    }

    /// Scaling by 32768 would wrap a full-scale +1.0 sample to -32768 — an inverted, maximally
    /// loud click at exactly the peak of a cry, which is the worst possible place for one.
    [Fact]
    public void Write_ClampsFullScaleAndOutOfRangeSamplesInsteadOfWrapping()
    {
        var bytes = Write(new[] { 1f, -1f, 4f, -4f });

        Assert.Equal(short.MaxValue, BitConverter.ToInt16(bytes, 44));
        Assert.Equal(-short.MaxValue, BitConverter.ToInt16(bytes, 46));
        Assert.Equal(short.MaxValue, BitConverter.ToInt16(bytes, 48));
        Assert.Equal(-short.MaxValue, BitConverter.ToInt16(bytes, 50));
    }

    [Fact]
    public void Write_StereoDoublesByteRateAndBlockAlign()
    {
        var bytes = Write(new[] { 0.1f, 0.2f, 0.3f, 0.4f }, sampleRate: 44100, channels: 2);

        Assert.Equal(2, BitConverter.ToInt16(bytes, 22));
        Assert.Equal(44100 * 4, BitConverter.ToInt32(bytes, 28));
        Assert.Equal(4, BitConverter.ToInt16(bytes, 32));
    }

    [Fact]
    public void Write_LeavesTheDestinationStreamOpenForTheCaller()
    {
        using var stream = new MemoryStream();

        PcmWavWriter.Write(stream, new[] { 0.1f }, 22050, 1);

        Assert.True(stream.CanWrite); // a using/FileStream caller owns disposal, not the writer
    }
}
