using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StructId;

/// <summary>
/// Showcases a custom interface that we want implemented by all 
/// guid-based struct ids.
/// </summary>
public interface IId
{
    public Guid Id { get; }
}
