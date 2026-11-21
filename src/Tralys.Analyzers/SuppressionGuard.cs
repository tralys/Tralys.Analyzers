using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Tralys.Analyzers;

/// <summary>Rejects a <c>[SuppressMessage]</c> or <c>[UnconditionalSuppressMessage]</c> naming a rule no one may
/// suppress (TRA0002). Those attributes silence an analyzer's diagnostics whatever their tags, but not a generator's,
/// so the check runs as a generator.</summary>
[Generator(LanguageNames.CSharp)]
public sealed class SuppressionGuard : IIncrementalGenerator
{
    static readonly ImmutableHashSet<string> Guarded = ["TRA0001", "TRA0003", "TRA0004", "TRA0005"];

    static readonly DiagnosticDescriptor Suppressed = new(
        "TRA0002",
        "A suppression names a rule that cannot be suppressed",
        "{0} cannot be suppressed; fix what it reports",
        "Tralys.Analyzers",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var suppressions = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => node is AttributeSyntax { ArgumentList.Arguments.Count: >= 2 },
            static (c, token) => Of((AttributeSyntax)c.Node, c.SemanticModel, token)
        );
        context.RegisterSourceOutput(
            suppressions,
            static (output, found) =>
            {
                if (found is { } s)
                    output.ReportDiagnostic(
                        Diagnostic.Create(
                            Suppressed,
                            Location.Create(s.Path, s.Span, s.Lines),
                            s.Id
                        )
                    );
            }
        );
    }

    static Suppression? Of(AttributeSyntax attribute, SemanticModel model, CancellationToken token)
    {
        if (
            attribute.ArgumentList is not { Arguments: { Count: >= 2 } arguments }
            || model.GetSymbolInfo(attribute, token).Symbol?.ContainingType is not { } type
            || type.ContainingNamespace.ToDisplayString() != "System.Diagnostics.CodeAnalysis"
            || type.Name
                is not ("SuppressMessageAttribute" or "UnconditionalSuppressMessageAttribute")
            || model.GetConstantValue(arguments[1].Expression, token).Value is not string checkId
        )
            return null;

        var colon = checkId.IndexOf(':');
        var id = (colon < 0 ? checkId : checkId.Substring(0, colon)).Trim();
        if (!Guarded.Contains(id))
            return null;

        var location = attribute.GetLocation();
        return new Suppression(
            location.SourceTree?.FilePath ?? "",
            location.SourceSpan,
            location.GetLineSpan().Span,
            id
        );
    }

    readonly record struct Suppression(
        string Path,
        TextSpan Span,
        LinePositionSpan Lines,
        string Id
    );
}
