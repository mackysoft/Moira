namespace MackySoft.Moira;

/// <summary>
/// Specifies how a fixed weighted distribution compiles and executes its selection mapping.
/// </summary>
public enum WeightedSelectionMethod : byte
{
    /// <summary>
    /// Uses a cumulative mass table and binary search for each selection.
    /// </summary>
    /// <remarks>
    /// Compilation and storage are linear in the source entry count. Each selection is logarithmic in that count.
    /// </remarks>
    Cumulative = 0,

    /// <summary>
    /// Uses a precomputed alias table for constant-time selection.
    /// </summary>
    /// <remarks>
    /// Compilation scans the source entries and retains an alias table linear in the selectable entry count. This
    /// method is intended for a fixed distribution that is selected from often enough to amortize table construction.
    /// </remarks>
    Alias = 1,
}
