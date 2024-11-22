using System;

namespace Sample;

public readonly partial record struct Class1 : IStructId<Guid>
{
}

public readonly partial record struct UserId : IStructId<Guid>;
