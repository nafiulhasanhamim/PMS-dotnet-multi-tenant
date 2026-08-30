using FluentAssertions;
using PMS.Domain.Entities;
using PMS.SchemaTests.Common;
using Xunit;

namespace PMS.SchemaTests;

/// <summary>
/// Baseline schema checks that hold no matter which entities exist.
///
/// The template's per-entity schema tests covered its sample domain (Customer, Product,
/// Order) and went with it. Add entity-specific tests here as PMS entities are built —
/// column types, indexes, relationships and query filters — following the patterns in
/// docs/COOKBOOK.md.
/// </summary>
public class ModelBuildsTests : IClassFixture<SchemaTestFixture>
{
    private readonly SchemaTestFixture _fixture;

    public ModelBuildsTests(SchemaTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Model_Builds_WithoutError()
    {
        // Proves every IEntityTypeConfiguration in the Persistence assembly is applied
        // without conflicting. Catches a broken configuration before it reaches a migration.
        _fixture.Model.Should().NotBeNull();
        _fixture.Model.GetEntityTypes().Should().NotBeEmpty();
    }

    [Fact]
    public void AuditTrail_IsMapped()
    {
        // AccessLog is the one entity the template ships that PMS keeps, written by
        // AccessLogBehavior on every request.
        _fixture.Model.FindEntityType(typeof(AccessLog)).Should().NotBeNull();
    }
}
