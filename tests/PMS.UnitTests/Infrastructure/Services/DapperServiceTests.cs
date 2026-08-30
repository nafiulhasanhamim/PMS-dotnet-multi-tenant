using PMS.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace PMS.UnitTests.Infrastructure.Services;

// Public interface for typed Dapper tests - must be public for Moq to create proxies
public interface ITestDbContext { }

/// <summary>
/// Tests for DapperService.
/// Note: Most Dapper methods require actual database connections.
/// These tests focus on constructor validation and utility methods.
/// </summary>
public class DapperServiceTests
{
    private readonly Mock<ILogger<DapperService>> _mockLogger;

    public DapperServiceTests()
    {
        _mockLogger = new Mock<ILogger<DapperService>>();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_ValidConnectionString_CreatesInstance()
    {
        // Arrange
        var connectionString = "Server=localhost;Database=TestDb;Trusted_Connection=True;";

        // Act
        var service = new DapperService(connectionString, _mockLogger.Object);

        // Assert
        service.Should().NotBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Constructor_NullOrEmptyConnectionString_ThrowsArgumentException(string? connectionString)
    {
        // Arrange & Act
        var act = () => new DapperService(connectionString!, _mockLogger.Object);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Connection string cannot be null or empty*");
    }

    [Fact]
    public void Constructor_WhitespaceConnectionString_CreatesInstance()
    {
        // Arrange - whitespace is technically not empty
        var connectionString = "   ";

        // Act
        var service = new DapperService(connectionString, _mockLogger.Object);

        // Assert - whitespace passes the check (would fail on actual connection)
        service.Should().NotBeNull();
    }

    #endregion

    #region Typed DapperService Tests

    [Fact]
    public void TypedConstructor_ValidConnectionString_CreatesInstance()
    {
        // Arrange
        var connectionString = "Server=localhost;Database=TestDb;Trusted_Connection=True;";
        var typedLogger = new Mock<ILogger<DapperService<ITestDbContext>>>();

        // Act
        var service = new DapperService<ITestDbContext>(connectionString, typedLogger.Object);

        // Assert
        service.Should().NotBeNull();
        service.Should().BeAssignableTo<DapperService>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void TypedConstructor_NullOrEmptyConnectionString_ThrowsArgumentException(string? connectionString)
    {
        // Arrange
        var typedLogger = new Mock<ILogger<DapperService<ITestDbContext>>>();

        // Act
        var act = () => new DapperService<ITestDbContext>(connectionString!, typedLogger.Object);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Connection string cannot be null or empty*");
    }

    #endregion

    #region SQL Truncation Tests (via reflection)

    [Fact]
    public void TruncateSql_ShortSql_ReturnsUnchanged()
    {
        // Arrange
        var shortSql = "SELECT * FROM Customers";

        // Act
        var result = InvokeTruncateSql(shortSql);

        // Assert
        result.Should().Be(shortSql);
    }

    [Fact]
    public void TruncateSql_LongSql_TruncatesTo200CharsWithEllipsis()
    {
        // Arrange
        var longSql = new string('X', 300);

        // Act
        var result = InvokeTruncateSql(longSql);

        // Assert
        result.Should().HaveLength(203); // 200 chars + "..."
        result.Should().EndWith("...");
        result[..200].Should().Be(new string('X', 200));
    }

    [Fact]
    public void TruncateSql_ExactlyMaxLength_ReturnsUnchanged()
    {
        // Arrange
        var exactSql = new string('Y', 200);

        // Act
        var result = InvokeTruncateSql(exactSql);

        // Assert
        result.Should().Be(exactSql);
        result.Should().HaveLength(200);
    }

    [Fact]
    public void TruncateSql_OneOverMaxLength_Truncates()
    {
        // Arrange
        var sql = new string('Z', 201);

        // Act
        var result = InvokeTruncateSql(sql);

        // Assert
        result.Should().HaveLength(203); // 200 + "..."
        result.Should().EndWith("...");
    }

    private static string InvokeTruncateSql(string sql)
    {
        // Use reflection to test private static method
        var method = typeof(DapperService).GetMethod(
            "TruncateSql",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        return (string)method!.Invoke(null, [sql])!;
    }

    #endregion
}
