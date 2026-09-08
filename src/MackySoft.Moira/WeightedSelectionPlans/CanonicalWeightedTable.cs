using System;

namespace MackySoft.Moira;

/// <summary>
/// Owns the validated, source-ordered values and scaled weights that can participate in a fixed distribution.
/// </summary>
/// <typeparam name="T">The type of value selected from the distribution.</typeparam>
/// <remarks>
/// Zero-weight entries are excluded from the value and weight arrays, while <see cref="SourceEntryCount" /> retains
/// the original entry count. Equal values, equal references, and <see langword="null" /> values remain separate entries.
/// </remarks>
internal readonly struct CanonicalWeightedTable<T>
{
    private CanonicalWeightedTable (
        T[] values,
        double[] scaledWeights,
        double totalMass,
        int sourceEntryCount)
    {
        Values = values;
        ScaledWeights = scaledWeights;
        TotalMass = totalMass;
        SourceEntryCount = sourceEntryCount;
    }

    internal T[] Values { get; }

    internal double[] ScaledWeights { get; }

    internal double TotalMass { get; }

    internal int SourceEntryCount { get; }

    internal static CanonicalWeightedTable<T> Create (
        ReadOnlySpan<WeightedEntry<T>> entries,
        string parameterName)
    {
        if (entries.Length == 0)
        {
            throw new ArgumentException("At least one entry is required.", parameterName);
        }

        ValidateWeights(entries, parameterName, out double maximumWeight, out int selectableEntryCount);
        if (selectableEntryCount == 0)
        {
            throw new ArgumentException("At least one entry must have a positive weight.", parameterName);
        }

        T[] values = new T[selectableEntryCount];
        double[] scaledWeights = new double[selectableEntryCount];
        double totalMass = PopulateSelectableEntries(
            entries,
            maximumWeight,
            parameterName,
            values,
            scaledWeights);

        return new CanonicalWeightedTable<T>(values, scaledWeights, totalMass, entries.Length);
    }

    private static void ValidateWeights (
        ReadOnlySpan<WeightedEntry<T>> entries,
        string parameterName,
        out double maximumWeight,
        out int selectableEntryCount)
    {
        maximumWeight = 0d;
        selectableEntryCount = 0;
        for (int sourceOrdinal = 0; sourceOrdinal < entries.Length; sourceOrdinal++)
        {
            double weight = entries[sourceOrdinal].Weight;
            if (!WeightedSelectionMath.IsFiniteNonNegative(weight))
            {
                throw new ArgumentException(
                    $"The weight at ordinal {sourceOrdinal} must be finite and nonnegative.",
                    parameterName);
            }

            if (weight == 0d)
            {
                continue;
            }

            selectableEntryCount++;
            if (weight > maximumWeight)
            {
                maximumWeight = weight;
            }
        }
    }

    private static double PopulateSelectableEntries (
        ReadOnlySpan<WeightedEntry<T>> entries,
        double maximumWeight,
        string parameterName,
        Span<T> values,
        Span<double> scaledWeights)
    {
        int valueIndex = 0;
        double totalMass = 0d;
        for (int sourceOrdinal = 0; sourceOrdinal < entries.Length; sourceOrdinal++)
        {
            WeightedEntry<T> entry = entries[sourceOrdinal];
            if (entry.Weight == 0d)
            {
                continue;
            }

            if (!WeightedSelectionMath.TryScale(entry.Weight, maximumWeight, out double scaledWeight))
            {
                throw new ArgumentException(
                    $"The weight at ordinal {sourceOrdinal} cannot form a finite positive selection interval.",
                    parameterName);
            }

            if (!WeightedSelectionMath.TryAddMass(totalMass, scaledWeight, out totalMass))
            {
                throw new ArgumentException(
                    $"The weight at ordinal {sourceOrdinal} cannot advance the cumulative selection mass.",
                    parameterName);
            }

            values[valueIndex] = entry.Value!;
            scaledWeights[valueIndex] = scaledWeight;
            valueIndex++;
        }

        return totalMass;
    }
}
