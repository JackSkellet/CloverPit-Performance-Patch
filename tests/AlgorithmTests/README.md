# Algorithm tests

From the repository root, run with the .NET 10 SDK:

```sh
dotnet run --project tests/AlgorithmTests -c Release
```

This suite links the production helper sources directly. It needs no game installation, game DLLs, Unity Editor, test-service account, or external test packages. A failure returns a nonzero exit code.

Expected results:

- 29 transition-speed cases, including wrapping and integer extremes.
- 61,087 exact-reference pattern-order comparisons plus a 4,096-record retained-pool case. Repeated calls change flags, weights and arbitrarily large scores.
- 57 opacity cases with 840 assertions covering color/geometry/UV preservation, zero-alpha recovery and the stock-behavior fallback conditions.

The Unity/TMP and pattern types in `Fixtures` are minimal authored test doubles. The reference sorting routine is an independent rotation/compare-swap model. These tests verify the authored helper algorithms; they do not establish native Unity rendering parity, actual game frame rates, or patch compatibility with an untested game build.
