using HashTagExample.Model;
using Microsoft.EntityFrameworkCore;

namespace HashTagExample;

public class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products { get; set; }

    public DbSet<Category> Categories { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>().HasData(new Category { Id = 1, Name = "Fruit" });
        modelBuilder.Entity<Product>().HasData(
            new Product { Id = 1, Name = "Apple", Price = 10.0m, CategoryId = 1}, 
            new Product { Id = 2, Name = "Banana", Price = 15.0m , CategoryId = 1}, 
            new Product { Id = 3, Name = "Cherry", Price = 8.0m , CategoryId = 1});
    }
}