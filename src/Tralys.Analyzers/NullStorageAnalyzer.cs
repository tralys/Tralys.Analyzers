using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Tralys.Analyzers;

/// <summary>Rejects <c>[AllowNull]</c> or <c>[NotNull]</c> on a field or auto-property (TRA0005): either lets it
/// store a null that reads then claim is not there, which no compiler check sees. Exempt: <c>[AllowNull]</c> with
/// <c>[MaybeNull]</c>, which is an honest nullable, and <c>[AllowNull]</c> on storage typed by an open type
/// parameter, which C# cannot otherwise spell as a slot cleared to default.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NullStorageAnalyzer : DiagnosticAnalyzer
{
    static readonly DiagnosticDescriptor Stored = new(
        "TRA0005",
        "A field or auto-property stores a null its reads deny",
        "[{0}] lets '{1}' store a null that its reads say cannot be there; declare its type nullable",
        "Tralys.Analyzers",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Stored];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(
            c =>
            {
                switch (c.Symbol)
                {
                    case IFieldSymbol { IsImplicitlyDeclared: false } field:
                        Check(c, field, field.Type, field.GetAttributes());
                        break;
                    case IPropertySymbol property when IsStored(property, c.CancellationToken):
                        Check(
                            c,
                            property,
                            property.Type,
                            [
                                .. property.GetAttributes(),
                                .. (
                                    BackingFieldOf(property) is { } backing
                                        ? backing.GetAttributes()
                                        : []
                                ),
                            ]
                        );
                        break;
                }
            },
            SymbolKind.Field,
            SymbolKind.Property
        );
    }

    static void Check(
        SymbolAnalysisContext context,
        ISymbol storage,
        ITypeSymbol type,
        ImmutableArray<AttributeData> attributes
    )
    {
        var maybeNull = attributes.Any(a => Is(a, "MaybeNullAttribute"));
        foreach (var attribute in attributes)
        {
            var allowNull = Is(attribute, "AllowNullAttribute");
            if (!allowNull && !Is(attribute, "NotNullAttribute"))
                continue;
            if (allowNull && (maybeNull || type is ITypeParameterSymbol))
                continue;
            context.ReportDiagnostic(
                Diagnostic.Create(
                    Stored,
                    attribute
                        .ApplicationSyntaxReference?.GetSyntax(context.CancellationToken)
                        .GetLocation()
                        ?? storage.Locations.FirstOrDefault(),
                    allowNull ? "AllowNull" : "NotNull",
                    storage.Name
                )
            );
        }
    }

    static bool Is(AttributeData attribute, string name) =>
        attribute.AttributeClass is { } type
        && type.Name == name
        && type.ContainingNamespace.ToDisplayString() == "System.Diagnostics.CodeAnalysis";

    static bool IsStored(IPropertySymbol property, CancellationToken token) =>
        property
            .DeclaringSyntaxReferences.Select(r => r.GetSyntax(token))
            .OfType<PropertyDeclarationSyntax>()
            .Any(declaration =>
                declaration.AccessorList is { } accessors
                && (
                    accessors.Accessors.All(a => a.Body is null && a.ExpressionBody is null)
                    || accessors.DescendantNodes().Any(n => n.IsKind(SyntaxKind.FieldExpression))
                )
            );

    static IFieldSymbol? BackingFieldOf(IPropertySymbol property) =>
        property
            .ContainingType.GetMembers()
            .OfType<IFieldSymbol>()
            .FirstOrDefault(f =>
                SymbolEqualityComparer.Default.Equals(f.AssociatedSymbol, property)
            );
}
