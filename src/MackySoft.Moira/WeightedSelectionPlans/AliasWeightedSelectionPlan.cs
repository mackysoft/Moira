using System;

namespace MackySoft.Moira;

/// <summary>
/// Stores and executes the compiled alias plan for one fixed weighted distribution.
/// </summary>
internal readonly struct AliasWeightedSelectionPlan
{
    private readonly double[] cutoffs;
    private readonly int[] aliases;

    private AliasWeightedSelectionPlan (double[] cutoffs, int[] aliases)
    {
        this.cutoffs = cutoffs;
        this.aliases = aliases;
    }

    internal static AliasWeightedSelectionPlan Create (
        double[] scaledWeights,
        double totalMass,
        string parameterName)
    {
        NormalizeProbabilities(scaledWeights, totalMass, parameterName);

        int[] aliases = new int[scaledWeights.Length];
        CompileAliasTable(scaledWeights, aliases);
        return new AliasWeightedSelectionPlan(scaledWeights, aliases);
    }

    internal int SelectValueIndex (double sample)
    {
        WeightedSelectionMath.SplitSample(
            sample,
            cutoffs.Length,
            out int column,
            out double columnSample);

        int selectedColumn = columnSample < cutoffs[column]
            ? column
            : aliases[column];
        return selectedColumn;
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

    private static void NormalizeProbabilities (
        Span<double> scaledProbabilities,
        double totalMass,
        string parameterName)
    {
        double columnCount = scaledProbabilities.Length;
        for (int column = 0; column < scaledProbabilities.Length; column++)
        {
            double normalizedProbability = (scaledProbabilities[column] * columnCount) / totalMass;
            if (!WeightedSelectionMath.IsFinitePositive(normalizedProbability)
                || normalizedProbability > columnCount)
            {
                throw new ArgumentException(
                    "The weights cannot form a valid alias probability table.",
                    parameterName);
            }

            scaledProbabilities[column] = normalizedProbability;
        }
    }

    private static void CompileAliasTable (
        Span<double> probabilitiesAndCutoffs,
        Span<int> aliases)
    {
        Span<double> columnStates = probabilitiesAndCutoffs;
        int[] smallerColumns = new int[columnStates.Length];
        int[] largerColumns = new int[columnStates.Length];
        ClassifyColumns(columnStates, smallerColumns, largerColumns, out int smallerCount, out int largerCount);

        // A resolved column's working probability is its final cutoff; only the paired larger column remains mutable.
        while (smallerCount > 0 && largerCount > 0)
        {
            int smallerColumn = smallerColumns[--smallerCount];
            int largerColumn = largerColumns[--largerCount];
            aliases[smallerColumn] = largerColumn;

            double remainingProbability =
                (columnStates[largerColumn] + columnStates[smallerColumn]) - 1d;
            columnStates[largerColumn] = remainingProbability;
            if (remainingProbability < 1d)
            {
                smallerColumns[smallerCount++] = largerColumn;
            }
            else
            {
                largerColumns[largerCount++] = largerColumn;
            }
        }

        CompleteColumns(largerColumns, largerCount, columnStates, aliases);
        CompleteColumns(smallerColumns, smallerCount, columnStates, aliases);
    }

    private static void ClassifyColumns (
        ReadOnlySpan<double> scaledProbabilities,
        Span<int> smallerColumns,
        Span<int> largerColumns,
        out int smallerCount,
        out int largerCount)
    {
        smallerCount = 0;
        largerCount = 0;
        for (int column = 0; column < scaledProbabilities.Length; column++)
        {
            if (scaledProbabilities[column] < 1d)
            {
                smallerColumns[smallerCount++] = column;
            }
            else
            {
                largerColumns[largerCount++] = column;
            }
        }
    }

    private static void CompleteColumns (
        ReadOnlySpan<int> columns,
        int count,
        Span<double> cutoffs,
        Span<int> aliases)
    {
        for (int ordinal = 0; ordinal < count; ordinal++)
        {
            int column = columns[ordinal];
            cutoffs[column] = 1d;
            aliases[column] = column;
        }
    }
}
