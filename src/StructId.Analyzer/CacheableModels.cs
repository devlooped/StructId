using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StructId;

/// <summary>
/// Plain-data model for a StructId type, replacing <c>TemplateArgs</c> which carried 
/// <see cref="ISymbol"/> objects that broke incremental caching.
/// </summary>
public record struct StructIdModel(
    string TypeName,
    string? Namespace,
    string TypeFullName,
    string ValueTypeName,
    string ValueTypeFullName,
    bool IsStringValue,
    string CoreNamespace,
    EquatableArray<string> ValueTypeAllInterfaces,
    string? ValueTypeBaseType,
    bool HasParameterList) : IEquatable<StructIdModel>
{
    public string HintName => Namespace != null ? $"{Namespace}.{TypeName}" : TypeName;

    public string FileName
    {
        get
        {
            var name = HintName.Replace('<', '{').Replace('>', '}');
            return name;
        }
    }
}

/// <summary>
/// Plain-data model for a [TStructId] template, replacing <c>Template</c> which carried 
/// <see cref="ISymbol"/> and <see cref="Compilation"/> objects.
/// </summary>
public record struct TemplateModel(
    string TemplateSyntaxText,
    string TemplateFilePath,
    string TValueFullName,
    bool IsLocalTValue,
    bool NoString,
    EquatableArray<string> TValueAllInterfaces,
    string? TValueBaseType) : IEquatable<TemplateModel>
{
    /// <summary>
    /// Checks whether this template applies to the given struct id model's value type.
    /// String-based equivalent of <c>Template.AppliesTo(INamedTypeSymbol)</c>.
    /// </summary>
    public bool AppliesTo(StructIdModel structId)
    {
        if (NoString && structId.IsStringValue)
            return false;

        if (structId.ValueTypeFullName == TValueFullName)
            return true;

        if (structId.ValueTypeAllInterfaces.Contains(TValueFullName))
            return true;

        // For file-local TValue types that define constraints via interfaces
        return IsLocalTValue &&
             (structId.ValueTypeBaseType == TValueBaseType ||
              structId.ValueTypeBaseType == "object" ||
              structId.ValueTypeBaseType == "System.ValueType") &&
             TValueAllInterfaces.AsImmutableArray().All(i =>
                structId.ValueTypeAllInterfaces.Contains(i));
    }
}

/// <summary>
/// Plain-data model for a [TValue] template used by Dapper, EF, and other value-type generators.
/// Replaces <c>TValueTemplate</c> which carried <see cref="ISymbol"/> and <see cref="Compilation"/>.
/// </summary>
public record struct TValueTemplateModel(
    string TemplateSyntaxText,
    string TTemplateName,
    string TTemplateFullName,
    bool IsLocalTValue,
    string TValueFullName,
    bool NoString,
    EquatableArray<string> TValueAllInterfaces,
    string? TValueBaseType,
    EquatableArray<string> TTemplateAllBaseTypes,
    EquatableArray<string> TTemplateAllInterfaces) : IEquatable<TValueTemplateModel>
{
    /// <summary>
    /// Checks whether this template applies to the given struct id model.
    /// </summary>
    public bool AppliesTo(StructIdModel model)
    {
        if (NoString && model.IsStringValue)
            return false;

        if (model.ValueTypeFullName == TValueFullName)
            return true;

        if (model.ValueTypeAllInterfaces.Contains(TValueFullName))
            return true;

        // For file-local TValue types that define constraints via interfaces
        return IsLocalTValue &&
             (model.ValueTypeBaseType == TValueBaseType ||
              model.ValueTypeBaseType == "object" ||
              model.ValueTypeBaseType == "System.ValueType") &&
             TValueAllInterfaces.AsImmutableArray().All(i =>
                model.ValueTypeAllInterfaces.Contains(i));
    }

    /// <summary>
    /// Checks if the template type is a subtype of the given target type.
    /// String-based equivalent of <c>Is(INamedTypeSymbol)</c>.
    /// Uses open generic name matching (strips generic arguments) because
    /// <c>OriginalDefinition.ToFullName()</c> uses the declaring type's parameter names.
    /// </summary>
    public bool IsSubtypeOf(string targetFullName)
    {
        var target = StripGenericArgs(targetFullName);
        return StripGenericArgs(TTemplateFullName) == target ||
               TTemplateAllBaseTypes.AsImmutableArray().Any(b => StripGenericArgs(b) == target) ||
               TTemplateAllInterfaces.AsImmutableArray().Any(i => StripGenericArgs(i) == target);
    }

    static string StripGenericArgs(string name)
    {
        var idx = name.IndexOf('<');
        return idx >= 0 ? name.Substring(0, idx) : name;
    }
}

/// <summary>
/// Output of applying a TValue template to a matched value type.
/// </summary>
public record struct TemplatizedValueOutput(
    string TTemplateFullName,
    EquatableArray<string> TTemplateAllBaseTypes,
    EquatableArray<string> TTemplateAllInterfaces,
    string ValueTypeFullName,
    string AppliedTypeName,
    string AppliedCode) : IEquatable<TemplatizedValueOutput>
{
    /// <summary>
    /// Checks if the template type is a subtype of the given target type.
    /// </summary>
    public bool IsSubtypeOf(string targetFullName)
    {
        var target = StripGenericArgs(targetFullName);
        return StripGenericArgs(TTemplateFullName) == target ||
               TTemplateAllBaseTypes.AsImmutableArray().Any(b => StripGenericArgs(b) == target) ||
               TTemplateAllInterfaces.AsImmutableArray().Any(i => StripGenericArgs(i) == target);
    }

    static string StripGenericArgs(string name)
    {
        var idx = name.IndexOf('<');
        return idx >= 0 ? name.Substring(0, idx) : name;
    }
}

/// <summary>
/// Factory methods for extracting cacheable models from symbols.
/// These run inside pipeline transform lambdas (with access to SemanticModel)
/// and produce plain-data records that cache correctly.
/// </summary>
static class ModelExtractors
{
    /// <summary>
    /// Extracts a <see cref="StructIdModel"/> from a declared struct id symbol.
    /// </summary>
    public static StructIdModel? ExtractStructIdModel(INamedTypeSymbol structId)
    {
        // Only process top-level types (in namespaces), not types nested inside classes/structs.
        // The original GetAllTypes() only iterated namespace members.
        if (structId.ContainingType != null)
            return null;

        if (!structId.IsStructId() || !structId.IsPartial())
            return null;

        var iface = structId.AllInterfaces.FirstOrDefault(x => x.Name == "IStructId");
        if (iface == null)
            return null;

        var valueType = iface.TypeArguments.OfType<INamedTypeSymbol>().FirstOrDefault();
        var isStringValue = valueType == null;
        var valueTypeName = valueType?.ToDisplayString(AnalysisExtensions.TypeName) ?? "string";
        var valueTypeFullName = valueType?.ToFullName() ?? "string";

        var coreNamespace = iface.ContainingNamespace?.ToFullName();
        if (string.IsNullOrEmpty(coreNamespace))
            coreNamespace = nameof(StructId);

        var valueAllInterfaces = valueType?.AllInterfaces
            .Select(i => i.OriginalDefinition.ToFullName())
            .Distinct()
            .ToImmutableArray() ?? ImmutableArray<string>.Empty;

        var valueBaseType = valueType?.BaseType?.OriginalDefinition.ToFullName();

        var ns = structId.ContainingNamespace != null && !structId.ContainingNamespace.IsGlobalNamespace
            ? structId.ContainingNamespace.ToDisplayString()
            : null;

        var hasParameterList = structId.DeclaringSyntaxReferences
            .Select(r => r.GetSyntax())
            .OfType<TypeDeclarationSyntax>()
            .Any(s => s.ParameterList != null);

        return new StructIdModel(
            TypeName: structId.Name,
            Namespace: ns,
            TypeFullName: structId.ToFullName(),
            ValueTypeName: valueTypeName,
            ValueTypeFullName: valueTypeFullName,
            IsStringValue: isStringValue,
            CoreNamespace: coreNamespace!,
            ValueTypeAllInterfaces: new EquatableArray<string>(valueAllInterfaces),
            ValueTypeBaseType: valueBaseType,
            HasParameterList: hasParameterList);
    }

    /// <summary>
    /// Extracts a <see cref="TemplateModel"/> from a [TStructId]-annotated template type.
    /// </summary>
    public static TemplateModel? ExtractTemplateModel(INamedTypeSymbol tself, Compilation compilation)
    {
        if (tself.DeclaringSyntaxReferences.Length == 0)
            return null;

        var syntaxRoot = tself.DeclaringSyntaxReferences[0].GetSyntax().SyntaxTree.GetRoot();
        var templateSyntaxText = syntaxRoot.ToFullString();
        var templateFilePath = tself.DeclaringSyntaxReferences[0].GetSyntax().SyntaxTree.FilePath;
        var noString = new NoStringSyntaxWalker().Accept(syntaxRoot);

        // Get the TValue type from the primary ctor Value parameter
        var valueProp = tself.GetMembers().OfType<IPropertySymbol>().FirstOrDefault(p => p.Name == "Value");
        if (valueProp == null)
            return null;

        if (valueProp.Type is not INamedTypeSymbol tvalue)
            return null;

        var isLocalTValue = tvalue.IsFileLocal;
        var tvalueFullName = tvalue.OriginalDefinition.ToFullName();
        var tvalueAllInterfaces = ImmutableArray<string>.Empty;
        string? tvalueBaseType = null;

        // For file-local TValue types, extract the constraint interface from the base list.
        // This is the interface that determines which value types this template applies to.
        if (isLocalTValue)
        {
            tvalueAllInterfaces = tvalue.AllInterfaces
                .Select(i => i.OriginalDefinition.ToFullName())
                .Distinct()
                .ToImmutableArray();
            tvalueBaseType = tvalue.BaseType?.OriginalDefinition.ToFullName();

            if (tvalue.DeclaringSyntaxReferences.Length > 0)
            {
                var type = tvalue.DeclaringSyntaxReferences[0].GetSyntax() as TypeDeclarationSyntax;
                var ifaceSyntax = type?.BaseList?.Types.FirstOrDefault()?.Type;
                if (type != null && ifaceSyntax != null)
                {
                    var model = compilation.GetSemanticModel(type.SyntaxTree);
                    if (model.GetSymbolInfo(ifaceSyntax).Symbol is INamedTypeSymbol ifaceType)
                    {
                        // Use OriginalDefinition for generic interfaces so that
                        // IParsable<TValue> becomes IParsable<T>, matching value type interfaces
                        tvalueFullName = ifaceType.OriginalDefinition.ToFullName();
                    }
                }
            }
        }

        return new TemplateModel(
            TemplateSyntaxText: templateSyntaxText,
            TemplateFilePath: templateFilePath,
            TValueFullName: tvalueFullName,
            IsLocalTValue: isLocalTValue,
            NoString: noString,
            TValueAllInterfaces: new EquatableArray<string>(tvalueAllInterfaces),
            TValueBaseType: tvalueBaseType);
    }

    /// <summary>
    /// Extracts a <see cref="TValueTemplateModel"/> from a [TValue]-annotated template type.
    /// </summary>
    public static TValueTemplateModel? ExtractTValueTemplateModel(INamedTypeSymbol ttemplate, Compilation compilation)
    {
        if (ttemplate.DeclaringSyntaxReferences.Length == 0)
            return null;

        var syntaxRoot = ttemplate.DeclaringSyntaxReferences[0].GetSyntax().SyntaxTree.GetRoot();
        var templateSyntaxText = syntaxRoot.ToFullString();
        var noString = new NoStringSyntaxWalker().Accept(syntaxRoot);

        // Find the file-local TValue type in the same file
        var tvalueDecl = syntaxRoot.DescendantNodes()
            .OfType<TypeDeclarationSyntax>()
            .Select(x => compilation.GetSemanticModel(syntaxRoot.SyntaxTree).GetDeclaredSymbol(x) as INamedTypeSymbol)
            .FirstOrDefault(x => x != null && x.Name == "TValue");

        var isLocalTValue = tvalueDecl != null && tvalueDecl.IsFileLocal;
        var tvalueFullName = tvalueDecl?.OriginalDefinition.ToFullName() ?? ttemplate.OriginalDefinition.ToFullName();
        var tvalueAllInterfaces = ImmutableArray<string>.Empty;
        string? tvalueBaseType = null;

        if (tvalueDecl != null)
        {
            tvalueAllInterfaces = tvalueDecl.AllInterfaces
                .Select(i => i.OriginalDefinition.ToFullName())
                .Distinct()
                .ToImmutableArray();
            tvalueBaseType = tvalueDecl.BaseType?.OriginalDefinition.ToFullName();
        }

        // Collect template type base types for Is() checks
        var baseTypesBuilder = ImmutableArray.CreateBuilder<string>();
        var current = ttemplate.BaseType;
        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            baseTypesBuilder.Add(current.OriginalDefinition.ToFullName());
            current = current.BaseType;
        }

        var templateAllInterfaces = ttemplate.AllInterfaces
            .Select(i => i.OriginalDefinition.ToFullName())
            .Distinct()
            .ToImmutableArray();

        return new TValueTemplateModel(
            TemplateSyntaxText: templateSyntaxText,
            TTemplateName: ttemplate.Name,
            TTemplateFullName: ttemplate.ToFullName(),
            IsLocalTValue: isLocalTValue,
            TValueFullName: tvalueFullName,
            NoString: noString,
            TValueAllInterfaces: new EquatableArray<string>(tvalueAllInterfaces),
            TValueBaseType: tvalueBaseType,
            TTemplateAllBaseTypes: new EquatableArray<string>(baseTypesBuilder.ToImmutable()),
            TTemplateAllInterfaces: new EquatableArray<string>(templateAllInterfaces));
    }
}
