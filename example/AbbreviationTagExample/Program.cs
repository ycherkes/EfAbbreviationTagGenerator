using HashTagExample;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

await using var connection = new SqliteConnection("DataSource=:memory:");
connection.Open();

var options = new DbContextOptionsBuilder<TestDbContext>()
    .EnableSensitiveDataLogging()
    .LogTo(Console.WriteLine)
    .UseSqlite(connection)
    .Options;

var context = new TestDbContext(options);
context.Database.EnsureCreated();

var products = await context.Products
    .TagWithCallSiteAbbreviation()
    .ToArrayAsync();

foreach (var product in products)
{
    Console.WriteLine(product.Name);
}