# Tralys.Analyzers

What the package does: `README.md`. It is also the nuget.org page, so it stays consumer-facing, and relative links
break there. This file covers working in the repo.

## Build and test

- `dotnet build Tralys.Analyzers.slnx`. Warnings are errors.
- `dotnet run --project tests/Tralys.Analyzers.Tests`, never `dotnet test`. Narrow with
  `-- --treenode-filter '/*/*/<Class>/<Test>'`.
- The full check is the steps in `.github/workflows/ci.yml`.
- A release is a `## [version]` section in `CHANGELOG.md` and a signed `v<version>` tag on master;
  `.github/workflows/release.yml` builds, publishes to nuget.org and creates the GitHub release.

## What belongs here

- Only rules that hold in any codebase. A rule about one library's API ships in that library's analyzers.
- Every rule ID is listed in `src/Tralys.Analyzers/AnalyzerReleases.Unshipped.md`; RS2008 fails the build otherwise.
- A rule nobody may switch off carries `WellKnownDiagnosticTags.NotConfigurable`, which stops `#pragma`, `<NoWarn>`
  and `.editorconfig`, and joins `SuppressionGuard.Guarded`. `[SuppressMessage]` silences an analyzer's diagnostics
  whatever their tags, but not a generator's, which is why that guard is a generator.
- No rule carries an allowance (a budget, a baseline, a per-project opt-out): an agent raises an allowance as easily
  as it adds the code the rule rejects.
- `NullableSwitchAnalyzer.NullableWarnings` copies the compiler's own list; a test fails when the referenced Roslyn
  knows one it lacks, so bump `Microsoft.CodeAnalysis.CSharp` and add the new IDs together.
- A `DiagnosticSuppressor` in another package can still silence a nullable warning; no analyzer sees another's.
- This repo has no null-forgiving operator either: the test project loads the analyzer.
