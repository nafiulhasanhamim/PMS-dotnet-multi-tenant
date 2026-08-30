using PMS.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace PMS.SchemaTests.Common;

/// <summary>
/// Test fixture providing an ApplicationDbContext for schema tests.
/// Uses in-memory database for fast schema validation without requiring a real database.
/// </summary>
public class SchemaTestFixture : IDisposable
{
    private readonly ApplicationDbContext _context;

    public SchemaTestFixture()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"SchemaTests_{Guid.NewGuid():N}")
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();
    }

    /// <summary>
    /// Gets the ApplicationDbContext for testing.
    /// </summary>
    public ApplicationDbContext Context => _context;

    /// <summary>
    /// Gets the EF Core model for schema inspection.
    /// </summary>
    public Microsoft.EntityFrameworkCore.Metadata.IModel Model => _context.Model;

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}
