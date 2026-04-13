namespace StructId;

/// <summary>
/// Showcases a custom interface that we want implemented by all 
/// guid-based struct ids.
/// </summary>
public interface IId
{
    Guid Id { get; }
}
