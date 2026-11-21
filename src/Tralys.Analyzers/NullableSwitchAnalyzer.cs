using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Tralys.Analyzers;

/// <summary>Rejects every way of switching the compiler's null checks off. In source (TRA0003): <c>#nullable
/// disable</c>; a <c>#pragma warning disable</c> naming a nullable warning, <c>nullable</c> or no code at all; an
/// <c>.editorconfig</c> severity that lowers a nullable warning. For the project (TRA0004): <c>Nullable</c> other
/// than <c>enable</c>; warning level 0; <c>NoWarn</c>, <c>WarningsNotAsErrors</c> or a global config lowering a
/// nullable warning.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NullableSwitchAnalyzer : DiagnosticAnalyzer
{
    const string Message =
        "{0} switches the compiler's null checks off; fix what they report instead";

    static readonly DiagnosticDescriptor InSource = new(
        "TRA0003",
        "Nullable checks switched off in source",
        Message,
        "Tralys.Analyzers",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    static readonly DiagnosticDescriptor InProject = new(
        "TRA0004",
        "Nullable checks switched off for the project",
        Message,
        "Tralys.Analyzers",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags:
        [
            WellKnownDiagnosticTags.CompilationEnd,
            WellKnownDiagnosticTags.NotConfigurable,
        ]
    );

    /// <summary>The compiler's nullable warnings, as <c>ErrorFacts.NullableWarnings</c> lists them.</summary>
    internal static readonly ImmutableHashSet<string> NullableWarnings =
    [
        "CS8597",
        "CS8600",
        "CS8601",
        "CS8602",
        "CS8603",
        "CS8604",
        "CS8605",
        "CS8607",
        "CS8608",
        "CS8609",
        "CS8610",
        "CS8611",
        "CS8612",
        "CS8613",
        "CS8614",
        "CS8615",
        "CS8616",
        "CS8617",
        "CS8618",
        "CS8619",
        "CS8620",
        "CS8621",
        "CS8622",
        "CS8624",
        "CS8625",
        "CS8629",
        "CS8631",
        "CS8633",
        "CS8634",
        "CS8643",
        "CS8644",
        "CS8645",
        "CS8655",
        "CS8667",
        "CS8670",
        "CS8714",
        "CS8762",
        "CS8763",
        "CS8764",
        "CS8765",
        "CS8766",
        "CS8767",
        "CS8768",
        "CS8769",
        "CS8770",
        "CS8774",
        "CS8775",
        "CS8776",
        "CS8777",
        "CS8819",
        "CS8824",
        "CS8825",
        "CS8847",
        "CS9158",
        "CS9159",
        "CS9264",
    ];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
    [InSource, InProject];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var options = start.Compilation.Options;
            start.RegisterSyntaxTreeAction(c => Source(c, options));
            start.RegisterCompilationEndAction(c => Project(c, options));
        });
    }

    static void Source(SyntaxTreeAnalysisContext context, CompilationOptions options)
    {
        var root = context.Tree.GetRoot(context.CancellationToken);
        for (
            var directive = root.GetFirstDirective();
            directive is not null;
            directive = directive.GetNextDirective()
        )
        {
            switch (directive)
            {
                case NullableDirectiveTriviaSyntax nullable
                    when nullable.SettingToken.IsKind(SyntaxKind.DisableKeyword):
                    Report(context, directive, nullable.ToString().Trim());
                    break;
                case PragmaWarningDirectiveTriviaSyntax pragma
                    when pragma.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword):
                    if (pragma.ErrorCodes.Count == 0)
                        Report(context, directive, "'#pragma warning disable' with no code");
                    foreach (var code in pragma.ErrorCodes)
                        if (IdOf(code) is { } id && IsNullable(id))
                            Report(context, code, $"'#pragma warning disable {code}'");
                    break;
            }
        }

        if (options.SyntaxTreeOptionsProvider is not { } provider)
            return;
        foreach (var id in NullableWarnings)
            if (
                provider.TryGetDiagnosticValue(
                    context.Tree,
                    id,
                    context.CancellationToken,
                    out var severity
                ) && Lowers(severity, options.GeneralDiagnosticOption)
            )
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        InSource,
                        Location.Create(context.Tree, default(TextSpan)),
                        $"An .editorconfig severity of '{severity}' for {id}"
                    )
                );
    }

    static void Project(CompilationAnalysisContext context, CompilationOptions options)
    {
        void Report(string what) =>
            context.ReportDiagnostic(Diagnostic.Create(InProject, Location.None, what));

        if (options.NullableContextOptions != NullableContextOptions.Enable)
            Report($"Nullable '{options.NullableContextOptions}'");
        if (options.WarningLevel == 0)
            Report("Warning level 0");
        if (options.GeneralDiagnosticOption == ReportDiagnostic.Suppress)
            Report("Suppressing every warning");
        foreach (var option in options.SpecificDiagnosticOptions)
            if (
                NullableWarnings.Contains(option.Key)
                && Lowers(option.Value, options.GeneralDiagnosticOption)
            )
                Report($"NoWarn or WarningsNotAsErrors for {option.Key}");
        if (options.SyntaxTreeOptionsProvider is { } provider)
            foreach (var id in NullableWarnings)
                if (
                    provider.TryGetGlobalDiagnosticValue(
                        id,
                        context.CancellationToken,
                        out var severity
                    ) && Lowers(severity, options.GeneralDiagnosticOption)
                )
                    Report($"A global config severity of '{severity}' for {id}");
    }

    static void Report(SyntaxTreeAnalysisContext context, SyntaxNode at, string what) =>
        context.ReportDiagnostic(Diagnostic.Create(InSource, at.GetLocation(), what));

    static string? IdOf(ExpressionSyntax code) =>
        code switch
        {
            IdentifierNameSyntax name => name.Identifier.ValueText,
            LiteralExpressionSyntax number => "CS" + number.Token.ValueText,
            _ => null,
        };

    static bool IsNullable(string id) =>
        string.Equals(id, "nullable", StringComparison.OrdinalIgnoreCase)
        || NullableWarnings.Contains(id.ToUpperInvariant());

    static bool Lowers(ReportDiagnostic severity, ReportDiagnostic general) =>
        severity is ReportDiagnostic.Suppress or ReportDiagnostic.Hidden or ReportDiagnostic.Info
        || (severity == ReportDiagnostic.Warn && general == ReportDiagnostic.Error);
}
