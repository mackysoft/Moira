using System;

namespace MackySoft.Moira;

/// <summary>
/// Owns one concrete selection plan and dispatches outside batch selection loops.
/// </summary>
internal readonly struct CompiledWeightedSelectionPlan
{
    private readonly WeightedSelectionMethod method;
    private readonly CumulativeWeightedSelectionPlan cumulativePlan;
    private readonly AliasWeightedSelectionPlan aliasPlan;

    private CompiledWeightedSelectionPlan (CumulativeWeightedSelectionPlan cumulativePlan)
    {
        method = WeightedSelectionMethod.Cumulative;
        this.cumulativePlan = cumulativePlan;
        aliasPlan = default;
    }

    private CompiledWeightedSelectionPlan (AliasWeightedSelectionPlan aliasPlan)
    {
        method = WeightedSelectionMethod.Alias;
        cumulativePlan = default;
        this.aliasPlan = aliasPlan;
    }

    internal WeightedSelectionMethod Method => method;

    internal static void ValidateMethod (WeightedSelectionMethod method, string parameterName)
    {
        if (method is WeightedSelectionMethod.Cumulative or WeightedSelectionMethod.Alias)
        {
            return;
        }

        throw new ArgumentOutOfRangeException(parameterName, method, "The weighted selection method is not supported.");
    }

    /// <summary>
    /// Compiles one concrete plan by taking ownership of a validated scaled-weight array.
    /// </summary>
    /// <param name="scaledWeights">
    /// Source-ordered positive scaled weights owned by the caller. The selected plan reuses and mutates this array as
    /// its persistent numeric table.
    /// </param>
    /// <param name="totalMass">The finite positive sum of <paramref name="scaledWeights" />.</param>
    /// <param name="method">A method previously accepted by <see cref="ValidateMethod" />.</param>
    /// <param name="parameterName">The public construction parameter represented by the compiled weights.</param>
    /// <returns>A plan whose value indexes correspond to the supplied weight order.</returns>
    internal static CompiledWeightedSelectionPlan CreateForValidatedMethod (
        double[] scaledWeights,
        double totalMass,
        WeightedSelectionMethod method,
        string parameterName)
    {
        if (method == WeightedSelectionMethod.Cumulative)
        {
            return new CompiledWeightedSelectionPlan(
                CumulativeWeightedSelectionPlan.Create(scaledWeights, totalMass));
        }

        return new CompiledWeightedSelectionPlan(
            AliasWeightedSelectionPlan.Create(scaledWeights, totalMass, parameterName));
    }

    internal int SelectValueIndex (double sample)
    {
        return method == WeightedSelectionMethod.Cumulative
            ? cumulativePlan.SelectValueIndex(sample)
            : aliasPlan.SelectValueIndex(sample);
    }

    internal void SelectMany<T> (
        ReadOnlySpan<double> samples,
        ReadOnlySpan<T> values,
        Span<T> destination)
    {
        if (method == WeightedSelectionMethod.Cumulative)
        {
            cumulativePlan.SelectMany(samples, values, destination);
            return;
        }

        aliasPlan.SelectMany(samples, values, destination);
    }
}
