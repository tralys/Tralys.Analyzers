using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Tralys.Analyzers.Tests;

public sealed class NullableSwitchTests
{
    [Test]
    public async Task Every_source_switch_for_null_checks_is_reported()
    {
        const string source = """
            #nullable disable
            public sealed class Off { }
            #nullable enable
            #nullable disable warnings
            #pragma warning disable CS8602
            #pragma warning disable 8618
            #pragma warning disable nullable
            #pragma warning disable
            #pragma warning disable CS0168
            public sealed class On { }
            """;

        var reported = await Harness.Analyze(new NullableSwitchAnalyzer(), source);

        await Assert
            .That(reported)
            .IsEquivalentTo([
                ("TRA0003", 0),
                ("TRA0003", 3),
                ("TRA0003", 4),
                ("TRA0003", 5),
                ("TRA0003", 6),
                ("TRA0003", 7),
            ]);
    }

    [Test]
    public async Task Every_project_switch_for_null_checks_is_reported()
    {
        const string source = "public sealed class Sample { }";

        var disabled = await Harness.Analyze(
            new NullableSwitchAnalyzer(),
            source,
            options => options.WithNullableContextOptions(NullableContextOptions.Disable)
        );
        var noWarn = await Harness.Analyze(
            new NullableSwitchAnalyzer(),
            source,
            options =>
                options.WithSpecificDiagnosticOptions([new("CS8602", ReportDiagnostic.Suppress)])
        );
        var editorConfig = await Harness.Analyze(
            new NullableSwitchAnalyzer(),
            source,
            options =>
                options.WithSyntaxTreeOptionsProvider(
                    new Severity("CS8602", ReportDiagnostic.Suppress)
                )
        );

        await Assert.That(disabled.Select(d => d.Id)).IsEquivalentTo(["TRA0004"]);
        await Assert.That(noWarn.Select(d => d.Id)).IsEquivalentTo(["TRA0004"]);
        await Assert.That(editorConfig.Select(d => d.Id)).IsEquivalentTo(["TRA0003"]);
    }

    [Test]
    public async Task Every_nullable_warning_the_compiler_knows_is_listed()
    {
        if (
            typeof(CSharpCompilation)
                .Assembly.GetType("Microsoft.CodeAnalysis.CSharp.ErrorFacts")
                ?.GetField(
                    "NullableWarnings",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
                )
                ?.GetValue(null)
            is not IEnumerable<string> known
        )
            throw new InvalidOperationException("Roslyn no longer has ErrorFacts.NullableWarnings");

        await Assert.That(known.Except(NullableSwitchAnalyzer.NullableWarnings)).IsEmpty();
    }

    sealed class Severity(string id, ReportDiagnostic severity) : SyntaxTreeOptionsProvider
    {
        public override GeneratedKind IsGenerated(
            SyntaxTree tree,
            CancellationToken cancellationToken
        ) => GeneratedKind.NotGenerated;

        public override bool TryGetDiagnosticValue(
            SyntaxTree tree,
            string diagnosticId,
            CancellationToken cancellationToken,
            out ReportDiagnostic value
        )
        {
            value = severity;
            return diagnosticId == id;
        }

        public override bool TryGetGlobalDiagnosticValue(
            string diagnosticId,
            CancellationToken cancellationToken,
            out ReportDiagnostic value
        )
        {
            value = ReportDiagnostic.Default;
            return false;
        }
    }
}
