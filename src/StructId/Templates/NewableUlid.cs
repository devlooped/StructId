using System;
using StructId;

[TStructId]
file partial record struct TSelf(Ulid Value)
{
    public static TSelf New() => new(Ulid.NewUlid());
}

// This will be removed when applying the template to each user-defined struct id.
file partial record struct TSelf : INewable<TSelf, Ulid>
{
    public static TSelf New(Ulid value) => throw new NotImplementedException();
}