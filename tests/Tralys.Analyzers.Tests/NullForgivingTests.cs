using Microsoft.CodeAnalysis;

namespace Tralys.Analyzers.Tests;

public sealed class NullForgivingTests
{
    [Test]
    public async Task Every_null_forgiving_operator_is_reported_and_no_logical_not()
    {
        const string source = """
            using System;

            public sealed class Sample
            {
                string _name = null!;

                public int Length(string? text) => text!.Length;

                public Func<string> Later(string? text) => () => text!;

                public bool Differs(bool flag, object a, object b) => !flag && a != b;
            }
            """;

        var reported = await Harness.Analyze(new NullForgivingAnalyzer(), source);

        await Assert
            .That(reported)
            .IsEquivalentTo([
                ("TRA0001", Harness.LineOf(source, "null!")),
                ("TRA0001", Harness.LineOf(source, "text!.Length")),
                ("TRA0001", Harness.LineOf(source, "() => text!")),
            ]);
    }

    [Test]
    public async Task No_suppression_silences_a_null_forgiving_operator()
    {
        const string source = """
            #pragma warning disable TRA0001
            public sealed class Pragma
            {
                public int Length(string? text) => text!.Length;
            }
            #pragma warning restore TRA0001

            public sealed class Configured
            {
                public string Name(string? name) => name!;
            }
            """;

        var reported = await Harness.Analyze(
            new NullForgivingAnalyzer(),
            source,
            options =>
                options.WithSpecificDiagnosticOptions([new("TRA0001", ReportDiagnostic.Suppress)])
        );

        await Assert
            .That(reported)
            .IsEquivalentTo([
                ("TRA0001", Harness.LineOf(source, "text!")),
                ("TRA0001", Harness.LineOf(source, "name!")),
            ]);
    }

    [Test]
    public async Task A_suppression_attribute_naming_a_guarded_rule_is_rejected()
    {
        const string source = """
            using System.Diagnostics.CodeAnalysis;

            [assembly: UnconditionalSuppressMessage("Tralys.Analyzers", "TRA0001:Null-forgiving operator")]

            public sealed class Sample
            {
                [SuppressMessage("Tralys.Analyzers", "TRA0001")]
                public int Length(string? text) => text?.Length ?? 0;

                [SuppressMessage("Style", "IDE0060")]
                public int Zero(int unused) => 0;
            }
            """;

        var reported = Harness.Generate(new SuppressionGuard(), source);

        await Assert
            .That(reported)
            .IsEquivalentTo([
                ("TRA0002", Harness.LineOf(source, "TRA0001:")),
                ("TRA0002", Harness.LineOf(source, "\"TRA0001\")")),
            ]);
    }
}
