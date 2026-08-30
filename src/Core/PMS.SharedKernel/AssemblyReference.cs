namespace PMS.SharedKernel;

/// <summary>
/// Assembly reference marker for PMS.SharedKernel.
/// Used for assembly scanning in dependency injection and reflection operations.
/// </summary>
public static class AssemblyReference
{
    public static readonly System.Reflection.Assembly Assembly = typeof(AssemblyReference).Assembly;
}
