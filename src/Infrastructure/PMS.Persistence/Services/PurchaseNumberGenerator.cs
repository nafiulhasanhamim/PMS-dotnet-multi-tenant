using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PMS.Application.Interfaces;
using PMS.Persistence.Contexts;
using PMS.SharedKernel.Interfaces;

namespace PMS.Persistence.Services;

/// <summary>
/// Per-pharmacy purchase numbering, allocated one number at a time by a single atomic statement.
///
/// <para><b>Deliberately not "count the purchases and add one".</b> Two deliveries booked in at
/// the same moment would both read the same count and both claim PUR-000124; the unique index
/// would then reject one of them, and a real purchase would be lost to a race. The
/// <c>UPDATE ... OUTPUT deleted.NextNumber</c> below reads and writes under one update lock, so
/// the two get 124 and 125.</para>
///
/// <para>This is the same design as <c>InvoiceNumberGenerator</c>, against its own counter table,
/// and the duplication is intentional: a shared "sequence service" would need a sequence-name
/// parameter threaded through every call site to buy nothing, and the two sequences have no
/// reason to stay identical to each other.</para>
///
/// <para><b>Gaps.</b> None in normal use: allocation happens inside the purchase transaction, so a
/// purchase that fails rolls its number back with everything else.</para>
/// </summary>
public sealed class PurchaseNumberGenerator : IPurchaseNumberGenerator
{
    /// <summary>"PUR-000124". Six digits, so a pharmacy has room for a million deliveries.</summary>
    private const string Prefix = "PUR-";
    private const string DigitFormat = "D6";

    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenantService _tenant;

    public PurchaseNumberGenerator(ApplicationDbContext context, ICurrentTenantService tenant)
    {
        _context = context;
        _tenant = tenant;
    }

    public async Task<string> NextAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenant.TenantId;

        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "A purchase number cannot be allocated without a pharmacy. The command should "
                + "carry ITenantScopedRequest, which refuses the request before it reaches here.");
        }

        // Two attempts, and the second only ever runs for a pharmacy's very first purchase. The
        // UPDATE returns nothing when the counter row does not exist; the INSERT then creates it,
        // and if a concurrent purchase created it first the loop returns to the UPDATE.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var allocated = await AllocateAsync(tenantId, cancellationToken);

            if (allocated is not null)
            {
                return Prefix + allocated.Value.ToString(DigitFormat);
            }

            var seeded = await SeedAsync(tenantId, cancellationToken);

            if (seeded)
            {
                return Prefix + 1.ToString(DigitFormat);
            }
        }

        throw new InvalidOperationException(
            "Could not allocate a purchase number after seeding the counter. This should be "
            + "unreachable: it means the counter row neither existed nor could be created.");
    }

    /// <summary>
    /// Takes the current value and advances the counter in one statement.
    /// <c>deleted.NextNumber</c> is the pre-update value, which is the number being allocated.
    /// </summary>
    private async Task<long?> AllocateAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        // The alias must be "Value": that is the column name EF Core binds a scalar
        // SqlQueryRaw result to, and anything else fails at materialisation rather than at
        // compile time.
        const string sql = """
            UPDATE [dbo].[PurchaseSequences]
            SET [NextNumber] = [NextNumber] + 1
            OUTPUT deleted.[NextNumber] AS [Value]
            WHERE [TenantId] = @tenantId
            """;

        var rows = await _context.Database
            .SqlQueryRaw<long>(sql, new SqlParameter("@tenantId", tenantId))
            .ToListAsync(cancellationToken);

        return rows.Count > 0 ? rows[0] : null;
    }

    /// <summary>
    /// Creates the counter row for a pharmacy recording its first purchase, already advanced past
    /// the number the caller is about to use. The <c>NOT EXISTS</c> guard rather than a bare
    /// INSERT so that two first purchases at once do not collide on the primary key.
    /// </summary>
    private async Task<bool> SeedAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO [dbo].[PurchaseSequences] ([TenantId], [NextNumber])
            SELECT @tenantId, 2
            WHERE NOT EXISTS (
                SELECT 1 FROM [dbo].[PurchaseSequences] WHERE [TenantId] = @tenantId)
            """;

        var affected = await _context.Database.ExecuteSqlRawAsync(
            sql, new object[] { new SqlParameter("@tenantId", tenantId) }, cancellationToken);

        return affected > 0;
    }
}
