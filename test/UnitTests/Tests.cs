using EfAbbreviationTagGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using VerifyCS = UnitTests.CSharpSourceGeneratorVerifier<EfAbbreviationTagGenerator.AbbreviationTagGenerator>;

namespace UnitTests;

public class Tests
{
    [Fact]
    public async Task GeneratesTagWithCallSiteAbbreviation()
    {
        // Input source code
        var inputSource = """
                          using System.Collections.Generic;
                          using Microsoft.EntityFrameworkCore;

                          namespace MyApp.Entities
                          {
                              public class User
                              {
                                  public int Id { get; set; }
                                  public string Name { get; set; }
                                  public ICollection<Order> Orders { get; set; }
                                  
                                  public static User Create(int id, string name, ICollection<Order> orders)
                                  {
                                      return new User
                                      {
                                          Id = id,
                                          Name = name,
                                          Orders = orders
                                      };
                                  }
                              }
                          
                              public class Order
                              {
                                  public int Id { get; set; }
                                  public string Description { get; set; }
                              }
                          
                              public class MyDbContext : DbContext
                              {
                                  public DbSet<User> Users { get; set; }
                              }
                              
                              public static class Program
                              {
                                  public static void Main(MyDbContext dbContext)
                                  {
                                      var usrs = dbContext.Users.TagWithCallSiteAbbreviation();
                                      foreach(var usr in usrs)
                                      {
                                          System.Console.WriteLine(usr.Name);
                                      }
                                  }
                              }
                          }
                          """;

        var expectedGeneratedExtensionMethodSource =
            $$"""
              using System;
              using System.CodeDom.Compiler;
              using System.IO;
              using System.Runtime.CompilerServices;
              using Microsoft.EntityFrameworkCore;
              using System.Linq;

              [GeneratedCode("{{GeneratorInfo.Name}}", "{{GeneratorInfo.Version}}")]
              internal static class AbbreviationTagExtensions
              {
                  /// <summary>
                  /// Tags the query with a short abbreviation derived from the call site (file, method, and line number).
                  /// </summary>
                  /// <typeparam name="T">The type of elements in the query.</typeparam>
                  /// <param name="query">The source queryable to tag.</param>
                  /// <param name="filePath">The source file path of the call site (injected by the compiler).</param>
                  /// <param name="memberName">The member name of the call site (injected by the compiler).</param>
                  /// <param name="lineNumber">The line number of the call site (injected by the compiler).</param>
                  /// <returns>The query tagged with the abbreviated call site identifier.</returns>
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
                          case "Test0.Main:L38": return "#tm38";
                          default: return location;
                      }
                  }
              }
              """;

        // Configure the test
        var test = new VerifyCS.Test
        {
            TestState =
            {
                Sources = { inputSource },
                AdditionalReferences =
                {
                    MetadataReference.CreateFromFile(typeof(DbContext).Assembly.Location)
                },
                GeneratedSources =
                {
                    // Verify the generated sources
                    (typeof(AbbreviationTagGenerator), "EfAbbreviationTagExtensions.g.cs", expectedGeneratedExtensionMethodSource)
                }
            },
        };

        // Run the test
        await test.RunAsync();
    }
}