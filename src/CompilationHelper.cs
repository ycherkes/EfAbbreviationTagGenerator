using Microsoft.CodeAnalysis;

namespace EfAbbreviationTagGenerator;

internal static class CompilationHelper
{
    public static CompilationContext LoadEfCoreContext(Compilation compilation)
    {
        return new CompilationContext
        {
            EfQueryableExtensionsType = TryLoadSymbol(compilation, "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions")
        };
    }

    private static INamedTypeSymbol TryLoadSymbol(Compilation compilation, string symbolName)
    {
        return compilation.GetTypeByMetadataName(symbolName)?.OriginalDefinition;
    }
}