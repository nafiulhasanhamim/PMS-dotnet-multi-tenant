namespace PMS.SharedKernel.Common;

/// <summary>
/// A write lost a race against a unique constraint.
///
/// <para><b>Why this type exists.</b> A handler checks "does this batch number already exist?"
/// and then inserts, and between those two statements another request can insert the same
/// number. The check is not wrong — it is what produces a helpful message almost every time —
/// but the database is the only thing that can actually enforce uniqueness, so the constraint
/// will occasionally be the thing that refuses the write.</para>
///
/// <para>When that happens the raw failure is a <c>DbUpdateException</c> wrapping a
/// <c>SqlException</c> number 2601, which the Application layer must not know about: it holds
/// no reference to EF Core or to a database driver, deliberately. The persistence layer
/// translates at the save boundary and throws this instead, so a handler can catch a race and
/// return the same conflict message it would have returned had it won the check — rather than
/// letting a 500 and a SQL index name reach the screen.</para>
/// </summary>
public sealed class DuplicateKeyException : Exception
{
    public DuplicateKeyException(string? constraintName, Exception innerException)
        : base(
            constraintName is null
                ? "A record with the same unique value already exists."
                : $"A record violating '{constraintName}' already exists.",
            innerException)
    {
        ConstraintName = constraintName;
    }

    /// <summary>
    /// The index or constraint that refused the write, when the driver named it.
    ///
    /// <para>Useful when one table has several unique constraints: a handler can tell which
    /// rule was broken instead of guessing from the values it happened to be writing.</para>
    /// </summary>
    public string? ConstraintName { get; }
}
