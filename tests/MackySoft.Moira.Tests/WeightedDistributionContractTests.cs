using System;
using System.Collections.Generic;
using Xunit;

namespace MackySoft.Moira.Tests;

public sealed class WeightedDistributionContractTests
{
    public static TheoryData<WeightedEntry<int>[]> InvalidEntrySets => new()
    {
        Array.Empty<WeightedEntry<int>>(),
        new[] { new WeightedEntry<int>(1, 0d), new WeightedEntry<int>(2, 0d) },
        new[] { new WeightedEntry<int>(1, -1d) },
        new[] { new WeightedEntry<int>(1, double.NaN) },
        new[] { new WeightedEntry<int>(1, double.PositiveInfinity) },
        new[] { new WeightedEntry<int>(1, double.NegativeInfinity) },
        new[] { new WeightedEntry<int>(1, double.Epsilon), new WeightedEntry<int>(2, double.MaxValue) },
        new[] { new WeightedEntry<int>(1, 1d), new WeightedEntry<int>(2, double.Epsilon) },
    };

    public static TheoryData<double> InvalidSamples => new()
    {
        -double.Epsilon,
        1d,
        double.NaN,
        double.PositiveInfinity,
        double.NegativeInfinity,
    };

    [Fact]
    public void Cumulative_select_maps_half_open_intervals_in_source_order_and_is_deterministic ()
    {
        WeightedEntry<string>[] entries =
        [
            new WeightedEntry<string>("zero", 0d),
            new WeightedEntry<string>("first", 2d),
            new WeightedEntry<string>("second", 1d),
            new WeightedEntry<string>("third", 1d),
        ];
        WeightedDistribution<string> distribution = new(entries, WeightedSelectionMethod.Cumulative);

        Assert.Equal(WeightedSelectionMethod.Cumulative, distribution.Method);
        Assert.Equal(4, distribution.SourceEntryCount);
        Assert.Equal("first", distribution.Select(0d));
        Assert.Equal("first", distribution.Select(-0d));
        Assert.Equal("first", distribution.Select(0.49999999999999994d));
        Assert.Equal("second", distribution.Select(0.5d));
        Assert.Equal("second", distribution.Select(0.7499999999999999d));
        Assert.Equal("third", distribution.Select(0.75d));
        Assert.Equal("third", distribution.Select(double.BitDecrement(1d)));
        Assert.Equal(distribution.Select(0.625d), distribution.Select(0.625d));
    }

    [Fact]
    public void Alias_select_maps_one_sample_to_a_column_and_cutoff_deterministically ()
    {
        WeightedDistribution<string> distribution = new(
        [
            new WeightedEntry<string>("leading-zero", 0d),
            new WeightedEntry<string>("frequent", 3d),
            new WeightedEntry<string>("middle-zero", 0d),
            new WeightedEntry<string>("rare", 1d),
        ],
        WeightedSelectionMethod.Alias);

        Assert.Equal(WeightedSelectionMethod.Alias, distribution.Method);
        Assert.Equal(4, distribution.SourceEntryCount);
        Assert.Equal("frequent", distribution.Select(0d));
        Assert.Equal("frequent", distribution.Select(double.BitDecrement(0.5d)));
        Assert.Equal("rare", distribution.Select(0.5d));
        Assert.Equal("rare", distribution.Select(double.BitDecrement(0.75d)));
        Assert.Equal("frequent", distribution.Select(0.75d));
        Assert.Equal("frequent", distribution.Select(double.BitDecrement(1d)));
        Assert.Equal(distribution.Select(0.625d), distribution.Select(0.625d));
    }

    [Fact]
    public void Selection_method_is_part_of_the_sample_mapping ()
    {
        WeightedEntry<string>[] entries =
        [
            new WeightedEntry<string>("frequent", 3d),
            new WeightedEntry<string>("rare", 1d),
        ];
        WeightedDistribution<string> cumulative = new(entries, WeightedSelectionMethod.Cumulative);
        WeightedDistribution<string> alias = new(entries, WeightedSelectionMethod.Alias);

        Assert.Equal("frequent", cumulative.Select(0.6d));
        Assert.Equal("rare", alias.Select(0.6d));
    }

    [Fact]
    public void Alias_selection_measure_matches_source_weights_on_an_exact_partition_vector ()
    {
        WeightedDistribution<int> distribution = new(
        [
            new WeightedEntry<int>(0, 1d),
            new WeightedEntry<int>(1, 2d),
            new WeightedEntry<int>(2, 3d),
        ],
        WeightedSelectionMethod.Alias);
        double[] samples = [1d / 12d, 3d / 12d, 5d / 12d, 7d / 12d, 9d / 12d, 11d / 12d];
        int[] destination = new int[samples.Length];
        int[] selectionCounts = new int[3];

        distribution.SelectMany(samples, destination);
        foreach (int selected in destination)
        {
            selectionCounts[selected]++;
        }

        Assert.Equal(new[] { 1, 2, 3 }, selectionCounts);
    }

    [Fact]
    public void Alias_select_batch_writes_one_value_per_sample_in_sample_order ()
    {
        WeightedDistribution<int> distribution = new(
        [
            new WeightedEntry<int>(0, 0d),
            new WeightedEntry<int>(1, 3d),
            new WeightedEntry<int>(0, 0d),
            new WeightedEntry<int>(2, 1d),
        ],
        WeightedSelectionMethod.Alias);
        ReadOnlySpan<double> samples = [0d, 0.5d, double.BitDecrement(0.75d), 0.75d, double.BitDecrement(1d)];
        Span<int> destination = stackalloc int[samples.Length];

        distribution.SelectMany(samples, destination);

        Assert.Equal(1, destination[0]);
        Assert.Equal(2, destination[1]);
        Assert.Equal(2, destination[2]);
        Assert.Equal(1, destination[3]);
        Assert.Equal(1, destination[4]);
    }

    [Fact]
    public void Select_batch_rejects_invalid_input_before_writing_the_destination ()
    {
        foreach (WeightedSelectionMethod method in Enum.GetValues<WeightedSelectionMethod>())
        {
            WeightedDistribution<string> distribution = new([new WeightedEntry<string>("selected", 1d)], method);
            double[] invalidSamples = [0d, double.NaN];
            string[] invalidDestination = ["first", "second"];
            string[] mismatchedDestination = ["unchanged"];

            Assert.Throws<ArgumentOutOfRangeException>(() => distribution.SelectMany(invalidSamples, invalidDestination));
            Assert.Equal(new[] { "first", "second" }, invalidDestination);
            Assert.Throws<ArgumentException>(() => distribution.SelectMany(invalidSamples, mismatchedDestination));
            Assert.Equal(new[] { "unchanged" }, mismatchedDestination);
        }
    }

    [Fact]
    public void Maximum_valid_sample_never_selects_a_trailing_zero_weight_entry ()
    {
        foreach (WeightedSelectionMethod method in Enum.GetValues<WeightedSelectionMethod>())
        {
            WeightedDistribution<string> distribution = new(
            [
                new WeightedEntry<string>("small", double.Epsilon),
                new WeightedEntry<string>("last-positive", 1d),
                new WeightedEntry<string>("trailing-zero", 0d),
            ],
            method);

            Assert.Equal("last-positive", distribution.Select(double.BitDecrement(1d)));
        }
    }

    [Theory]
    [MemberData(nameof(InvalidEntrySets))]
    public void Constructor_rejects_invalid_or_unrepresentable_entry_sets (WeightedEntry<int>[] entries)
    {
        foreach (WeightedSelectionMethod method in Enum.GetValues<WeightedSelectionMethod>())
        {
            Assert.Throws<ArgumentException>(() => new WeightedDistribution<int>(entries, method));
        }
    }

    [Fact]
    public void Construction_rejects_an_unsupported_selection_method ()
    {
        WeightedEntry<int>[] entries = [new WeightedEntry<int>(1, 1d)];
        WeightedSelectionMethod unsupported = (WeightedSelectionMethod)byte.MaxValue;

        Assert.Throws<ArgumentOutOfRangeException>(() => new WeightedDistribution<int>(entries, unsupported));
        Assert.Throws<ArgumentOutOfRangeException>(() => WeightedDistribution<int>.Create(
            entries,
            unsupported,
            entry => entry));
    }

    [Theory]
    [MemberData(nameof(InvalidSamples))]
    public void Select_rejects_samples_outside_the_finite_half_open_range (double sample)
    {
        foreach (WeightedSelectionMethod method in Enum.GetValues<WeightedSelectionMethod>())
        {
            WeightedDistribution<int> distribution = new([new WeightedEntry<int>(1, 1d)], method);

            Assert.Throws<ArgumentOutOfRangeException>(() => distribution.Select(sample));
        }
    }

    [Fact]
    public void Constructor_owns_entries_without_constraining_value_identity_or_null ()
    {
        AlwaysEqual first = new();
        AlwaysEqual second = new();
        WeightedEntry<AlwaysEqual?>[] entries =
        [
            new WeightedEntry<AlwaysEqual?>(first, 1d),
            new WeightedEntry<AlwaysEqual?>(first, 1d),
            new WeightedEntry<AlwaysEqual?>(second, 1d),
            new WeightedEntry<AlwaysEqual?>(null, 1d),
        ];
        WeightedDistribution<AlwaysEqual?> distribution = new(entries, WeightedSelectionMethod.Alias);
        entries[0] = new WeightedEntry<AlwaysEqual?>(null, 1d);
        entries[1] = new WeightedEntry<AlwaysEqual?>(first, 100d);

        Assert.Equal(4, distribution.SourceEntryCount);
        Assert.Same(first, distribution.Select(0d));
        Assert.Same(second, distribution.Select(0.5d));
        Assert.Null(distribution.Select(0.75d));
    }

    [Fact]
    public void Factory_transforms_each_source_once_in_order_and_applies_constructor_validation ()
    {
        int[] source = [4, 5, 6];
        List<int> selectedSource = [];
        WeightedDistribution<int> distribution = WeightedDistribution<int>.Create(
            source,
            WeightedSelectionMethod.Alias,
            value =>
            {
                selectedSource.Add(value);
                return new WeightedEntry<int>(value, 1d);
            });

        Assert.Equal(source, selectedSource);
        source[1] = 99;
        Assert.Equal(5, distribution.Select(0.5d));

        Func<int, WeightedEntry<int>> nullSelector = null!;
        Assert.Throws<ArgumentNullException>(() => WeightedDistribution<int>.Create(
            source,
            WeightedSelectionMethod.Cumulative,
            nullSelector));
        Assert.Throws<InvalidOperationException>(() => WeightedDistribution<int>.Create(
            source,
            WeightedSelectionMethod.Cumulative,
            value => value == 99
                ? throw new InvalidOperationException()
                : new WeightedEntry<int>(value, 1d)));
        Assert.Throws<ArgumentException>(() => WeightedDistribution<int>.Create(
            source,
            WeightedSelectionMethod.Alias,
            value => new WeightedEntry<int>(value, value == 99 ? -1d : 1d)));
    }

    private sealed class AlwaysEqual
    {
        public override bool Equals (object? obj)
        {
            return obj is AlwaysEqual;
        }

        public override int GetHashCode ()
        {
            return 0;
        }
    }
}
