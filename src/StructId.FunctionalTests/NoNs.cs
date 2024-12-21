using StructId.Functional;

// Showcases that types don't need to have a namespace
public partial record struct NoNsId : IStructId;

public record NoNsProduct(NoNsId Id, string Name);