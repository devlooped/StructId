﻿// <auto-generated />
#nullable enable

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using StructId;

[TStructId]
file partial record struct TSelf(TValue Value) : INewable<TSelf, TValue>
{
    /// <summary>
    /// Provides value conversion for Entity Framework Core
    /// </summary>
    public partial class EntityFrameworkValueConverter : ValueConverter<TSelf, TValue>
    {
        public EntityFrameworkValueConverter() : this(null) { }

        public EntityFrameworkValueConverter(ConverterMappingHints? mappingHints = null)
            : base(id => id.Value, value => TSelf.New(value), mappingHints) { }
    }
}

file partial record struct TSelf
{
    public static TSelf New(TValue value) => throw new System.NotImplementedException();
}

file partial record struct TValue;