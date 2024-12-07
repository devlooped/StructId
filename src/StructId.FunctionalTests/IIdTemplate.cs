using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StructId;
using StructId.Functional;

[TStructId]
file partial record struct TSelf(Guid Value) : IId
{
    public Guid Id => Value;
}
