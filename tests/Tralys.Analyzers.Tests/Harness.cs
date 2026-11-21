using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Tralys.Analyzers.Tests;

static class Harness
{
    static readonly MetadataReference[] References =
    [
        .. File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "harness.references"))
            .Select(path => MetadataReference.CreateFromFile(path)),
    ];

    static readonly CSharpParseOptions Parse = new(LanguageVersion.Preview);

    public static async Task<IEnumerable<(string Id, int Line)>> Analyze(
        DiagnosticAnalyzer analyzer,
        string source,
        Func<CSharpCompilationOptions, CSharpCompilationOptions>? options = null
    )
    {
        var reported = await Compile(source, options)
            .WithAnalyzers([analyzer])
            .GetAnalyzerDiagnosticsAsync();
        return reported.Select(Located);
    }

    public static IEnumerable<(string Id, int Line)> Generate(
        IIncrementalGenerator generator,
        string source
    )
    {
        CSharpGeneratorDriver
            .Create([generator.AsSourceGenerator()], parseOptions: Parse)
            .RunGeneratorsAndUpdateCompilation(Compile(source), out _, out var reported);
        return reported.Select(Located);
    }

    static CSharpCompilation Compile(
        string source,
        Func<CSharpCompilationOptions, CSharpCompilationOptions>? options = null
    )
    {
        var defaults = new CSharpCompilationOptions(
            OutputKind.DynamicallyLinkedLibrary,
            nullableContextOptions: NullableContextOptions.Enable
        );
        var compilation = CSharpCompilation.Create(
            "Sample",
            [CSharpSyntaxTree.ParseText(source, Parse, "Sample.cs")],
            References,
            options?.Invoke(defaults) ?? defaults
        );
        var broken = compilation
            .GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();
        if (broken.Length > 0)
            throw new InvalidOperationException(
                string.Join("\n", broken.Select(d => d.ToString()))
            );
        return compilation;
    }

    static (string Id, int Line) Located(Diagnostic diagnostic) =>
        (diagnostic.Id, diagnostic.Location.GetLineSpan().StartLinePosition.Line);

    public static int LineOf(string source, string text) =>
        Array.FindIndex(source.Split('\n'), line => line.Contains(text, StringComparison.Ordinal));
}
