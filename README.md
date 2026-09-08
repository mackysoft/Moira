# MackySoft.Moira

MackySoft.Moira provides deterministic weighted selection primitives for .NET Standard 2.1.
The caller supplies each selection sample, so the library does not own a random number generator.

## Install

```sh
dotnet add package MackySoft.Moira
```

## Fixed distribution

Use `WeightedDistribution<T>` when every selection uses the same source-ordered weights. Choose the compiled selection
method explicitly: `Cumulative` uses binary search, while `Alias` builds an alias table for constant-time selections.

```csharp
using MackySoft.Moira;

WeightedDistribution<string> distribution = new(
[
    new WeightedEntry<string>("common", 80d),
    new WeightedEntry<string>("rare", 20d),
],
WeightedSelectionMethod.Cumulative);

double sample = GetSampleFromYourRandomSource();
string? selected = distribution.Select(sample);

double[] samples = GetSamplesFromYourRandomSource(10);
string[] selectedBatch = new string[samples.Length];
distribution.SelectMany(samples, selectedBatch);
```

Use `WeightedSelectionMethod.Alias` for a fixed distribution that is selected from often enough to amortize the alias
table construction. Use `Cumulative` for distributions with fewer selections or shorter lifetimes. The library does not
select a method automatically because the break-even point depends on the target runtime and workload.

## Finite bag

Use `WeightedBagDefinition<T>` to create independent bags whose remaining quantities decrease after a draw.

```csharp
using MackySoft.Moira;

WeightedBagDefinition<string> definition = new(
[
    new WeightedBagEntry<string>("potion", 4d, 3),
    new WeightedBagEntry<string>("elixir", 1d, 1),
]);

WeightedBag<string> bag = definition.CreateBag();
double sample = GetSampleFromYourRandomSource();
string? drawn = bag.Draw(sample);

double[] samples = GetSamplesFromYourRandomSource(10);
string[] drawnBatch = new string[samples.Length];
bag.DrawMany(samples, drawnBatch);

WeightedBag<string> restored = definition.Restore(bag.RemainingCounts);
```

The span-based batch methods write directly into caller-owned storage and do not allocate a result array. Batch bag draws are ordered sequential draws: each result changes the remaining quantities used by the next sample. `TryDrawMany(samples, destination)` writes the available prefix and returns its length when the bag may contain fewer items than requested.

`RemainingCounts` is a transient, allocation-free `ReadOnlySpan<int>` over the bag-owned counts in definition order. Copy it into caller-owned storage before a draw when the previous state must remain stable. `CaptureRemainingCounts` provides the allocating convenience form. `Restore` copies and validates either input before returning a new bag.

## Samples and validation

Pass a finite sample in the half-open range `[0, 1)` to `Select`, `Draw`, and a nonempty `TryDraw`. Within the same package
version, the same distribution method, source order, weights, and sample bits select the same source entry. `Cumulative`
and `Alias` can map the same sample to different source entries, so persist the selected method with replay metadata.

Distribution entries require finite nonnegative weights, with at least one positive weight. Bag entries require finite positive unit weights and positive initial counts. Definitions and restored counts reject invalid or unrepresentable selection states with `ArgumentException`; invalid samples produce `ArgumentOutOfRangeException`.

`Draw` throws `InvalidOperationException` for an empty bag. Use `TryDraw` when an empty bag is an expected result; it returns `false` and sets the out value to its default.

## License

MackySoft.Moira is licensed under the [MIT License](LICENSE).
