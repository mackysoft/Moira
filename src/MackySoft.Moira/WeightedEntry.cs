using System.Diagnostics.CodeAnalysis;

namespace MackySoft.Moira;

/// <summary>
/// Represents one unvalidated value and weight supplied to a weighted distribution.
/// </summary>
/// <typeparam name="T">The type of value selected from the distribution.</typeparam>
public readonly struct WeightedEntry<T>
{
    /// <summary>
    /// Initializes an entry without validating its value or weight.
    /// </summary>
    /// <param name="value">The value represented by this entry. <see langword="null" /> is permitted.</param>
    /// <param name="weight">The weight supplied to the distribution.</param>
    public WeightedEntry (T value, double weight)
    {
        Value = value;
        Weight = weight;
    }

    /// <summary>
    /// Gets the value represented by this entry. The value can be <see langword="null" />.
    /// </summary>
    [MaybeNull]
    public T Value { get; }

    /// <summary>
    /// Gets the unvalidated weight for this entry.
    /// </summary>
    public double Weight { get; }
}
