namespace Tralys.Analyzers.Tests;

public sealed class NullStorageTests
{
    [Test]
    public async Task Storage_that_holds_a_null_its_reads_deny_is_reported()
    {
        const string source = """
            using System.Diagnostics.CodeAnalysis;

            public sealed class Storage<T>
            {
                [AllowNull] string _field = "";
                [NotNull] string? _assumed = "";
                [AllowNull] public string Auto { get; set; } = "";
                [field: AllowNull] public string Backed { get; set; } = "";
                [AllowNull] T _slot = default;
                [AllowNull, MaybeNull] string _honest = "";
                string _name = "";
                [AllowNull] public string Normalized { get => _name; set => _name = value ?? ""; }
                public void Take([AllowNull] string value) { }
            }
            """;

        var reported = await Harness.Analyze(new NullStorageAnalyzer(), source);

        await Assert
            .That(reported)
            .IsEquivalentTo([
                ("TRA0005", Harness.LineOf(source, "_field")),
                ("TRA0005", Harness.LineOf(source, "_assumed")),
                ("TRA0005", Harness.LineOf(source, "Auto")),
                ("TRA0005", Harness.LineOf(source, "Backed")),
            ]);
    }
}
