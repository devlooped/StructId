using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;

namespace StructId;

public static class RoslynTestingExtensions
{
    // Workaround for https://github.com/dotnet/roslyn-sdk/pull/1195
    public static void Add(this SourceFileCollection sources, (string filename, string content, Encoding encoding) file)
    {
        sources.Add((file.filename, SourceText.From(file.content, file.encoding)));
    }

    public static void Add(this SourceFileCollection sources, (Type generatorType, string filename, string content, Encoding encoding) file)
    {
        sources.Add((file.generatorType, file.filename, SourceText.From(file.content, file.encoding)));
    }

}
