using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PMS.Application.Interfaces;
using PMS.Persistence.Contexts;
using PMS.SharedKernel.Interfaces;

namespace PMS.Persistence.Services;

/// <summary>
/// Per-pharmacy invoice numbering, allocated one number at a time by a single atomic statement.
///
/// <para><b>The statement is the whole design.</b> <c>UPDATE ... SET NextNumber = NextNumber + 1
/// OUTPUT deleted.NextNumber</c> reads and writes under one update lock, so two cashiers
/// completing a sale in the same instant get 452 and 453 — never both 452. A read followed by a
/// write would not do this however carefully it were written, because the gap between the two
/// is where the second cashier fits.</para>
///
/// <para><b>Why this table is not an EF entity.</b> It holds no business data and nothing ever
/// reads it except this class. Mapping it would give it a global tenant query filter that this
/// SQL would then have to bypass — and a raw statement that quietly circumvents the isolation
/// mechanism is exactly the pattern worth not establishing. Instead the tenant id is an
/// explicit parameter on every statement here, visible in three lines of SQL you can read at
/// once, taken from the tenant context rather than from a caller.</para>
///
/// <para><b>Gaps.</b> There are none in normal use: the allocation happens inside the sale
/// transaction, so a sale that fails rolls its number back too. That is the reason
/// <c>IUnitOfWork.ExecuteInTransactionAsync</c> exists.</para>
/// </summary>
public sealed class InvoiceNumberGenerator : IInvoiceNumberGenerator
{
    /// <summary>"INV-000452". Six digits, so a pharmacy has room for a million invoices.</summary>
    private const string Prefix = "INV-";
    private const string DigitFormat = "D6";

    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenantService _tenant;

    public InvoiceNumberGenerator(ApplicationDbContext context, ICurrentTenantService tenant)
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
                "An invoice number cannot be allocated without a pharmacy. The command should "
                + "carry ITenantScopedRequest, which refuses the request before it reaches here.");
        }

        // Two attempts is enough, and the second is only for the very first sale a pharmacy
        // ever makes. The UPDATE returns nothing when the counter row does not exist yet; the
        // INSERT then creates it, and if a concurrent sale created it first the loop comes back
        // round to the UPDATE, which is where every subsequent sale is served from.
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
            "Could not allocate an invoice number after seeding the counter. This should be "
            + "unreachable: it means the counter row neither existed nor could be created.");
    }

    /// <summary>
    /// Takes the current value and advances the counter in one statement.
    /// <c>deleted.NextNumber</c> is the pre-update value, which is the number being allocated.
    /// Returns null when the pharmacy has no counter row yet.
    /// </summary>
    private async Task<long?> AllocateAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        // The alias must be "Value": that is the column name EF Core binds a scalar
        // SqlQueryRaw result to, and anything else fails at materialisation rather than at
        // compile time.
        const string sql = """
            UPDATE [dbo].[InvoiceSequences]
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
    /// Creates the counter row for a pharmacy making its first sale, already advanced past the
    /// number the caller is about to use.
    ///
    /// <para>The <c>NOT EXISTS</c> guard rather than a bare INSERT: two first sales at once
    /// would otherwise race, and one would hit the primary key. Returning false when it did
    /// nothing sends the caller back to the UPDATE path, which is correct — the other sale has
    /// taken 1 and this one takes 2.</para>
    /// </summary>
    private async Task<bool> SeedAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO [dbo].[InvoiceSequences] ([TenantId], [NextNumber])
            SELECT @tenantId, 2
            WHERE NOT EXISTS (
                SELECT 1 FROM [dbo].[InvoiceSequences] WHERE [TenantId] = @tenantId)
            """;

        var affected = await _context.Database.ExecuteSqlRawAsync(
            sql, new object[] { new SqlParameter("@tenantId", tenantId) }, cancellationToken);

        return affected > 0;
    }
}
