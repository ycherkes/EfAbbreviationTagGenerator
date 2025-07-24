using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;

namespace EfAbbreviationTagGenerator;

[Generator]
public class AbbreviationTagGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var compilationInfoFromProvider = context.CompilationProvider
            .Select((c, _) => CompilationHelper.LoadEfCoreContext(c));

        var calls = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => s is InvocationExpressionSyntax
                {
                    Expression: MemberAccessExpressionSyntax
                    {
                        Name.Identifier.Text: "TagWithCallSiteAbbreviation"
                    }
                },
                transform: static (ctx, _) => (InvocationExpressionSyntax)ctx.Node)
            .Collect();

        var combined = compilationInfoFromProvider.Combine(calls);

        context.RegisterImplementationSourceOutput(combined, GenerateExtension);
    }

    private static void GenerateExtension(SourceProductionContext context, (CompilationContext compilationContext, ImmutableArray<InvocationExpressionSyntax> calls) arg)
    {
        if (arg.compilationContext?.EfQueryableExtensionsType == null)
            return;

        var entries = new HashSet<(string file, string method, int line)>();

        foreach (var call in arg.calls)
        {
            var location = call.GetLocation().GetLineSpan();
            var file = System.IO.Path.GetFileNameWithoutExtension(location.Path);
            var line = location.EndLinePosition.Line + 1;

            var methodDecl = call.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
            var methodName = methodDecl?.Identifier.Text ?? "<Main>$";

            entries.Add((file, methodName, line));
        }

        var switchCases = new StringBuilder();

        var abbreviatedLocations = entries
            .GroupBy(e => $"{System.IO.Path.GetFileNameWithoutExtension(e.file)}.{e.method}:L{e.line}")
            .Select(g => g.Select(i => new LocationWithAbbreviation { Location = $"{System.IO.Path.GetFileNameWithoutExtension(i.file)}.{i.method}:L{i.line}", Abbreviation = AbbreviateLocation(i) }).First())
            .ToArray();

        foreach (var g in abbreviatedLocations.GroupBy(al => al.Abbreviation))
        {
            if (g.Count() > 1)
            {
                int groupIndex = 1;
                foreach (var locationWithAbbreviation in g)
                {
                    locationWithAbbreviation.Abbreviation = locationWithAbbreviation.Abbreviation + "x" + groupIndex++;
                }
            }
        }

        int index = 0;
        foreach (var abbLocation in abbreviatedLocations)
        {
            if (index > 0)
            {
                switchCases.AppendLine();
            }
            switchCases.Append($"            case \"{abbLocation.Location}\": return \"#{abbLocation.Abbreviation}\";");
            index++;
        }

        var source = $$"""
                  using System;
                  using System.IO;
                  using System.Runtime.CompilerServices;
                  using Microsoft.EntityFrameworkCore;
                  using System.Linq;

                  internal static class AbbreviationTagExtensions
                  {
                      public static IQueryable<T> TagWithCallSiteAbbreviation<T>(
                          this IQueryable<T> query,
                          [CallerFilePath] string filePath = null,
                          [CallerMemberName] string memberName = null,
                          [CallerLineNumber] int lineNumber = 0)
                      {
                          var location = $"{Path.GetFileNameWithoutExtension(filePath)}.{memberName}:L{lineNumber}";
                          var hashTag = GetAbbreviationByLocation(location);
                          return query.TagWith(hashTag);
                      }
                  
                      private static string GetAbbreviationByLocation(string location)
                      {
                          switch (location)
                          {
                  {{switchCases}}
                              default: return location;
                          }
                      }
                  }
                  """;

        context.AddSource("EfAbbreviationTagExtensions.g.cs", SourceText.From(source, Encoding.UTF8));
    }

    private static string AbbreviateLocation((string file, string method, int line) location)
    {
        return AbbreviateWord(location.file) + AbbreviateWord(location.method) + location.line;
    }


    private const int ShortWord = 3;

    private static string AbbreviateWord(string word)
    {
        var normalized = Normalize(word);

        var wordLength = normalized.Length;

        if (wordLength < ShortWord)
        {
            return normalized.ToLowerInvariant();
        }

        var upper = new string(normalized.Where(char.IsUpper).ToArray());
        if (upper.Length > 0)
        {
            return upper.ToLowerInvariant();
        }

        var lowerNormalized = normalized.ToLowerInvariant();

        var consonants = lowerNormalized.Where(IsConsonant).ToArray();

        if (consonants.Length == 0)
        {
            return lowerNormalized;
        }

        return new string(consonants);
    }

    private static string Normalize(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
        {
            return string.Empty;
        }

        var result = new StringBuilder();

        foreach (var c in word.Where(char.IsLetterOrDigit))
        {
            result.Append(c);
        }

        return result.ToString();
    }

    private static readonly string Vowels =
        "aeiouy" +
        "àáâãäåæèéêëìíîïòóôõöøùúûüýÿ" +
        "аеёиіїыэоуюяє" +
        "αεηιουω";

    private static bool IsConsonant(char c)
    {
        var cat = CharUnicodeInfo.GetUnicodeCategory(c);
        if (cat != UnicodeCategory.UppercaseLetter && cat != UnicodeCategory.LowercaseLetter)
            return false;

        return !Vowels.AsSpan().Contains([c], StringComparison.InvariantCultureIgnoreCase);
    }

    private class LocationWithAbbreviation
    {
        public string Location { get; set; }
        public string Abbreviation { get; set; }
    }
}
