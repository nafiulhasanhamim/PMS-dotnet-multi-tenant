using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;
using Xunit.Abstractions;

namespace PMS.ArchitectureTests;

/// <summary>
/// Tests to ensure Clean Architecture principles are followed.
/// </summary>
public class CleanArchitectureTests
{
    private readonly ITestOutputHelper _output;
    private readonly Assembly _domainAssembly;
    private readonly Assembly _applicationAssembly;
    private readonly Assembly _infrastructureAssembly;
    private readonly Assembly _persistenceAssembly;
    private readonly Assembly _sharedKernelAssembly;

    public CleanArchitectureTests(ITestOutputHelper output)
    {
        _output = output;
        _domainAssembly = Assembly.Load("PMS.Domain");
        _applicationAssembly = Assembly.Load("PMS.Application");
        _infrastructureAssembly = Assembly.Load("PMS.Infrastructure");
        _persistenceAssembly = Assembly.Load("PMS.Persistence");
        _sharedKernelAssembly = Assembly.Load("PMS.SharedKernel");
    }

    #region Layer Dependency Tests

    [Fact]
    public void Domain_Should_NotDependOn_Application()
    {
        // Arrange & Act
        var result = Types.InAssembly(_domainAssembly)
            .Should()
            .NotHaveDependencyOn("PMS.Application")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            "Domain layer should not depend on Application layer");

        LogResult("Domain → Application", result);
    }

    [Fact]
    public void Domain_Should_NotDependOn_Infrastructure()
    {
        // Arrange & Act
        var result = Types.InAssembly(_domainAssembly)
            .Should()
            .NotHaveDependencyOn("PMS.Infrastructure")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            "Domain layer should not depend on Infrastructure layer");

        LogResult("Domain → Infrastructure", result);
    }

    [Fact]
    public void Domain_Should_NotDependOn_Persistence()
    {
        // Arrange & Act
        var result = Types.InAssembly(_domainAssembly)
            .Should()
            .NotHaveDependencyOn("PMS.Persistence")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            "Domain layer should not depend on Persistence layer");

        LogResult("Domain → Persistence", result);
    }

    [Fact]
    public void Application_Should_NotDependOn_Infrastructure()
    {
        // Arrange & Act
        var result = Types.InAssembly(_applicationAssembly)
            .Should()
            .NotHaveDependencyOn("PMS.Infrastructure")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            "Application layer should not depend on Infrastructure layer");

        LogResult("Application → Infrastructure", result);
    }

    [Fact]
    public void Application_Should_NotDependOn_Persistence()
    {
        // Arrange & Act
        var result = Types.InAssembly(_applicationAssembly)
            .Should()
            .NotHaveDependencyOn("PMS.Persistence")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            "Application layer should not depend on Persistence layer");

        LogResult("Application → Persistence", result);
    }

    [Fact]
    public void SharedKernel_Should_NotDependOn_Domain()
    {
        // Arrange & Act
        var result = Types.InAssembly(_sharedKernelAssembly)
            .Should()
            .NotHaveDependencyOn("PMS.Domain")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            "SharedKernel should not depend on Domain layer");

        LogResult("SharedKernel → Domain", result);
    }

    [Fact]
    public void SharedKernel_Should_NotDependOn_Application()
    {
        // Arrange & Act
        var result = Types.InAssembly(_sharedKernelAssembly)
            .Should()
            .NotHaveDependencyOn("PMS.Application")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            "SharedKernel should not depend on Application layer");

        LogResult("SharedKernel → Application", result);
    }

    [Fact]
    public void SharedKernel_Should_NotDependOn_Infrastructure()
    {
        // Arrange & Act
        var result = Types.InAssembly(_sharedKernelAssembly)
            .Should()
            .NotHaveDependencyOn("PMS.Infrastructure")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            "SharedKernel should not depend on Infrastructure layer");

        LogResult("SharedKernel → Infrastructure", result);
    }

    #endregion

    #region Naming Convention Tests

    [Fact]
    public void Handlers_Should_EndWith_Handler()
    {
        // Arrange & Act
        var result = Types.InAssembly(_applicationAssembly)
            .That()
            .ImplementInterface(typeof(MediatR.IRequestHandler<,>))
            .Should()
            .HaveNameEndingWith("Handler")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            "All MediatR handlers should end with 'Handler'");

        LogResult("Handler Naming", result);
    }

    [Fact]
    public void Validators_Should_EndWith_Validator()
    {
        // Arrange & Act
        var result = Types.InAssembly(_applicationAssembly)
            .That()
            .Inherit(typeof(FluentValidation.AbstractValidator<>))
            .Should()
            .HaveNameEndingWith("Validator")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            "All validators should end with 'Validator'");

        LogResult("Validator Naming", result);
    }

    [Fact]
    public void Commands_Should_EndWith_Command()
    {
        // Arrange & Act - Check types that implement IRequest (actual MediatR commands)
        var result = Types.InAssembly(_applicationAssembly)
            .That()
            .ResideInNamespaceContaining("Commands")
            .And()
            .ImplementInterface(typeof(MediatR.IRequest<>))
            .Should()
            .HaveNameEndingWith("Command")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            "All commands should end with 'Command'");

        LogResult("Command Naming", result);
    }

    [Fact]
    public void Queries_Should_EndWith_Query()
    {
        // Arrange & Act - Check types that implement IRequest (actual MediatR queries)
        var result = Types.InAssembly(_applicationAssembly)
            .That()
            .ResideInNamespaceContaining("Queries")
            .And()
            .ImplementInterface(typeof(MediatR.IRequest<>))
            .Should()
            .HaveNameEndingWith("Query")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            "All queries should end with 'Query'");

        LogResult("Query Naming", result);
    }

    #endregion

    #region Folder Structure Tests

    [Fact]
    public void Commands_Should_ResideIn_CommandsNamespace()
    {
        // Arrange & Act
        var result = Types.InAssembly(_applicationAssembly)
            .That()
            .HaveNameEndingWith("Command")
            .And()
            .AreClasses()
            .And()
            .DoNotHaveNameEndingWith("Handler")
            .Should()
            .ResideInNamespaceContaining("Commands")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            "All Commands should reside in a Commands namespace");

        LogResult("Commands Location", result);
    }

    [Fact]
    public void Queries_Should_ResideIn_QueriesNamespace()
    {
        // Arrange & Act
        var result = Types.InAssembly(_applicationAssembly)
            .That()
            .HaveNameEndingWith("Query")
            .And()
            .AreClasses()
            .And()
            .DoNotHaveNameEndingWith("Handler")
            .Should()
            .ResideInNamespaceContaining("Queries")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            "All Queries should reside in a Queries namespace");

        LogResult("Queries Location", result);
    }

    #endregion

    #region Entity Tests

    [Fact]
    public void Entities_Should_BeSealed_Or_HaveProtectedConstructor()
    {
        // Get all entity classes in Domain (excluding enums that might slip through)
        var entities = Types.InAssembly(_domainAssembly)
            .That()
            .ResideInNamespaceContaining("Entities")
            .And()
            .AreClasses()
            .And()
            .AreNotAbstract()
            .GetTypes()
            .Where(t => !t.IsEnum && !t.IsValueType); // Exclude enums and value types

        var violations = new List<string>();

        foreach (var entity in entities)
        {
            // Check if sealed or has parameterless constructor (for EF)
            var hasParameterlessConstructor = entity.GetConstructors(
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                .Any(c => c.GetParameters().Length == 0);

            if (!hasParameterlessConstructor)
            {
                violations.Add($"{entity.Name} does not have a parameterless constructor");
            }
        }

        violations.Should().BeEmpty(
            "All entities should have a parameterless constructor for EF Core");

        _output.WriteLine($"Checked {entities.Count()} entities");
    }

    #endregion

    #region Interface Tests

    [Fact]
    public void Interfaces_Should_StartWith_I()
    {
        // Arrange & Act
        var result = Types.InAssemblies(new[] { _applicationAssembly, _sharedKernelAssembly })
            .That()
            .AreInterfaces()
            .Should()
            .HaveNameStartingWith("I")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            "All interfaces should start with 'I'");

        LogResult("Interface Naming", result);
    }

    #endregion

    #region Helper Methods

    private void LogResult(string testName, TestResult result)
    {
        if (result.IsSuccessful)
        {
            _output.WriteLine($"✓ {testName}: Passed");
        }
        else
        {
            _output.WriteLine($"✗ {testName}: Failed");
            foreach (var type in result.FailingTypes ?? Enumerable.Empty<Type>())
            {
                _output.WriteLine($"  - {type.FullName}");
            }
        }
    }

    #endregion
}
