using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Scriban;

namespace StructId;

[Generator(LanguageNames.CSharp)]
public class DapperGenerator() : BaseGenerator(
    "Dapper.SqlMapper+TypeHandler`1", "", "", ReferenceCheck.TypeExists)
{
    static readonly Template template = Template.Parse(ThisAssembly.Resources.DapperExtensions.Text);

    protected override IncrementalValuesProvider<TemplateArgs> OnInitialize(IncrementalGeneratorInitializationContext context, IncrementalValuesProvider<TemplateArgs> source)
    {
        bool IsBuiltIn(string type) => type switch
        {
            "System.String" => true,
            "System.Guid" => true,
            "System.Int32" => true,
            "System.Int64" => true,
            "string" => true,
            "int" => true,
            "long" => true,
            _ => false
        };

        var builtInHandled = source.Where(x => IsBuiltIn(x.TValue.ToFullName()));

        // Any type in the compilation that inherits from Dapper.SqlMapper.TypeHandler<T> is also picked up, 
        // unless its a value template
        var customHandlers = context.CompilationProvider
            .SelectMany((x, _) => x.Assembly.GetAllTypes().OfType<INamedTypeSymbol>())
            .Combine(context.CompilationProvider.Select((x, _) => x.GetTypeByMetadataName("Dapper.SqlMapper+TypeHandler`1")))
            .Where(x => x.Left != null && x.Right != null &&
                x.Left.Is(x.Right) &&
                // Don't emit as plain handlers if they are id templates
                !x.Left.GetAttributes().Any(a => a.IsValueTemplate()))
            .Select((x, _) => x.Left)
            .Collect();

        // Non built-in value types can be templatized by using [TValue] templates. These would necessarily be
        // file-local types which are not registered as handlers themselves but applied to each struct id TValue in turn.
        var templatizedValues = context.SelectTemplatizedValues()
            .Where(x => !IsBuiltIn(x.TValue.ToFullName()))
            .Combine(context.CompilationProvider.Select((x, _) => x.GetTypeByMetadataName("Dapper.SqlMapper+TypeHandler`1")))
            .Where(x => x.Left.Template.TTemplate.Is(x.Right))
            .Select((x, _) => x.Left);

        // If there are custom type handlers for value types that are in turn used in struct ids, we need to register them 
        // as handlers that pass-through to the value handler itself. 
        var customHandled = source
            .Combine(customHandlers.Combine(templatizedValues.Collect()))
            .Select((x, _) =>
            {
                (TemplateArgs args, (ImmutableArray<INamedTypeSymbol> handlers, ImmutableArray<TemplatizedTValue> templatized)) = x;

                var handlerType = args.ReferenceType.Construct(args.TValue);
                var handler = handlers.FirstOrDefault(x => x.Is(handlerType, false));

                if (handler == null)
                {
                    var templated = templatized.Where(x => x.TValue.Equals(args.TValue, SymbolEqualityComparer.Default))
                        .FirstOrDefault();
                    // Consider templatized handlers that will be emitted as custom handlers too for registration.
                    if (templated != null)
                    {
                        var identifier = templated.Template.Syntax.ApplyValue(templated.TValue)
                           .DescendantNodes()
                           .OfType<TypeDeclarationSyntax>()
                           .First()
                           .Identifier.Text;

                        // Use lighter symbol since our template rendering only uses the type name.
                        handler = new KnownTypeNameSymbol(identifier);
                    }
                }

                return args with { ReferenceType = handler! };
            })
            .Where(x => x.ReferenceType != null);

        context.RegisterSourceOutput(builtInHandled.Collect().Combine(customHandled.Collect()).Combine(templatizedValues.Collect()), GenerateHandlers);

        // Turn off codegen in the base template.
        return source.Where(x => false);
    }

    void GenerateHandlers(SourceProductionContext context, ((ImmutableArray<TemplateArgs> builtInHandled, ImmutableArray<TemplateArgs> customHandled), ImmutableArray<TemplatizedTValue> templatizedValues) source)
    {
        var ((builtInHandled, customHandled), templatizedValues) = source;
        if (builtInHandled.Length == 0 && customHandled.Length == 0 && templatizedValues.Length == 0)
            return;

        var structIdNamespace = builtInHandled.Concat(customHandled).Select(x => x.KnownTypes.StructIdNamespace).FirstOrDefault()
            ?? "StructId";

        var templatizedHandlers = new HashSet<string>(templatizedValues
            .Select(x => x.TypeName));

        var customValueHandlers = customHandled
            .GroupBy(x => x.ReferenceType.ToFullName())
            // Avoid registering twice the same templatized value handlers since they are 
            // already added at the end of the scriban rendering.
            .Where(x => !templatizedHandlers.Contains(x.Key))
            .Select(x => new ValueHandlerModel(x.First().TValue.ToFullName(), x.Key))
            .ToArray();

        var model = new SelectorModel(
            structIdNamespace,
            // Built-in use the Name of the value type since it's used as a suffix for well-known provided implementations.
            builtInHandled.Select(x => new StructIdModel(x.TSelf.ToFullName(), x.TValue.Name)),
            customHandled.Select(x => new StructIdCustomModel(x.TSelf.ToFullName(), x.TValue.ToFullName(), x.ReferenceType.ToFullName())),
            customValueHandlers,
            templatizedValues.Select(x => new ValueHandlerModelCode(x)));

        var output = template.Render(model, member => member.Name);
        context.AddSource($"DapperExtensions.cs", output.Trim());
    }

    public static string Render(string @namespace, string tself, string tvalue)
        => template.Render(new SelectorModel(@namespace, [new(tself, tvalue)], [], [], []), member => member.Name).Trim();

    public static string RenderCustom(string @namespace, string tself, string tvalue, string thandler)
        => template.Render(new SelectorModel(@namespace, [], [new(tself, tvalue, thandler)], [new(tvalue, thandler)], []), member => member.Name).Trim();

    public static string RenderTemplatized(string @namespace, string tself, string tvalue, string thandler, string handlerCode)
        => template.Render(new SelectorModel(@namespace, [], [new(tself, tvalue, thandler)], [], [new(tvalue, thandler, handlerCode)]), member => member.Name).Trim();

    record StructIdModel(string TSelf, string TValue);

    record StructIdCustomModel(string TSelf, string TValue, string THandler);

    record ValueHandlerModel(string TValue, string THandler);

    class ValueHandlerModelCode
    {
        public ValueHandlerModelCode(TemplatizedTValue template)
        {
            var declaration = template.Template.Syntax.ApplyValue(template.TValue)
               .DescendantNodes()
               .OfType<TypeDeclarationSyntax>()
               .First();

            TValue = template.TValue.ToFullName();
            THandler = declaration.Identifier.Text;
            Code = declaration.ToFullString();
        }

        public ValueHandlerModelCode(string tvalue, string thandler, string code)
            => (TValue, THandler, Code) = (tvalue, thandler, code);

        public string TValue { get; }
        public string THandler { get; }
        public string Code { get; }
    }

    record SelectorModel(
        string Namespace,
        IEnumerable<StructIdModel> Ids,
        IEnumerable<StructIdCustomModel> CustomIds,
        IEnumerable<ValueHandlerModel> CustomValues,
        IEnumerable<ValueHandlerModelCode> TemplatizedValueHandlers);

#pragma warning disable RS1009 // Only internal implementations of this interface are allowed
    class KnownTypeNameSymbol(string typeName) : INamedTypeSymbol
#pragma warning restore RS1009 // Only internal implementations of this interface are allowed
    {
        public string Name => typeName;
        public string ToDisplayString(SymbolDisplayFormat? format = null) => typeName;

        public int Arity => throw new System.NotImplementedException();

        public bool IsGenericType => throw new System.NotImplementedException();

        public bool IsUnboundGenericType => throw new System.NotImplementedException();

        public bool IsScriptClass => throw new System.NotImplementedException();

        public bool IsImplicitClass => throw new System.NotImplementedException();

        public bool IsComImport => throw new System.NotImplementedException();

        public bool IsFileLocal => throw new System.NotImplementedException();

        public IEnumerable<string> MemberNames => throw new System.NotImplementedException();

        public ImmutableArray<ITypeParameterSymbol> TypeParameters => throw new System.NotImplementedException();

        public ImmutableArray<ITypeSymbol> TypeArguments => throw new System.NotImplementedException();

        public ImmutableArray<NullableAnnotation> TypeArgumentNullableAnnotations => throw new System.NotImplementedException();

        public INamedTypeSymbol OriginalDefinition => throw new System.NotImplementedException();

        public IMethodSymbol? DelegateInvokeMethod => throw new System.NotImplementedException();

        public INamedTypeSymbol? EnumUnderlyingType => throw new System.NotImplementedException();

        public INamedTypeSymbol ConstructedFrom => throw new System.NotImplementedException();

        public ImmutableArray<IMethodSymbol> InstanceConstructors => throw new System.NotImplementedException();

        public ImmutableArray<IMethodSymbol> StaticConstructors => throw new System.NotImplementedException();

        public ImmutableArray<IMethodSymbol> Constructors => throw new System.NotImplementedException();

        public ISymbol? AssociatedSymbol => throw new System.NotImplementedException();

        public bool MightContainExtensionMethods => throw new System.NotImplementedException();

        public INamedTypeSymbol? TupleUnderlyingType => throw new System.NotImplementedException();

        public ImmutableArray<IFieldSymbol> TupleElements => throw new System.NotImplementedException();

        public bool IsSerializable => throw new System.NotImplementedException();

        public INamedTypeSymbol? NativeIntegerUnderlyingType => throw new System.NotImplementedException();

        public TypeKind TypeKind => throw new System.NotImplementedException();

        public INamedTypeSymbol? BaseType => throw new System.NotImplementedException();

        public ImmutableArray<INamedTypeSymbol> Interfaces => throw new System.NotImplementedException();

        public ImmutableArray<INamedTypeSymbol> AllInterfaces => throw new System.NotImplementedException();

        public bool IsReferenceType => throw new System.NotImplementedException();

        public bool IsValueType => throw new System.NotImplementedException();

        public bool IsAnonymousType => throw new System.NotImplementedException();

        public bool IsTupleType => throw new System.NotImplementedException();

        public bool IsNativeIntegerType => throw new System.NotImplementedException();

        public SpecialType SpecialType => throw new System.NotImplementedException();

        public bool IsRefLikeType => throw new System.NotImplementedException();

        public bool IsUnmanagedType => throw new System.NotImplementedException();

        public bool IsReadOnly => throw new System.NotImplementedException();

        public bool IsRecord => throw new System.NotImplementedException();

        public NullableAnnotation NullableAnnotation => throw new System.NotImplementedException();

        public bool IsNamespace => throw new System.NotImplementedException();

        public bool IsType => throw new System.NotImplementedException();

        public SymbolKind Kind => throw new System.NotImplementedException();

        public string Language => throw new System.NotImplementedException();

        public string MetadataName => throw new System.NotImplementedException();

        public int MetadataToken => throw new System.NotImplementedException();

        public ISymbol ContainingSymbol => throw new System.NotImplementedException();

        public IAssemblySymbol ContainingAssembly => throw new System.NotImplementedException();

        public IModuleSymbol ContainingModule => throw new System.NotImplementedException();

        public INamedTypeSymbol ContainingType => throw new System.NotImplementedException();

        public INamespaceSymbol ContainingNamespace => throw new System.NotImplementedException();

        public bool IsDefinition => throw new System.NotImplementedException();

        public bool IsStatic => throw new System.NotImplementedException();

        public bool IsVirtual => throw new System.NotImplementedException();

        public bool IsOverride => throw new System.NotImplementedException();

        public bool IsAbstract => throw new System.NotImplementedException();

        public bool IsSealed => throw new System.NotImplementedException();

        public bool IsExtern => throw new System.NotImplementedException();

        public bool IsImplicitlyDeclared => throw new System.NotImplementedException();

        public bool CanBeReferencedByName => throw new System.NotImplementedException();

        public ImmutableArray<Location> Locations => throw new System.NotImplementedException();

        public ImmutableArray<SyntaxReference> DeclaringSyntaxReferences => throw new System.NotImplementedException();

        public Accessibility DeclaredAccessibility => throw new System.NotImplementedException();

        public bool HasUnsupportedMetadata => throw new System.NotImplementedException();

        ITypeSymbol ITypeSymbol.OriginalDefinition => throw new System.NotImplementedException();

        ISymbol ISymbol.OriginalDefinition => throw new System.NotImplementedException();

        public void Accept(SymbolVisitor visitor) => throw new System.NotImplementedException();
        public TResult? Accept<TResult>(SymbolVisitor<TResult> visitor) => throw new System.NotImplementedException();
        public TResult Accept<TArgument, TResult>(SymbolVisitor<TArgument, TResult> visitor, TArgument argument) => throw new System.NotImplementedException();
        public INamedTypeSymbol Construct(params ITypeSymbol[] typeArguments) => throw new System.NotImplementedException();
        public INamedTypeSymbol Construct(ImmutableArray<ITypeSymbol> typeArguments, ImmutableArray<NullableAnnotation> typeArgumentNullableAnnotations) => throw new System.NotImplementedException();
        public INamedTypeSymbol ConstructUnboundGenericType() => throw new System.NotImplementedException();
        public bool Equals(ISymbol? other, SymbolEqualityComparer equalityComparer) => throw new System.NotImplementedException();
        public bool Equals(ISymbol? other) => throw new System.NotImplementedException();
        public ISymbol? FindImplementationForInterfaceMember(ISymbol interfaceMember) => throw new System.NotImplementedException();
        public ImmutableArray<AttributeData> GetAttributes() => throw new System.NotImplementedException();
        public string? GetDocumentationCommentId() => throw new System.NotImplementedException();
        public string? GetDocumentationCommentXml(CultureInfo? preferredCulture = null, bool expandIncludes = false, CancellationToken cancellationToken = default) => throw new System.NotImplementedException();
        public ImmutableArray<ISymbol> GetMembers() => throw new System.NotImplementedException();
        public ImmutableArray<ISymbol> GetMembers(string name) => throw new System.NotImplementedException();
        public ImmutableArray<CustomModifier> GetTypeArgumentCustomModifiers(int ordinal) => throw new System.NotImplementedException();
        public ImmutableArray<INamedTypeSymbol> GetTypeMembers() => throw new System.NotImplementedException();
        public ImmutableArray<INamedTypeSymbol> GetTypeMembers(string name) => throw new System.NotImplementedException();
        public ImmutableArray<INamedTypeSymbol> GetTypeMembers(string name, int arity) => throw new System.NotImplementedException();
        public ImmutableArray<SymbolDisplayPart> ToDisplayParts(NullableFlowState topLevelNullability, SymbolDisplayFormat? format = null) => throw new System.NotImplementedException();
        public ImmutableArray<SymbolDisplayPart> ToDisplayParts(SymbolDisplayFormat? format = null) => throw new System.NotImplementedException();
        public string ToDisplayString(NullableFlowState topLevelNullability, SymbolDisplayFormat? format = null) => throw new System.NotImplementedException();
        public ImmutableArray<SymbolDisplayPart> ToMinimalDisplayParts(SemanticModel semanticModel, NullableFlowState topLevelNullability, int position, SymbolDisplayFormat? format = null) => throw new System.NotImplementedException();
        public ImmutableArray<SymbolDisplayPart> ToMinimalDisplayParts(SemanticModel semanticModel, int position, SymbolDisplayFormat? format = null) => throw new System.NotImplementedException();
        public string ToMinimalDisplayString(SemanticModel semanticModel, NullableFlowState topLevelNullability, int position, SymbolDisplayFormat? format = null) => throw new System.NotImplementedException();
        public string ToMinimalDisplayString(SemanticModel semanticModel, int position, SymbolDisplayFormat? format = null) => throw new System.NotImplementedException();
        public ITypeSymbol WithNullableAnnotation(NullableAnnotation nullableAnnotation) => throw new System.NotImplementedException();
    }
}