﻿// <auto-generated />
#nullable enable

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using StructId.Functional;

[TStructId]
file partial record struct TSelf(Ulid Value) : INewable<TSelf, Ulid>
{
    /// <summary>
    /// Provides value conversion for Entity Framework Core
    /// </summary>
    public partial class EntityFrameworkUlidValueConverter : ValueConverter<TSelf, string>
    {
        public EntityFrameworkUlidValueConverter() : this(null) { }
        
        public EntityFrameworkUlidValueConverter(ConverterMappingHints? mappingHints = null)
            : base(id => id.Value.ToString(), value => TSelf.New(Ulid.Parse(value)), mappingHints) { }
    }
}

file partial record struct TSelf
{
    public static TSelf New(Ulid value) => throw new System.NotImplementedException();
}