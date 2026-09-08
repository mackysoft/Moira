using System;
using Xunit;

namespace MackySoft.Moira.Tests;

public sealed class WeightedBagContractTests
{
    public static TheoryData<double> InvalidSamples => new()
    {
        -double.Epsilon,
        1d,
        double.NaN,
        double.PositiveInfinity,
        double.NegativeInfinity,
    };

    [Fact]
    public void Draw_uses_remaining_quantity_for_mass_and_decrements_only_the_selected_group ()
    {
        WeightedBagDefinition<string> definition = new(
        [
            new WeightedBagEntry<string>("first", 2d, 2),
            new WeightedBagEntry<string>("second", 1d, 2),
        ]);
        WeightedBag<string> bag = definition.CreateBag();

        Assert.Equal("first", bag.Draw(0.6d));
        Assert.Equal(3L, bag.RemainingItemCount);
        Assert.Equal(new[] { 1, 2 }, bag.CaptureRemainingCounts());
        Assert.Equal("second", bag.Draw(0.5d));
        Assert.Equal(new[] { 1, 1 }, bag.CaptureRemainingCounts());
    }

    [Fact]
    public void Draw_batch_applies_samples_as_an_ordered_sequence_and_consumes_the_exact_count ()
    {
        WeightedBagDefinition<string> definition = new(
        [
            new WeightedBagEntry<string>("first", 2d, 2),
            new WeightedBagEntry<string>("second", 1d, 2),
        ]);
        WeightedBag<string> bag = definition.CreateBag();
        double[] samples = [0.6d, 0.5d];
        string[] destination = new string[samples.Length];

        bag.DrawMany(samples, destination);

        Assert.Equal(new[] { "first", "second" }, destination);
        Assert.Equal(2L, bag.RemainingItemCount);
        Assert.Equal(new[] { 1, 1 }, bag.CaptureRemainingCounts());
    }

    [Fact]
    public void Draw_batch_rejects_invalid_samples_before_changing_state_or_destination ()
    {
        WeightedBag<int> bag = CreateTwoItemBag();
        int[] destination = [10, 20];

        Assert.Throws<ArgumentOutOfRangeException>(() => bag.DrawMany(new[] { 0d, double.NaN }, destination));

        Assert.Equal(new[] { 10, 20 }, destination);
        AssertHasTwoUndrawnItems(bag);
    }

    [Fact]
    public void Draw_batch_rejects_insufficient_inventory_before_changing_state_or_destination ()
    {
        WeightedBag<int> bag = CreateTwoItemBag();
        int[] destination = [10, 20, 30];

        Assert.Throws<InvalidOperationException>(() => bag.DrawMany(new[] { 0d, 0d, 0d }, destination));

        Assert.Equal(new[] { 10, 20, 30 }, destination);
        AssertHasTwoUndrawnItems(bag);
    }

    [Fact]
    public void Draw_batch_rejects_mismatched_lengths_before_changing_state_or_destination ()
    {
        WeightedBag<int> bag = CreateTwoItemBag();
        int[] destination = [10];

        Assert.Throws<ArgumentException>(() => bag.DrawMany(new[] { 0d, 0d }, destination));

        Assert.Equal(new[] { 10 }, destination);
        AssertHasTwoUndrawnItems(bag);
    }

    [Fact]
    public void Try_draw_batch_rejects_invalid_consumed_samples_before_changing_state_or_destination ()
    {
        WeightedBag<int> bag = CreateTwoItemBag();
        int[] destination = [10, 20];

        Assert.Throws<ArgumentOutOfRangeException>(() => bag.TryDrawMany(new[] { 0d, double.NaN }, destination));

        Assert.Equal(new[] { 10, 20 }, destination);
        AssertHasTwoUndrawnItems(bag);
    }

    [Fact]
    public void Try_draw_batch_writes_the_available_prefix_and_ignores_samples_after_the_bag_becomes_empty ()
    {
        WeightedBagDefinition<string> definition = new(
        [
            new WeightedBagEntry<string>("first", 1d, 1),
            new WeightedBagEntry<string>("second", 1d, 1),
        ]);
        WeightedBag<string> bag = definition.CreateBag();
        double[] samples = [0d, 0d, double.NaN];
        string[] destination = ["unchanged-0", "unchanged-1", "unchanged-2"];

        int drawCount = bag.TryDrawMany(samples, destination);

        Assert.Equal(2, drawCount);
        Assert.Equal(new[] { "first", "second", "unchanged-2" }, destination);
        Assert.Equal(0L, bag.RemainingItemCount);

        string[] emptyDestination = ["still-unchanged"];
        Assert.Equal(0, bag.TryDrawMany(new[] { double.NaN }, emptyDestination));
        Assert.Equal(new[] { "still-unchanged" }, emptyDestination);
    }

    [Fact]
    public void Draw_skips_depleted_groups_and_keeps_equal_or_null_values_as_distinct_ordinals ()
    {
        AlwaysEqual first = new();
        AlwaysEqual second = new();
        WeightedBagDefinition<AlwaysEqual?> definition = new(
        [
            new WeightedBagEntry<AlwaysEqual?>(first, 1d, 1),
            new WeightedBagEntry<AlwaysEqual?>(second, 1d, 1),
            new WeightedBagEntry<AlwaysEqual?>(null, 1d, 1),
        ]);
        WeightedBag<AlwaysEqual?> bag = definition.CreateBag();

        Assert.Equal(3, definition.GroupCount);
        Assert.Same(first, bag.Draw(0d));
        Assert.Equal(new[] { 0, 1, 1 }, bag.CaptureRemainingCounts());
        Assert.Same(second, bag.Draw(0d));
        Assert.True(bag.TryDraw(0d, out AlwaysEqual? value));
        Assert.Null(value);
    }

    [Fact]
    public void Draw_returns_null_and_consumes_one_item_from_a_null_value_group ()
    {
        WeightedBagDefinition<string?> definition = new([new WeightedBagEntry<string?>(null, 1d, 2)]);
        WeightedBag<string?> bag = definition.CreateBag();

        Assert.Null(bag.Draw(0d));
        Assert.Equal(1L, bag.RemainingItemCount);
        Assert.Equal(new[] { 1 }, bag.CaptureRemainingCounts());
    }

    [Fact]
    public void Bags_keep_duplicate_references_and_nulls_as_independent_definition_ordinals ()
    {
        object shared = new();
        WeightedBagDefinition<object?> definition = new(
        [
            new WeightedBagEntry<object?>(shared, 1d, 1),
            new WeightedBagEntry<object?>(shared, 1d, 1),
            new WeightedBagEntry<object?>(null, 1d, 1),
            new WeightedBagEntry<object?>(null, 1d, 1),
        ]);
        WeightedBag<object?> bag = definition.CreateBag();

        Assert.Equal(4, definition.GroupCount);
        Assert.Equal(new[] { 1, 1, 1, 1 }, bag.CaptureRemainingCounts());

        Assert.Same(shared, bag.Draw(0.3d));
        Assert.Equal(new[] { 1, 0, 1, 1 }, bag.CaptureRemainingCounts());
        Assert.True(bag.TryDraw(0.8d, out object? nullValue));
        Assert.Null(nullValue);
        Assert.Equal(new[] { 1, 0, 1, 0 }, bag.CaptureRemainingCounts());

        Assert.Same(shared, bag.Draw(0d));
        Assert.True(bag.TryDraw(0d, out object? lastNullValue));
        Assert.Null(lastNullValue);
        Assert.Equal(new[] { 0, 0, 0, 0 }, bag.CaptureRemainingCounts());
    }

    [Fact]
    public void Empty_bag_distinguishes_failure_from_a_successful_null_draw_and_does_not_validate_samples ()
    {
        WeightedBagDefinition<string?> definition = new([new WeightedBagEntry<string?>(null, 1d, 1)]);
        WeightedBag<string?> bag = definition.CreateBag();

        Assert.True(bag.TryDraw(0d, out string? drawn));
        Assert.Null(drawn);
        Assert.False(bag.TryDraw(double.NaN, out string? emptyValue));
        Assert.Null(emptyValue);
        Assert.Throws<InvalidOperationException>(() => bag.Draw(-1d));
    }

    [Theory]
    [MemberData(nameof(InvalidSamples))]
    public void Invalid_sample_on_nonempty_bag_leaves_try_draw_and_draw_state_unchanged (double sample)
    {
        WeightedBag<int> tryDrawBag = CreateTwoItemBag();
        WeightedBag<int> drawBag = CreateTwoItemBag();

        Assert.Throws<ArgumentOutOfRangeException>(() => tryDrawBag.TryDraw(sample, out _));
        AssertHasTwoUndrawnItems(tryDrawBag);
        Assert.Throws<ArgumentOutOfRangeException>(() => drawBag.Draw(sample));
        AssertHasTwoUndrawnItems(drawBag);
    }

    [Fact]
    public void Bags_and_captured_or_restored_counts_have_independent_ownership ()
    {
        WeightedBagDefinition<string> definition = new(
        [
            new WeightedBagEntry<string>("first", 1d, 1),
            new WeightedBagEntry<string>("second", 1d, 2),
        ]);
        WeightedBag<string> firstBag = definition.CreateBag();
        WeightedBag<string> secondBag = definition.CreateBag();
        int[] captured = firstBag.CaptureRemainingCounts();
        captured[0] = 0;

        Assert.Equal("first", firstBag.Draw(0d));
        Assert.Equal(new[] { 1, 2 }, secondBag.CaptureRemainingCounts());
        Assert.Equal(new[] { 0, 2 }, firstBag.CaptureRemainingCounts());

        int[] restoredInput = [0, 2];
        WeightedBag<string> restored = definition.Restore(restoredInput);
        restoredInput[1] = 0;
        Assert.Equal(new[] { 0, 2 }, restored.CaptureRemainingCounts());
        Assert.Equal("second", restored.Draw(0d));
    }

    [Fact]
    public void Remaining_counts_expose_current_definition_order_and_copy_into_caller_owned_storage ()
    {
        WeightedBagDefinition<string> definition = new(
        [
            new WeightedBagEntry<string>("first", 1d, 1),
            new WeightedBagEntry<string>("second", 1d, 2),
        ]);
        WeightedBag<string> bag = definition.Restore([0, 2]);

        ReadOnlySpan<int> remainingCounts = bag.RemainingCounts;

        Assert.Equal(2, remainingCounts.Length);
        Assert.Equal(0, remainingCounts[0]);
        Assert.Equal(2, remainingCounts[1]);

        Span<int> snapshot = stackalloc int[remainingCounts.Length];
        remainingCounts.CopyTo(snapshot);
        Assert.Equal("second", bag.Draw(0d));
        Assert.Equal(0, snapshot[0]);
        Assert.Equal(2, snapshot[1]);
    }

    [Fact]
    public void Restore_uses_dense_definition_ordinals_and_rejects_invalid_state_without_affecting_existing_bags ()
    {
        WeightedBagDefinition<string> definition = new(
        [
            new WeightedBagEntry<string>("first", 1d, 1),
            new WeightedBagEntry<string>("second", 1d, 2),
        ]);
        WeightedBag<string> existing = definition.CreateBag();
        WeightedBag<string> restored = definition.Restore([0, 1]);

        Assert.Equal("second", restored.Draw(0d));

        Assert.Throws<ArgumentException>(() => definition.Restore([1]));
        Assert.Throws<ArgumentException>(() => definition.Restore([2, 1]));
        Assert.Throws<ArgumentException>(() => definition.Restore([-1, 1]));
        Assert.Equal(new[] { 1, 2 }, existing.CaptureRemainingCounts());
        Assert.Equal(new[] { 1, 2 }, definition.CreateBag().CaptureRemainingCounts());

        WeightedBag<string> empty = definition.Restore([0, 0]);
        Assert.Equal(0L, empty.RemainingItemCount);
        Assert.Equal(new[] { 0, 0 }, empty.CaptureRemainingCounts());
    }

    [Fact]
    public void Restore_rejects_a_current_mass_that_cannot_advance_without_changing_existing_state ()
    {
        WeightedBagDefinition<int> definition = new(
        [
            new WeightedBagEntry<int>(1, 1d, int.MaxValue),
            new WeightedBagEntry<int>(2, Math.ScaleB(1d, -51), int.MaxValue),
        ]);
        WeightedBag<int> existing = definition.CreateBag();
        int[] remainingCounts = [int.MaxValue, 1];

        Assert.Throws<ArgumentException>(() => definition.Restore(remainingCounts));
        Assert.Equal(new[] { int.MaxValue, int.MaxValue }, existing.CaptureRemainingCounts());
        Assert.Equal(new[] { int.MaxValue, int.MaxValue }, definition.CreateBag().CaptureRemainingCounts());
        Assert.Equal(new[] { int.MaxValue, 1 }, remainingCounts);
    }

    [Fact]
    public void Maximum_valid_sample_selects_the_last_active_positive_interval_before_trailing_zero_mass ()
    {
        WeightedBagDefinition<string> definition = new(
        [
            new WeightedBagEntry<string>("small", double.Epsilon, 1),
            new WeightedBagEntry<string>("last-positive", 1d, 1),
            new WeightedBagEntry<string>("trailing", 1d, 1),
        ]);
        WeightedBag<string> bag = definition.CreateBag();
        double maximumSample = double.BitDecrement(1d);

        Assert.Equal("trailing", bag.Draw(maximumSample));
        Assert.Equal("last-positive", bag.Draw(maximumSample));
        Assert.Equal("small", bag.Draw(maximumSample));
    }

    private static WeightedBag<int> CreateTwoItemBag ()
    {
        WeightedBagDefinition<int> definition = new(
        [
            new WeightedBagEntry<int>(1, 1d, 1),
            new WeightedBagEntry<int>(2, 1d, 1),
        ]);

        return definition.CreateBag();
    }

    private static void AssertHasTwoUndrawnItems (WeightedBag<int> bag)
    {
        Assert.Equal(new[] { 1, 1 }, bag.CaptureRemainingCounts());
        Assert.Equal(2L, bag.RemainingItemCount);
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
