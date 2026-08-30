using System.Linq.Expressions;
using System.Reflection;

namespace PMS.SharedKernel.Grid;

/// <summary>
/// Builds LINQ expressions from grid filter and sort descriptors.
/// Used to apply grid state to IQueryable sources.
/// </summary>
public static class GridExpressionBuilder
{
    /// <summary>
    /// Applies filtering to the queryable based on the filter descriptor.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="query">The queryable to filter.</param>
    /// <param name="filter">The filter descriptor.</param>
    /// <returns>The filtered queryable.</returns>
    public static IQueryable<T> ApplyFilter<T>(this IQueryable<T> query, FilterDescriptor? filter)
    {
        if (filter is null)
        {
            return query;
        }

        var expression = BuildFilterExpression<T>(filter);
        return expression is null ? query : query.Where(expression);
    }

    /// <summary>
    /// Applies sorting to the queryable based on sort descriptors.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="query">The queryable to sort.</param>
    /// <param name="sorts">The sort descriptors.</param>
    /// <returns>The sorted queryable.</returns>
    public static IQueryable<T> ApplySort<T>(this IQueryable<T> query, List<SortDescriptor>? sorts)
    {
        if (sorts is null || sorts.Count == 0)
        {
            return query;
        }

        IOrderedQueryable<T>? orderedQuery = null;

        foreach (var sort in sorts)
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var property = GetPropertyExpression(parameter, sort.Field);

            if (property is null)
            {
                continue;
            }

            var lambda = Expression.Lambda(property, parameter);

            var methodName = orderedQuery is null
                ? (sort.IsDescending ? "OrderByDescending" : "OrderBy")
                : (sort.IsDescending ? "ThenByDescending" : "ThenBy");

            var method = typeof(Queryable).GetMethods()
                .First(m => m.Name == methodName && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(T), property.Type);

            orderedQuery = (IOrderedQueryable<T>)method.Invoke(null, [orderedQuery ?? query, lambda])!;
        }

        return orderedQuery ?? query;
    }

    /// <summary>
    /// Applies pagination to the queryable.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="query">The queryable to paginate.</param>
    /// <param name="skip">Number of items to skip.</param>
    /// <param name="take">Number of items to take.</param>
    /// <returns>The paginated queryable.</returns>
    public static IQueryable<T> ApplyPaging<T>(this IQueryable<T> query, int skip, int take)
    {
        if (skip > 0)
        {
            query = query.Skip(skip);
        }

        if (take > 0)
        {
            query = query.Take(take);
        }

        return query;
    }

    /// <summary>
    /// Applies the full grid request (filter, sort, paging) to the queryable.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="query">The queryable to process.</param>
    /// <param name="request">The grid request.</param>
    /// <returns>The processed queryable.</returns>
    public static IQueryable<T> ApplyGridRequest<T>(this IQueryable<T> query, GridRequest request)
    {
        return query
            .ApplyFilter(request.Filter)
            .ApplySort(request.Sort)
            .ApplyPaging(request.Skip, request.Take);
    }

    private static Expression<Func<T, bool>>? BuildFilterExpression<T>(FilterDescriptor filter)
    {
        var parameter = Expression.Parameter(typeof(T), "x");

        // Handle composite filters
        if (filter.Filters is { Count: > 0 })
        {
            Expression? combined = null;

            foreach (var childFilter in filter.Filters)
            {
                var childExpression = BuildFilterPredicate<T>(childFilter, parameter);

                if (childExpression is null)
                {
                    continue;
                }

                combined = combined is null
                    ? childExpression
                    : filter.Logic?.Equals("or", StringComparison.OrdinalIgnoreCase) == true
                        ? Expression.OrElse(combined, childExpression)
                        : Expression.AndAlso(combined, childExpression);
            }

            return combined is null ? null : Expression.Lambda<Func<T, bool>>(combined, parameter);
        }

        // Handle single filter
        var predicate = BuildFilterPredicate<T>(filter, parameter);
        return predicate is null ? null : Expression.Lambda<Func<T, bool>>(predicate, parameter);
    }

    private static Expression? BuildFilterPredicate<T>(FilterDescriptor filter, ParameterExpression parameter)
    {
        if (string.IsNullOrEmpty(filter.Field))
        {
            return null;
        }

        var property = GetPropertyExpression(parameter, filter.Field);

        if (property is null)
        {
            return null;
        }

        var value = ConvertValue(filter.Value, property.Type);
        var constant = Expression.Constant(value, property.Type);

        return filter.Operator?.ToLowerInvariant() switch
        {
            "eq" => Expression.Equal(property, constant),
            "neq" => Expression.NotEqual(property, constant),
            "gt" => Expression.GreaterThan(property, constant),
            "gte" => Expression.GreaterThanOrEqual(property, constant),
            "lt" => Expression.LessThan(property, constant),
            "lte" => Expression.LessThanOrEqual(property, constant),
            "contains" => BuildStringContainsExpression(property, filter.Value?.ToString()),
            "startswith" => BuildStringMethodExpression(property, "StartsWith", filter.Value?.ToString()),
            "endswith" => BuildStringMethodExpression(property, "EndsWith", filter.Value?.ToString()),
            "isnull" => Expression.Equal(property, Expression.Constant(null, property.Type)),
            "isnotnull" => Expression.NotEqual(property, Expression.Constant(null, property.Type)),
            _ => Expression.Equal(property, constant)
        };
    }

    private static Expression? GetPropertyExpression(Expression parameter, string propertyPath)
    {
        Expression? current = parameter;

        foreach (var propertyName in propertyPath.Split('.'))
        {
            var propertyInfo = current.Type.GetProperty(
                propertyName,
                BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);

            if (propertyInfo is null)
            {
                return null;
            }

            current = Expression.Property(current, propertyInfo);
        }

        return current;
    }

    private static Expression? BuildStringContainsExpression(Expression property, string? value)
    {
        if (value is null || property.Type != typeof(string))
        {
            return null;
        }

        var containsMethod = typeof(string).GetMethod("Contains", [typeof(string)])!;
        return Expression.Call(property, containsMethod, Expression.Constant(value));
    }

    private static Expression? BuildStringMethodExpression(Expression property, string methodName, string? value)
    {
        if (value is null || property.Type != typeof(string))
        {
            return null;
        }

        var method = typeof(string).GetMethod(methodName, [typeof(string)])!;
        return Expression.Call(property, method, Expression.Constant(value));
    }

    private static object? ConvertValue(object? value, Type targetType)
    {
        if (value is null)
        {
            return null;
        }

        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (underlyingType.IsEnum)
        {
            return Enum.Parse(underlyingType, value.ToString()!);
        }

        return Convert.ChangeType(value, underlyingType);
    }
}
