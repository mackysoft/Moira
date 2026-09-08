using System;
using System.Diagnostics.CodeAnalysis;

namespace MackySoft.Moira;

/// <summary>
/// Represents the mutable remaining inventory of one weighted bag definition.
/// </summary>
/// <typeparam name="T">The type of value drawn from this bag.</typeparam>
public sealed class WeightedBag<T>
{
    private readonly WeightedBagDefinition<T> definition;
    private readonly int[] remainingCounts;
    private long remainingItemCount;

    internal WeightedBag (WeightedBagDefinition<T> definition, int[] remainingCounts, long remainingItemCount)
    {
        this.definition = definition;
        this.remainingCounts = remainingCounts;
        this.remainingItemCount = remainingItemCount;
    }

    /// <summary>
    /// Gets the total number of items remaining across all groups.
    /// </summary>
    public long RemainingItemCount => remainingItemCount;

    /// <summary>
    /// Gets a transient read-only view of the remaining counts in definition ordinal order.
    /// </summary>
    /// <remarks>
    /// The view references bag-owned state and does not allocate a snapshot. A successful draw can change values observed
    /// through the view. Copy the view or use <see cref="CaptureRemainingCounts" /> when a stable caller-owned snapshot is required.
    /// </remarks>
    public ReadOnlySpan<int> RemainingCounts => remainingCounts;

    /// <summary>
    /// Attempts to draw one value using a finite sample in the range <c>[0, 1)</c>.
    /// </summary>
    /// <param name="sample">The sample used to select a value when the bag is not empty.</param>
    /// <param name="value">The drawn value, or the default value when the bag is empty.</param>
    /// <returns><see langword="true" /> when a value was drawn; otherwise, <see langword="false" />.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The bag is not empty and <paramref name="sample" /> is not finite or is outside <c>[0, 1)</c>.
    /// </exception>
    public bool TryDraw (double sample, [MaybeNull] out T value)
    {
        if (remainingItemCount == 0L)
        {
            value = default;
            return false;
        }

        value = DrawNonEmpty(sample);
        return true;
    }

    /// <summary>
    /// Attempts to draw one value per sample in order until every sample is consumed or the bag becomes empty.
    /// </summary>
    /// <param name="samples">Samples to consume in order while the bag contains items.</param>
    /// <param name="destination">
    /// Caller-owned storage with the same length as <paramref name="samples" />. Written values can be <see langword="null" />.
    /// </param>
    /// <returns>
    /// The number of values written to the destination prefix. The remaining destination elements are unchanged.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="destination" /> does not have the same length as <paramref name="samples" />.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A sample that would be consumed is not finite or is outside <c>[0, 1)</c>.
    /// </exception>
    /// <remarks>
    /// Samples beyond the bag's remaining item count are not validated or consumed. Every consumed sample is validated
    /// before the bag or destination is changed. The two spans must not overlap.
    /// </remarks>
    public int TryDrawMany (ReadOnlySpan<double> samples, Span<T> destination)
    {
        ValidateBatchLength(samples, destination);

        int drawCount = remainingItemCount < samples.Length
            ? (int)remainingItemCount
            : samples.Length;
        ReadOnlySpan<double> consumedSamples = samples.Slice(0, drawCount);
        WeightedSelectionMath.ValidateSamples(consumedSamples);

        for (int ordinal = 0; ordinal < drawCount; ordinal++)
        {
            destination[ordinal] = DrawValidated(consumedSamples[ordinal])!;
        }

        return drawCount;
    }

    /// <summary>
    /// Draws one value using a finite sample in the range <c>[0, 1)</c>.
    /// </summary>
    /// <param name="sample">The sample used to select a value.</param>
    /// <returns>The drawn value, which can be <see langword="null" />.</returns>
    /// <exception cref="InvalidOperationException">The bag is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sample" /> is not finite or is outside <c>[0, 1)</c>.</exception>
    [return: MaybeNull]
    public T Draw (double sample)
    {
        if (remainingItemCount == 0L)
        {
            throw new InvalidOperationException("Cannot draw from an empty weighted bag.");
        }

        return DrawNonEmpty(sample);
    }

    /// <summary>
    /// Draws one value per sample in order and writes the values in draw order.
    /// </summary>
    /// <param name="samples">Finite samples in the range <c>[0, 1)</c>.</param>
    /// <param name="destination">
    /// Caller-owned storage with the same length as <paramref name="samples" />. Written values can be <see langword="null" />.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="destination" /> does not have the same length as <paramref name="samples" />.
    /// </exception>
    /// <exception cref="InvalidOperationException">The bag contains fewer items than the number of supplied samples.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An element of <paramref name="samples" /> is not finite or is outside <c>[0, 1)</c>.
    /// </exception>
    /// <remarks>
    /// The remaining counts after each draw determine the next draw's probabilities. Item availability and every sample
    /// are validated before the bag or destination is changed. The two spans must not overlap.
    /// </remarks>
    public void DrawMany (ReadOnlySpan<double> samples, Span<T> destination)
    {
        ValidateBatchLength(samples, destination);
        if (remainingItemCount < samples.Length)
        {
            throw new InvalidOperationException("The weighted bag does not contain enough items for every sample.");
        }

        WeightedSelectionMath.ValidateSamples(samples);
        for (int ordinal = 0; ordinal < samples.Length; ordinal++)
        {
            destination[ordinal] = DrawValidated(samples[ordinal])!;
        }
    }

    /// <summary>
    /// Captures the remaining counts in definition ordinal order.
    /// </summary>
    /// <returns>A new caller-owned array with one remaining count per definition group.</returns>
    public int[] CaptureRemainingCounts ()
    {
        int[] snapshot = new int[remainingCounts.Length];
        RemainingCounts.CopyTo(snapshot);
        return snapshot;
    }

    [return: MaybeNull]
    private T DrawNonEmpty (double sample)
    {
        WeightedSelectionMath.ValidateSample(sample);

        return DrawValidated(sample);
    }

    [return: MaybeNull]
    private T DrawValidated (double sample)
    {
        double totalMass = CalculateTotalMass();
        double target = WeightedSelectionMath.CorrectTarget(sample, totalMass);
        int selectedOrdinal = FindSelectedOrdinal(target);
        remainingCounts[selectedOrdinal]--;
        remainingItemCount--;
        return definition.GetValue(selectedOrdinal);
    }

    private static void ValidateBatchLength (ReadOnlySpan<double> samples, Span<T> destination)
    {
        if (destination.Length != samples.Length)
        {
            throw new ArgumentException("The destination length must match the sample length.", nameof(destination));
        }
    }

    private double CalculateTotalMass ()
    {
        double totalMass = 0d;
        for (int ordinal = 0; ordinal < remainingCounts.Length; ordinal++)
        {
            int remainingCount = remainingCounts[ordinal];
            if (remainingCount == 0)
            {
                continue;
            }

            if (!WeightedSelectionMath.TryCreateMass(definition.GetScaledUnitWeight(ordinal), remainingCount, out double mass)
                || !WeightedSelectionMath.TryAddMass(totalMass, mass, out totalMass))
            {
                throw new InvalidOperationException("The weighted bag cannot represent its current selection mass.");
            }
        }

        return totalMass;
    }

    private int FindSelectedOrdinal (double target)
    {
        double cumulativeMass = 0d;
        for (int ordinal = 0; ordinal < remainingCounts.Length; ordinal++)
        {
            int remainingCount = remainingCounts[ordinal];
            if (remainingCount == 0)
            {
                continue;
            }

            if (!WeightedSelectionMath.TryCreateMass(definition.GetScaledUnitWeight(ordinal), remainingCount, out double mass)
                || !WeightedSelectionMath.TryAddMass(cumulativeMass, mass, out cumulativeMass))
            {
                throw new InvalidOperationException("The weighted bag cannot represent its current selection mass.");
            }

            if (cumulativeMass > target)
            {
                return ordinal;
            }
        }

        throw new InvalidOperationException("The weighted bag did not contain a selectable value.");
    }
}
