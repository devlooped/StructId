using System.Collections.Immutable;
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
        => ImmutableArray.Create(MustBeRecordStruct);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ClassDeclaration);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.StructDeclaration);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.RecordDeclaration);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.RecordStructDeclaration);
    }

    static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var ns = context.Options.AnalyzerConfigOptionsProvider.GlobalOptions.GetStructIdNamespace();

        if (context.Node is not TypeDeclarationSyntax typeDeclaration ||
            context.Compilation.GetTypeByMetadataName($"{ns}.IStructId`1") is not { } structIdType)
            return;

        var symbol = context.SemanticModel.GetDeclaredSymbol(typeDeclaration);
        if (symbol is null)
            return;

        if (!symbol.Is(structIdType))
            return;

        if (!typeDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword) ||
            !typeDeclaration.Modifiers.Any(SyntaxKind.ReadOnlyKeyword) ||
            !typeDeclaration.IsKind(SyntaxKind.RecordStructDeclaration))
        {
            if (typeDeclaration.BaseList?.Types.FirstOrDefault(t => t.Type is GenericNameSyntax { Identifier.Text: "IStructId", Arity: 1 }) is { } implementation)
                context.ReportDiagnostic(Diagnostic.Create(MustBeRecordStruct, implementation.GetLocation(), symbol.Name));
            else
                context.ReportDiagnostic(Diagnostic.Create(MustBeRecordStruct, typeDeclaration.Identifier.GetLocation(), symbol.Name));
        }

    }
}
