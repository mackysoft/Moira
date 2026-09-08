using System.Diagnostics.CodeAnalysis;

namespace MackySoft.Moira;

/// <summary>
/// Represents one unvalidated value group supplied to a weighted bag definition.
/// </summary>
/// <typeparam name="T">The type of value drawn from the bag.</typeparam>
public readonly struct WeightedBagEntry<T>
{
    /// <summary>
    /// Initializes an entry without validating its value, unit weight, or initial count.
    /// </summary>
    /// <param name="value">The value represented by this group. <see langword="null" /> is permitted.</param>
    /// <param name="unitWeight">The weight of one remaining item in this group.</param>
    /// <param name="initialCount">The initial number of items in this group.</param>
    public WeightedBagEntry (T value, double unitWeight, int initialCount)
    {
        Value = value;
        UnitWeight = unitWeight;
        InitialCount = initialCount;
    }

    /// <summary>
    /// Gets the value represented by this group. The value can be <see langword="null" />.
    /// </summary>
    [MaybeNull]
    public T Value { get; }

    /// <summary>
    /// Gets the unvalidated weight of one item in this group.
    /// </summary>
    public double UnitWeight { get; }

    /// <summary>
    /// Gets the unvalidated initial number of items in this group.
    /// </summary>
    public int InitialCount { get; }
}
