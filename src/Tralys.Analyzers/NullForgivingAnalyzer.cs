using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Tralys.Analyzers;

/// <summary>Rejects every null-forgiving operator (TRA0001). The rule cannot be switched off.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NullForgivingAnalyzer : DiagnosticAnalyzer
{
    static readonly DiagnosticDescriptor Forgiven = new(
        "TRA0001",
        "Null-forgiving operator",
        "'!' silences the compiler's null check; make the type non-nullable or handle the null here",
        "Tralys.Analyzers",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Forgiven];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            c =>
                c.ReportDiagnostic(
                    Diagnostic.Create(
                        Forgiven,
                        ((PostfixUnaryExpressionSyntax)c.Node).OperatorToken.GetLocation()
                    )
                ),
            SyntaxKind.SuppressNullableWarningExpression
        );
    }
}
