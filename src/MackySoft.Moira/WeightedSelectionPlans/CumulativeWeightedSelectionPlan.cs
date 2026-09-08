using System;

namespace MackySoft.Moira;

/// <summary>
/// Stores and executes the compiled cumulative plan for one fixed weighted distribution.
/// </summary>
internal readonly struct CumulativeWeightedSelectionPlan
{
    private readonly double[] cumulativeMasses;
    private readonly double totalMass;

    private CumulativeWeightedSelectionPlan (double[] cumulativeMasses, double totalMass)
    {
        this.cumulativeMasses = cumulativeMasses;
        this.totalMass = totalMass;
    }

    internal static CumulativeWeightedSelectionPlan Create (double[] scaledWeights, double totalMass)
    {
        double cumulativeMass = 0d;
        for (int valueIndex = 0; valueIndex < scaledWeights.Length; valueIndex++)
        {
            cumulativeMass += scaledWeights[valueIndex];
            scaledWeights[valueIndex] = cumulativeMass;
        }

        return new CumulativeWeightedSelectionPlan(scaledWeights, totalMass);
    }

    internal int SelectValueIndex (double sample)
    {
        double target = WeightedSelectionMath.CorrectTarget(sample, totalMass);
        int lower = 0;
        int upper = cumulativeMasses.Length - 1;
        while (lower < upper)
        {
            int middle = lower + ((upper - lower) / 2);
            if (cumulativeMasses[middle] > target)
            {
                upper = middle;
            }
            else
            {
                lower = middle + 1;
            }
        }

        return lower;
    }

    internal void SelectMany<T> (
        ReadOnlySpan<double> samples,
        ReadOnlySpan<T> values,
        Span<T> destination)
    {
        for (int ordinal = 0; ordinal < samples.Length; ordinal++)
        {
            destination[ordinal] = values[SelectValueIndex(samples[ordinal])]!;
        }
    }
}
