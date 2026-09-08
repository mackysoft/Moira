using System;
using System.Collections.Generic;
using Xunit;

namespace MackySoft.Moira.Tests;

public sealed class WeightedBagDefinitionContractTests
{
    public static TheoryData<WeightedBagEntry<int>[]> InvalidEntrySets => new()
    {
        Array.Empty<WeightedBagEntry<int>>(),
        new[] { new WeightedBagEntry<int>(1, 0d, 1) },
        new[] { new WeightedBagEntry<int>(1, -1d, 1) },
        new[] { new WeightedBagEntry<int>(1, double.NaN, 1) },
        new[] { new WeightedBagEntry<int>(1, double.PositiveInfinity, 1) },
        new[] { new WeightedBagEntry<int>(1, double.NegativeInfinity, 1) },
        new[] { new WeightedBagEntry<int>(1, 1d, 0) },
        new[] { new WeightedBagEntry<int>(1, 1d, -1) },
        new[] { new WeightedBagEntry<int>(1, double.Epsilon, 1), new WeightedBagEntry<int>(2, double.MaxValue, 1) },
        new[] { new WeightedBagEntry<int>(1, 1d, 1), new WeightedBagEntry<int>(2, double.Epsilon, 1) },
    };

    [Theory]
    [MemberData(nameof(InvalidEntrySets))]
    public void Constructor_rejects_invalid_or_unrepresentable_groups (WeightedBagEntry<int>[] entries)
    {
        Assert.Throws<ArgumentException>(() => new WeightedBagDefinition<int>(entries));
    }

    [Fact]
    public void Constructor_rejects_a_unit_weight_that_cannot_advance_the_scaled_unit_total ()
    {
        WeightedBagEntry<int>[] entries =
        [
            new WeightedBagEntry<int>(1, 1d, 1),
            new WeightedBagEntry<int>(2, Math.ScaleB(1d, -54), int.MaxValue),
        ];

        Assert.Throws<ArgumentException>(() => new WeightedBagDefinition<int>(entries));
    }

    [Fact]
    public void Constructor_owns_groups_and_quantity_is_one_dense_group ()
    {
        WeightedBagEntry<string>[] entries =
        [
            new WeightedBagEntry<string>("original", 1d, 1000),
        ];
        WeightedBagDefinition<string> definition = new(entries);
        entries[0] = new WeightedBagEntry<string>("changed", 1d, 1);
        WeightedBag<string> bag = definition.CreateBag();

        Assert.Equal(1, definition.GroupCount);
        Assert.Equal(1000L, definition.InitialItemCount);
        Assert.Equal(new[] { 1000 }, bag.CaptureRemainingCounts());
        Assert.Equal("original", bag.Draw(0d));
    }

    [Fact]
    public void Factory_transforms_each_source_once_in_order_and_rejects_invalid_input ()
    {
        int[] source = [4, 5, 6];
        List<int> selectedSource = [];
        WeightedBagDefinition<int> definition = WeightedBagDefinition<int>.Create(
            source,
            value =>
            {
                selectedSource.Add(value);
                return new WeightedBagEntry<int>(value, 1d, 1);
            });

        Assert.Equal(source, selectedSource);
        source[1] = 99;
        Assert.Equal(5, definition.CreateBag().Draw(0.5d));

        Func<int, WeightedBagEntry<int>> nullSelector = null!;
        Assert.Throws<ArgumentNullException>(() => WeightedBagDefinition<int>.Create(source, nullSelector));
        Assert.Throws<InvalidOperationException>(() => WeightedBagDefinition<int>.Create(
            source,
            value => value == 99
                ? throw new InvalidOperationException()
                : new WeightedBagEntry<int>(value, 1d, 1)));
        Assert.Throws<ArgumentException>(() => WeightedBagDefinition<int>.Create(
            source,
            value => new WeightedBagEntry<int>(value, value == 99 ? 0d : 1d, 1)));
    }
}
