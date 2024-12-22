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

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ClassDeclaration);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.StructDeclaration);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.RecordDeclaration);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.RecordStructDeclaration);
    }

    static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var known = new KnownTypes(context.Compilation);

        if (context.Node is not TypeDeclarationSyntax typeDeclaration ||
            known.IStructIdT is not { } structIdTypeOfT ||
            known.IStructId is not { } structIdType)
            return;

        var symbol = context.SemanticModel.GetDeclaredSymbol(typeDeclaration);
        if (symbol is null)
            return;

        if (!symbol.Is(structIdType) && !symbol.Is(structIdTypeOfT))
            return;

        // If there's only one declaration and it's not partial
        var report = symbol.DeclaringSyntaxReferences.Length == 1 && !typeDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword);
        report |= !typeDeclaration.IsKind(SyntaxKind.RecordStructDeclaration) || !symbol.IsReadOnly;

        if (report)
        {
            if (typeDeclaration.BaseList?.Types.FirstOrDefault(t => t.Type is GenericNameSyntax { Identifier.Text: "IStructId", Arity: 1 }) is { } generic)
                context.ReportDiagnostic(Diagnostic.Create(MustBeRecordStruct, generic.GetLocation(), symbol.Name));
            else if (typeDeclaration.BaseList?.Types.FirstOrDefault(t => t.Type is IdentifierNameSyntax { Identifier.Text: "IStructId" }) is { } implementation)
                context.ReportDiagnostic(Diagnostic.Create(MustBeRecordStruct, implementation.GetLocation(), symbol.Name));
            else
                context.ReportDiagnostic(Diagnostic.Create(MustBeRecordStruct, typeDeclaration.Identifier.GetLocation(), symbol.Name));
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
