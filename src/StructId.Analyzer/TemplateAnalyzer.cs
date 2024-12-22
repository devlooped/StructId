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
public class TemplateAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(TemplateMustBeFileRecordStruct, TemplateConstructorValueConstructor, TemplateDeclarationNotTSelf);

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
        if (context.Node is not TypeDeclarationSyntax typeDeclaration ||
            !typeDeclaration.AttributeLists.Any(list => list.Attributes.Any(attr => attr.IsStructIdTemplate())))
            return;

        var symbol = context.SemanticModel.GetDeclaredSymbol(typeDeclaration);
        if (symbol is null)
            return;

        if (!symbol.IsPartial() || !typeDeclaration.IsKind(SyntaxKind.RecordStructDeclaration))
        {
            context.ReportDiagnostic(Diagnostic.Create(TemplateMustBeFileRecordStruct, typeDeclaration.Identifier.GetLocation(), symbol.Name));
        }

        foreach (var nonLocal in symbol.DeclaringSyntaxReferences
            .Select(x => x.GetSyntax() as TypeDeclarationSyntax)
            .Where(x => x != null && !x.Modifiers.Any(m => m.IsKind(SyntaxKind.FileKeyword))))
        {
            // We report the issue at each declaration, since ALL need to be file-local to be picked up by the code template cleanup.
            context.ReportDiagnostic(Diagnostic.Create(TemplateMustBeFileRecordStruct, nonLocal!.Identifier.GetLocation(), symbol.Name));
        }

        // If there are parameters, it must be only one, and be named Value
        if (typeDeclaration.ParameterList is { } parameters)
        {

            if (typeDeclaration.ParameterList.Parameters.Count != 1)
                context.ReportDiagnostic(Diagnostic.Create(TemplateConstructorValueConstructor, typeDeclaration.ParameterList.GetLocation(), symbol.Name));
            else if (typeDeclaration.ParameterList.Parameters[0].Identifier.Text != "Value")
                context.ReportDiagnostic(Diagnostic.Create(TemplateConstructorValueConstructor, typeDeclaration.ParameterList.Parameters[0].Identifier.GetLocation(), symbol.Name));
        }

        if (typeDeclaration.Identifier.Text != "TSelf")
            context.ReportDiagnostic(Diagnostic.Create(TemplateDeclarationNotTSelf, typeDeclaration.Identifier.GetLocation(), symbol.Name));
    }
}
