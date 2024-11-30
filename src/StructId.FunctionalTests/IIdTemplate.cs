using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StructId;

[TStructId]
file partial record struct IIdTemplate(Guid Value) : IId
{
    public Guid Id => Value;
}
