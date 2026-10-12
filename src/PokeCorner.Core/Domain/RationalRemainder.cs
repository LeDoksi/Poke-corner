using System.Numerics;
namespace PokeCorner.Core.Persistence;

public sealed class RationalRemainder
{
    public long Numerator { get; set; }
    public long Denominator { get; set; } = 1;

    public long Settle(long numerator, long denominator)
    {
        if (numerator < 0) throw new ArgumentOutOfRangeException(nameof(numerator));
        if (denominator <= 0) throw new ArgumentOutOfRangeException(nameof(denominator));
        if (Denominator <= 0 || Numerator < 0 || Numerator >= Denominator)
            throw new InvalidOperationException("Invalid persisted remainder.");
        var bottom = (BigInteger)Denominator * denominator;
        var top = (BigInteger)Numerator * denominator + (BigInteger)numerator * Denominator;
        var units = top / bottom;
        var remainder = top % bottom;
        var divisor = BigInteger.GreatestCommonDivisor(remainder, bottom);
        remainder /= divisor;
        bottom /= divisor;
        var settled = checked((long)units);
        var savedNumerator = checked((long)remainder);
        var savedDenominator = checked((long)bottom);
        Numerator = savedNumerator;
        Denominator = savedDenominator;
        return settled;
    }
}
