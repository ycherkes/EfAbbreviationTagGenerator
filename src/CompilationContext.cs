using Microsoft.CodeAnalysis;

namespace EfAbbreviationTagGenerator;

internal class CompilationContext
{
    public INamedTypeSymbol EfQueryableExtensionsType { get; set; }
}