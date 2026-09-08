using System;
using System.Diagnostics.CodeAnalysis;

namespace MackySoft.Moira;

/// <summary>
/// Represents an immutable, source-ordered distribution that selects values by their weights.
/// </summary>
/// <typeparam name="T">The type of value selected from the distribution.</typeparam>
public sealed class WeightedDistribution<T>
{
    private readonly T[] selectableValues;
    private readonly int sourceEntryCount;
    private readonly CompiledWeightedSelectionPlan weightedSelectionPlan;

    /// <summary>
    /// Initializes an immutable distribution from the supplied entries.
    /// </summary>
    /// <param name="entries">The source-ordered entries whose selectable values and weights are compiled into the distribution.</param>
    /// <param name="method">The method used to compile and execute this distribution.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="entries" /> is empty, contains an invalid weight, or cannot represent every selection interval.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="method" /> is not supported.</exception>
    public WeightedDistribution (
        ReadOnlySpan<WeightedEntry<T>> entries,
        WeightedSelectionMethod method)
        : this(CreateCanonicalTable(entries, method, nameof(entries)), method, nameof(entries))
    {
    }

    private WeightedDistribution (
        CanonicalWeightedTable<T> table,
        WeightedSelectionMethod method,
        string parameterName)
    {
        selectableValues = table.Values;
        sourceEntryCount = table.SourceEntryCount;
        weightedSelectionPlan = CompiledWeightedSelectionPlan.CreateForValidatedMethod(
            table.ScaledWeights,
            table.TotalMass,
            method,
            parameterName);
    }

    /// <summary>
    /// Creates an immutable distribution by transforming each source item in source order.
    /// </summary>
    /// <typeparam name="TSource">The type of a source item.</typeparam>
    /// <param name="source">The source items to transform.</param>
    /// <param name="method">The method used to compile and execute the distribution.</param>
    /// <param name="entrySelector">Transforms one source item into an entry.</param>
    /// <returns>A distribution whose compiled state is independent of the source storage.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entrySelector" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="source" /> is empty, selects an invalid weight, or cannot represent every selection interval.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="method" /> is not supported.</exception>
    public static WeightedDistribution<T> Create<TSource> (
        ReadOnlySpan<TSource> source,
        WeightedSelectionMethod method,
        Func<TSource, WeightedEntry<T>> entrySelector)
    {
        CompiledWeightedSelectionPlan.ValidateMethod(method, nameof(method));

        if (entrySelector is null)
        {
            throw new ArgumentNullException(nameof(entrySelector));
        }

        WeightedEntry<T>[] entries = new WeightedEntry<T>[source.Length];
        for (int ordinal = 0; ordinal < source.Length; ordinal++)
        {
            entries[ordinal] = entrySelector(source[ordinal]);
        }

        CanonicalWeightedTable<T> table = CanonicalWeightedTable<T>.Create(entries, nameof(source));
        return new WeightedDistribution<T>(table, method, nameof(source));
    }

    /// <summary>
    /// Gets the method compiled for this distribution.
    /// </summary>
    public WeightedSelectionMethod Method => weightedSelectionPlan.Method;

    /// <summary>
    /// Gets the number of source entries, including zero-weight entries.
    /// </summary>
    public int SourceEntryCount => sourceEntryCount;

    /// <summary>
    /// Selects a source-ordered value through this distribution's compiled weighted selection plan.
    /// </summary>
    /// <param name="sample">A finite sample in the range <c>[0, 1)</c>.</param>
    /// <returns>The selected value, which can be <see langword="null" />.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sample" /> is not finite or is outside <c>[0, 1)</c>.</exception>
    /// <remarks>
    /// Within one package version, identical methods, source order, weights, and sample bit patterns select the same
    /// source ordinal. Different methods can map the same sample to different source ordinals.
    /// </remarks>
    [return: MaybeNull]
    public T Select (double sample)
    {
        WeightedSelectionMath.ValidateSample(sample);
        return selectableValues[weightedSelectionPlan.SelectValueIndex(sample)];
    }

    /// <summary>
    /// Selects one source-ordered value for each supplied sample and writes the values in sample order.
    /// </summary>
    /// <param name="samples">Finite samples in the range <c>[0, 1)</c>.</param>
    /// <param name="destination">
    /// Caller-owned storage with the same length as <paramref name="samples" />. Written values can be <see langword="null" />.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="destination" /> does not have the same length as <paramref name="samples" />.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An element of <paramref name="samples" /> is not finite or is outside <c>[0, 1)</c>.
    /// </exception>
    /// <remarks>
    /// All samples are validated before the destination is written. The two spans must not overlap.
    /// </remarks>
    public void SelectMany (ReadOnlySpan<double> samples, Span<T> destination)
    {
        ValidateBatchLength(samples, destination);
        WeightedSelectionMath.ValidateSamples(samples);
        weightedSelectionPlan.SelectMany(samples, selectableValues, destination);
    }

    private static CanonicalWeightedTable<T> CreateCanonicalTable (
        ReadOnlySpan<WeightedEntry<T>> entries,
        WeightedSelectionMethod method,
        string parameterName)
    {
        CompiledWeightedSelectionPlan.ValidateMethod(method, nameof(method));
        return CanonicalWeightedTable<T>.Create(entries, parameterName);
    }

    private static void ValidateBatchLength (ReadOnlySpan<double> samples, Span<T> destination)
    {
        if (destination.Length != samples.Length)
        {
            throw new ArgumentException("The destination length must match the sample length.", nameof(destination));
        }
    }
}
