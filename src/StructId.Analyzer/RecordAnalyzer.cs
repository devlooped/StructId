using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using static StructId.Diagnostics;

namespace StructId;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class RecordAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(MustBeRecordStruct, MustHaveValueConstructor);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        if (!Debugger.IsAttached)
            context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(start =>
        {
            var known = new KnownTypes(start.Compilation);
            if (known.IStructId is null || known.IStructIdT is null)
                return;

            start.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.NamedType);
        });
    }

    static void AnalyzeSymbol(SymbolAnalysisContext context)
    {
        if (context.Symbol is not INamedTypeSymbol symbol)
            return;

        var known = new KnownTypes(context.Compilation);

        // We only care about IStructId and IStructId<T>
        if (!symbol.Is(known.IStructId) && !symbol.Is(known.IStructIdT))
            return;

        // We can only analyze if there's a declaration in source.
        if (symbol.DeclaringSyntaxReferences.Length == 0 ||
            symbol.DeclaringSyntaxReferences
            .Select(x => x.GetSyntax())
            .OfType<TypeDeclarationSyntax>()
            .FirstOrDefault() is not { } typeDeclaration)
            return;

        // TODO: report or ignore if more than one declaration?

        // If there's only one declaration and it's not partial
        var report = symbol.DeclaringSyntaxReferences.Length == 1 &&
            !typeDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword);

        report |= !symbol.IsRecord || symbol.TypeKind != TypeKind.Struct || !symbol.IsReadOnly;

        if (report)
        {
            if (typeDeclaration.BaseList?.Types.FirstOrDefault(t => t.Type is GenericNameSyntax { Identifier.Text: "IStructId", Arity: 1 }) is { } generic)
                context.ReportDiagnostic(Diagnostic.Create(MustBeRecordStruct, generic.GetLocation(), symbol.Name));
            else if (typeDeclaration.BaseList?.Types.FirstOrDefault(t => t.Type is IdentifierNameSyntax { Identifier.Text: "IStructId" }) is { } implementation)
                context.ReportDiagnostic(Diagnostic.Create(MustBeRecordStruct, implementation.GetLocation(), symbol.Name));
            else
                context.ReportDiagnostic(Diagnostic.Create(MustBeRecordStruct, symbol.Locations.FirstOrDefault(), symbol.Name));
        }

        if (typeDeclaration.ParameterList is null)
            return;

        // If there are parameters, it must be only one, be named Value and be either 
        // type string (if implementing IStructId) or the TValue (if implementing IStructId<TValue>)
        if (typeDeclaration.ParameterList.Parameters.Count != 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(MustHaveValueConstructor, typeDeclaration.ParameterList.GetLocation(), symbol.Name));
            return;
        }

        var parameter = typeDeclaration.ParameterList.Parameters[0];
        if (parameter.Identifier.Text != "Value")
            context.ReportDiagnostic(Diagnostic.Create(MustHaveValueConstructor, parameter.Identifier.GetLocation(), symbol.Name));
    }
}
