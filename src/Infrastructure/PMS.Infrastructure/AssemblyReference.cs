namespace PMS.Infrastructure;

/// <summary>
/// Assembly reference marker for PMS.Infrastructure.
/// Used for assembly scanning in dependency injection and reflection operations.
/// </summary>
public static class AssemblyReference
{
    public static readonly System.Reflection.Assembly Assembly = typeof(AssemblyReference).Assembly;
}
