using StructId;

namespace Sample;

public record Product(ProductId Id, string Name);

public readonly partial record struct ProductId : IStructId<Guid>;
