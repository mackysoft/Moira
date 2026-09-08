using System;
using System.Diagnostics.CodeAnalysis;

namespace MackySoft.Moira;

/// <summary>
/// Represents an immutable, source-ordered definition for independent weighted bags.
/// </summary>
/// <typeparam name="T">The type of value drawn from bags created by this definition.</typeparam>
public sealed class WeightedBagDefinition<T>
{
    private readonly WeightedBagEntry<T>[] entries;
    private readonly double[] scaledUnitWeights;

    /// <summary>
    /// Initializes an immutable bag definition from the supplied value groups.
    /// </summary>
    /// <param name="entries">The source-ordered value groups to copy into the definition.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="entries" /> is empty, contains an invalid group, or cannot represent every selection interval.
    /// </exception>
    public WeightedBagDefinition (ReadOnlySpan<WeightedBagEntry<T>> entries)
    {
        WeightedBagEntry<T>[] copiedEntries = new WeightedBagEntry<T>[entries.Length];
        entries.CopyTo(copiedEntries);

        CreateValidated(copiedEntries, nameof(entries), out scaledUnitWeights, out long initialItemCount);
        this.entries = copiedEntries;
        InitialItemCount = initialItemCount;
    }

    private WeightedBagDefinition (
        WeightedBagEntry<T>[] entries,
        double[] scaledUnitWeights,
        long initialItemCount)
    {
        this.entries = entries;
        this.scaledUnitWeights = scaledUnitWeights;
        InitialItemCount = initialItemCount;
    }

    /// <summary>
    /// Creates an immutable bag definition by transforming each source item in source order.
    /// </summary>
    /// <typeparam name="TSource">The type of a source item.</typeparam>
    /// <param name="source">The source items to transform.</param>
    /// <param name="entrySelector">Transforms one source item into a bag entry.</param>
    /// <returns>A bag definition that owns copies of the selected entries.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entrySelector" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="source" /> is empty, selects an invalid group, or cannot represent every selection interval.
    /// </exception>
    public static WeightedBagDefinition<T> Create<TSource> (
        ReadOnlySpan<TSource> source,
        Func<TSource, WeightedBagEntry<T>> entrySelector)
    {
        if (entrySelector is null)
        {
            throw new ArgumentNullException(nameof(entrySelector));
        }

        WeightedBagEntry<T>[] entries = new WeightedBagEntry<T>[source.Length];
        for (int ordinal = 0; ordinal < source.Length; ordinal++)
        {
            entries[ordinal] = entrySelector(source[ordinal]);
        }

        CreateValidated(entries, nameof(source), out double[] scaledUnitWeights, out long initialItemCount);
        return new WeightedBagDefinition<T>(entries, scaledUnitWeights, initialItemCount);
    }

    /// <summary>
    /// Gets the number of source-ordered value groups.
    /// </summary>
    public int GroupCount => entries.Length;

    /// <summary>
    /// Gets the total initial item count across all groups.
    /// </summary>
    public long InitialItemCount { get; }

    /// <summary>
    /// Creates a new independent bag with every group at its initial count.
    /// </summary>
    /// <returns>A new bag that references this definition and owns its remaining counts.</returns>
    public WeightedBag<T> CreateBag ()
    {
        int[] remainingCounts = new int[entries.Length];
        for (int ordinal = 0; ordinal < entries.Length; ordinal++)
        {
            remainingCounts[ordinal] = entries[ordinal].InitialCount;
        }

        return new WeightedBag<T>(this, remainingCounts, InitialItemCount);
    }

    /// <summary>
    /// Creates a new bag from a source-ordered snapshot of remaining counts.
    /// </summary>
    /// <param name="remainingCounts">The counts to snapshot and validate against this definition.</param>
    /// <returns>A new bag with the restored remaining counts.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="remainingCounts" /> does not match this definition or cannot represent every active selection interval.
    /// </exception>
    public WeightedBag<T> Restore (ReadOnlySpan<int> remainingCounts)
    {
        if (remainingCounts.Length != entries.Length)
        {
            throw new ArgumentException("The remaining count length must match the group count.", nameof(remainingCounts));
        }

        int[] snapshot = new int[remainingCounts.Length];
        remainingCounts.CopyTo(snapshot);
        long remainingItemCount = ValidateRemainingCounts(snapshot, nameof(remainingCounts));
        return new WeightedBag<T>(this, snapshot, remainingItemCount);
    }

    internal double GetScaledUnitWeight (int ordinal)
    {
        return scaledUnitWeights[ordinal];
    }

    [return: MaybeNull]
    internal T GetValue (int ordinal)
    {
        return entries[ordinal].Value;
    }

    private static void CreateValidated (
        WeightedBagEntry<T>[] entries,
        string parameterName,
        out double[] scaledUnitWeights,
        out long initialItemCount)
    {
        if (entries.Length == 0)
        {
            throw new ArgumentException("At least one value group is required.", parameterName);
        }

        double maximumWeight = ValidateEntries(entries, parameterName, out initialItemCount);
        scaledUnitWeights = BuildScaledUnitWeights(entries, maximumWeight, parameterName);
    }

    private static double ValidateEntries (
        WeightedBagEntry<T>[] entries,
        string parameterName,
        out long initialItemCount)
    {
        double maximumWeight = 0d;
        initialItemCount = 0L;
        for (int ordinal = 0; ordinal < entries.Length; ordinal++)
        {
            WeightedBagEntry<T> entry = entries[ordinal];
            if (!WeightedSelectionMath.IsFinitePositive(entry.UnitWeight))
            {
                throw new ArgumentException($"The unit weight at ordinal {ordinal} must be finite and positive.", parameterName);
            }

            if (entry.InitialCount <= 0)
            {
                throw new ArgumentException($"The initial count at ordinal {ordinal} must be positive.", parameterName);
            }

            try
            {
                initialItemCount = checked(initialItemCount + entry.InitialCount);
            }
            catch (OverflowException exception)
            {
                throw new ArgumentException("The initial item count cannot be represented as an Int64 value.", parameterName, exception);
            }

            if (entry.UnitWeight > maximumWeight)
            {
                maximumWeight = entry.UnitWeight;
            }
        }

        return maximumWeight;
    }

    private static double[] BuildScaledUnitWeights (
        WeightedBagEntry<T>[] entries,
        double maximumWeight,
        string parameterName)
    {
        double[] scaledUnitWeights = new double[entries.Length];
        double initialUnitTotal = 0d;
        double initialTotalMass = 0d;
        for (int ordinal = 0; ordinal < entries.Length; ordinal++)
        {
            WeightedBagEntry<T> entry = entries[ordinal];
            if (!WeightedSelectionMath.TryScale(entry.UnitWeight, maximumWeight, out double scaledUnitWeight))
            {
                throw new ArgumentException($"The unit weight at ordinal {ordinal} cannot form a finite positive selection interval.", parameterName);
            }

            if (!WeightedSelectionMath.TryAddMass(initialUnitTotal, scaledUnitWeight, out initialUnitTotal))
            {
                throw new ArgumentException($"The unit weight at ordinal {ordinal} cannot advance the initial unit selection mass.", parameterName);
            }

            if (!WeightedSelectionMath.TryCreateMass(scaledUnitWeight, entry.InitialCount, out double mass)
                || !WeightedSelectionMath.TryAddMass(initialTotalMass, mass, out initialTotalMass))
            {
                throw new ArgumentException($"The value group at ordinal {ordinal} cannot advance the initial selection mass.", parameterName);
            }

            scaledUnitWeights[ordinal] = scaledUnitWeight;
        }

        return scaledUnitWeights;
    }

    private long ValidateRemainingCounts (int[] remainingCounts, string parameterName)
    {
        long remainingItemCount = 0L;
        double totalMass = 0d;
        for (int ordinal = 0; ordinal < remainingCounts.Length; ordinal++)
        {
            int remainingCount = remainingCounts[ordinal];
            int initialCount = entries[ordinal].InitialCount;
            if (remainingCount < 0 || remainingCount > initialCount)
            {
                throw new ArgumentException($"The remaining count at ordinal {ordinal} must be within the initial count range.", parameterName);
            }

            try
            {
                remainingItemCount = checked(remainingItemCount + remainingCount);
            }
            catch (OverflowException exception)
            {
                throw new ArgumentException("The remaining item count cannot be represented as an Int64 value.", parameterName, exception);
            }

            if (remainingCount == 0)
            {
                continue;
            }

            if (!WeightedSelectionMath.TryCreateMass(scaledUnitWeights[ordinal], remainingCount, out double mass)
                || !WeightedSelectionMath.TryAddMass(totalMass, mass, out totalMass))
            {
                throw new ArgumentException($"The remaining count at ordinal {ordinal} cannot form a valid selection interval.", parameterName);
            }
        }

        return remainingItemCount;
    }
}
