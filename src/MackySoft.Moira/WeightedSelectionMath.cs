using System;

namespace MackySoft.Moira;

internal static class WeightedSelectionMath
{
    internal static void ValidateSample (double sample)
    {
        if (!IsValidSample(sample))
        {
            throw new ArgumentOutOfRangeException(nameof(sample), sample, "The sample must be finite and within [0, 1).");
        }
    }

    internal static void ValidateSamples (ReadOnlySpan<double> samples)
    {
        for (int ordinal = 0; ordinal < samples.Length; ordinal++)
        {
            double sample = samples[ordinal];
            if (!IsValidSample(sample))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(samples),
                    sample,
                    $"The sample at ordinal {ordinal} must be finite and within [0, 1).");
            }
        }
    }

    internal static bool IsFinite (double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    internal static bool IsFiniteNonNegative (double value)
    {
        return IsFinite(value) && value >= 0d;
    }

    internal static bool IsFinitePositive (double value)
    {
        return IsFinite(value) && value > 0d;
    }

    private static bool IsValidSample (double sample)
    {
        return IsFinite(sample) && sample >= 0d && sample < 1d;
    }

    internal static bool TryScale (double weight, double maximumWeight, out double scaledWeight)
    {
        scaledWeight = weight / maximumWeight;
        return IsFinitePositive(scaledWeight);
    }

    internal static bool TryCreateMass (double scaledWeight, int count, out double mass)
    {
        mass = scaledWeight * count;
        return IsFinitePositive(mass);
    }

    internal static bool TryAddMass (double cumulativeMass, double mass, out double nextMass)
    {
        nextMass = cumulativeMass + mass;
        return IsFinite(nextMass) && nextMass > cumulativeMass;
    }

    internal static double CorrectTarget (double sample, double totalMass)
    {
        double target = sample * totalMass;
        if (target < totalMass)
        {
            return target;
        }

        return PreviousDouble(totalMass);
    }

    internal static void SplitSample (
        double sample,
        int partitionCount,
        out int partition,
        out double partitionSample)
    {
        double scaledSample = sample * partitionCount;
        if (scaledSample < partitionCount)
        {
            partition = (int)scaledSample;
            partitionSample = scaledSample - partition;
            return;
        }

        partition = partitionCount - 1;
        partitionSample = PreviousDouble(1d);
    }

    private static double PreviousDouble (double value)
    {
        long bits = BitConverter.DoubleToInt64Bits(value);
        return BitConverter.Int64BitsToDouble(bits - 1L);
    }
}
