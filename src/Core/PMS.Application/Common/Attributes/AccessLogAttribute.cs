namespace PMS.Application.Common.Attributes;

/// <summary>
/// Marks a command/query for access logging.
/// When applied, the AccessLogBehavior will automatically log the action to the database.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class AccessLogAttribute : Attribute
{
    /// <summary>
    /// The action name (e.g., "Create", "Update", "Delete", "View").
    /// </summary>
    public string ActionName { get; }

    /// <summary>
    /// The entity name being accessed (e.g., "Supplier", "Sale").
    /// </summary>
    public string EntityName { get; }

    /// <summary>
    /// Creates a new AccessLogAttribute.
    /// </summary>
    /// <param name="actionName">The action being performed.</param>
    /// <param name="entityName">The entity being accessed.</param>
    public AccessLogAttribute(string actionName, string entityName)
    {
        ActionName = actionName;
        EntityName = entityName;
    }
}
