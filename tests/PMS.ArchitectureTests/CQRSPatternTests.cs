using System.Reflection;
using FluentAssertions;
using FluentValidation;
using MediatR;
using NetArchTest.Rules;
using Xunit;
using Xunit.Abstractions;

namespace PMS.ArchitectureTests;

/// <summary>
/// Tests to enforce CQRS pattern conventions.
/// </summary>
public class CQRSPatternTests
{
    private readonly ITestOutputHelper _output;
    private readonly Assembly _applicationAssembly;
    private readonly Assembly _domainAssembly;

    public CQRSPatternTests(ITestOutputHelper output)
    {
        _output = output;
        _applicationAssembly = Assembly.Load("PMS.Application");
        _domainAssembly = Assembly.Load("PMS.Domain");
    }

    #region Command Tests

    [Fact]
    public void Commands_Should_NotReturnDomainEntities()
    {
        // Arrange
        var commandTypes = Types.InAssembly(_applicationAssembly)
            .That()
            .HaveNameEndingWith("Command")
            .And()
            .AreClasses()
            .And()
            .DoNotHaveNameEndingWith("CommandHandler")
            .And()
            .DoNotHaveNameEndingWith("CommandValidator")
            .GetTypes();

        var domainEntityTypes = Types.InAssembly(_domainAssembly)
            .That()
            .ResideInNamespaceContaining("Entities")
            .And()
            .AreClasses()
            .GetTypes()
            .ToHashSet();

        var violations = new List<string>();

        foreach (var commandType in commandTypes)
        {
            var iRequestInterface = commandType.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType &&
                    i.GetGenericTypeDefinition() == typeof(IRequest<>));

            if (iRequestInterface != null)
            {
                var returnType = iRequestInterface.GetGenericArguments()[0];
                var actualReturnType = UnwrapResultType(returnType);

                if (domainEntityTypes.Contains(actualReturnType))
                {
                    violations.Add($"{commandType.Name} returns domain entity {actualReturnType.Name}");
                }
            }
        }

        // Assert
        violations.Should().BeEmpty(
            "Commands should return DTOs, not domain entities");

        _output.WriteLine($"Checked {commandTypes.Count()} commands");
    }

    [Fact]
    public void Commands_Should_ImplementIRequest()
    {
        // Arrange
        var commandTypes = Types.InAssembly(_applicationAssembly)
            .That()
            .HaveNameEndingWith("Command")
            .And()
            .AreClasses()
            .And()
            .DoNotHaveNameEndingWith("CommandHandler")
            .And()
            .DoNotHaveNameEndingWith("CommandValidator")
            .GetTypes();

        var violations = new List<string>();

        foreach (var commandType in commandTypes)
        {
            var implementsIRequest = commandType.GetInterfaces()
                .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>));

            if (!implementsIRequest)
            {
                violations.Add(commandType.Name);
            }
        }

        // Assert
        violations.Should().BeEmpty(
            "All Commands should implement IRequest<T>");

        _output.WriteLine($"Checked {commandTypes.Count()} commands");
    }

    #endregion

    #region Query Tests

    [Fact]
    public void Queries_Should_NotReturnDomainEntities()
    {
        // Arrange
        var queryTypes = Types.InAssembly(_applicationAssembly)
            .That()
            .HaveNameEndingWith("Query")
            .And()
            .AreClasses()
            .And()
            .DoNotHaveNameEndingWith("QueryHandler")
            .And()
            .DoNotHaveNameEndingWith("QueryValidator")
            .GetTypes();

        var domainEntityTypes = Types.InAssembly(_domainAssembly)
            .That()
            .ResideInNamespaceContaining("Entities")
            .And()
            .AreClasses()
            .GetTypes()
            .ToHashSet();

        var violations = new List<string>();

        foreach (var queryType in queryTypes)
        {
            var iRequestInterface = queryType.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType &&
                    i.GetGenericTypeDefinition() == typeof(IRequest<>));

            if (iRequestInterface != null)
            {
                var returnType = iRequestInterface.GetGenericArguments()[0];
                var actualReturnType = UnwrapResultType(returnType);

                if (domainEntityTypes.Contains(actualReturnType))
                {
                    violations.Add($"{queryType.Name} returns domain entity {actualReturnType.Name}");
                }
            }
        }

        // Assert
        violations.Should().BeEmpty(
            "Queries should return DTOs, not domain entities");

        _output.WriteLine($"Checked {queryTypes.Count()} queries");
    }

    [Fact]
    public void Queries_Should_ImplementIRequest()
    {
        // Arrange
        var queryTypes = Types.InAssembly(_applicationAssembly)
            .That()
            .HaveNameEndingWith("Query")
            .And()
            .AreClasses()
            .And()
            .DoNotHaveNameEndingWith("QueryHandler")
            .GetTypes();

        var violations = new List<string>();

        foreach (var queryType in queryTypes)
        {
            var implementsIRequest = queryType.GetInterfaces()
                .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>));

            if (!implementsIRequest)
            {
                violations.Add(queryType.Name);
            }
        }

        // Assert
        violations.Should().BeEmpty(
            "All Queries should implement IRequest<T>");

        _output.WriteLine($"Checked {queryTypes.Count()} queries");
    }

    #endregion

    #region Handler Tests

    [Fact]
    public void EachCommand_Should_HaveHandler()
    {
        // Arrange
        var commandTypes = Types.InAssembly(_applicationAssembly)
            .That()
            .HaveNameEndingWith("Command")
            .And()
            .AreClasses()
            .And()
            .DoNotHaveNameEndingWith("CommandHandler")
            .And()
            .DoNotHaveNameEndingWith("CommandValidator")
            .GetTypes()
            .ToList();

        var handlerTypes = Types.InAssembly(_applicationAssembly)
            .That()
            .HaveNameEndingWith("CommandHandler")
            .And()
            .AreClasses()
            .GetTypes()
            .ToList();

        // Get the command types that handlers handle
        var handledCommandTypes = new HashSet<Type>();
        foreach (var handlerType in handlerTypes)
        {
            var iRequestHandlerInterface = handlerType.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType &&
                    i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>));

            if (iRequestHandlerInterface != null)
            {
                var commandType = iRequestHandlerInterface.GetGenericArguments()[0];
                handledCommandTypes.Add(commandType);
            }
        }

        var commandsWithoutHandlers = commandTypes
            .Where(c => !handledCommandTypes.Contains(c))
            .Select(c => c.Name)
            .ToList();

        // Assert
        commandsWithoutHandlers.Should().BeEmpty(
            "Each Command should have a corresponding Handler");

        _output.WriteLine($"Checked {commandTypes.Count} commands, {handlerTypes.Count} handlers");
    }

    [Fact]
    public void EachQuery_Should_HaveHandler()
    {
        // Arrange
        var queryTypes = Types.InAssembly(_applicationAssembly)
            .That()
            .HaveNameEndingWith("Query")
            .And()
            .AreClasses()
            .And()
            .DoNotHaveNameEndingWith("QueryHandler")
            .GetTypes()
            .ToList();

        var handlerTypes = Types.InAssembly(_applicationAssembly)
            .That()
            .HaveNameEndingWith("QueryHandler")
            .And()
            .AreClasses()
            .GetTypes()
            .ToList();

        // Get the query types that handlers handle
        var handledQueryTypes = new HashSet<Type>();
        foreach (var handlerType in handlerTypes)
        {
            var iRequestHandlerInterface = handlerType.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType &&
                    i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>));

            if (iRequestHandlerInterface != null)
            {
                var queryType = iRequestHandlerInterface.GetGenericArguments()[0];
                handledQueryTypes.Add(queryType);
            }
        }

        var queriesWithoutHandlers = queryTypes
            .Where(q => !handledQueryTypes.Contains(q))
            .Select(q => q.Name)
            .ToList();

        // Assert
        queriesWithoutHandlers.Should().BeEmpty(
            "Each Query should have a corresponding Handler");

        _output.WriteLine($"Checked {queryTypes.Count} queries, {handlerTypes.Count} handlers");
    }

    #endregion

    #region Validator Tests

    [Fact]
    public void EachCommand_Should_HaveValidator()
    {
        // Arrange
        var commandTypes = Types.InAssembly(_applicationAssembly)
            .That()
            .HaveNameEndingWith("Command")
            .And()
            .AreClasses()
            .And()
            .DoNotHaveNameEndingWith("CommandHandler")
            .And()
            .DoNotHaveNameEndingWith("CommandValidator")
            .GetTypes()
            .ToList();

        var validatorTypes = Types.InAssembly(_applicationAssembly)
            .That()
            .Inherit(typeof(AbstractValidator<>))
            .And()
            .AreClasses()
            .GetTypes()
            .ToList();

        // Get the command types that validators validate
        var validatedCommandTypes = new HashSet<Type>();
        foreach (var validatorType in validatorTypes)
        {
            var baseType = validatorType.BaseType;
            while (baseType != null &&
                (!baseType.IsGenericType ||
                    baseType.GetGenericTypeDefinition() != typeof(AbstractValidator<>)))
            {
                baseType = baseType.BaseType;
            }

            if (baseType != null)
            {
                var validatedType = baseType.GetGenericArguments()[0];
                validatedCommandTypes.Add(validatedType);
            }
        }

        var commandsWithoutValidators = commandTypes
            .Where(c => !validatedCommandTypes.Contains(c))
            .Select(c => c.Name)
            .ToList();

        // Assert
        commandsWithoutValidators.Should().BeEmpty(
            "Each Command should have a corresponding Validator");

        _output.WriteLine($"Checked {commandTypes.Count} commands, {validatorTypes.Count} validators");
    }

    #endregion

    #region Helper Methods

    private static Type UnwrapResultType(Type type)
    {
        // Handle Result<T> pattern
        if (type.IsGenericType && type.Name.StartsWith("Result"))
        {
            return type.GetGenericArguments()[0];
        }

        // Handle IEnumerable<T>, List<T>, etc.
        if (type.IsGenericType && typeof(System.Collections.IEnumerable).IsAssignableFrom(type))
        {
            return type.GetGenericArguments()[0];
        }

        return type;
    }

    #endregion
}
