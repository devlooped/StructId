namespace StructId;

/// <summary>
/// Constants for incremental generator pipeline step tracking names.
/// Used with <c>.WithTrackingName()</c> to enable <c>TrackedSteps</c> 
/// queries in incrementality tests.
/// </summary>
static class TrackingNames
{
    public const string ReferenceType = nameof(ReferenceType);
    public const string StructIds = nameof(StructIds);
    public const string Combined = nameof(Combined);
    public const string Templates = nameof(Templates);
    public const string TemplatizedStructIds = nameof(TemplatizedStructIds);
    public const string BuiltInHandled = nameof(BuiltInHandled);
    public const string CustomHandlers = nameof(CustomHandlers);
    public const string TemplatizedValues = nameof(TemplatizedValues);
    public const string Converters = nameof(Converters);
    public const string NewtonsoftSource = nameof(NewtonsoftSource);
    public const string TValueTemplates = nameof(TValueTemplates);
    public const string TValueValues = nameof(TValueValues);
}
