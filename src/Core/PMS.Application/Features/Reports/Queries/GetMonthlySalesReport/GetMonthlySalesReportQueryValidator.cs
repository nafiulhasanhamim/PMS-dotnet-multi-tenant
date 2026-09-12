using FluentValidation;

namespace PMS.Application.Features.Reports.Queries.GetMonthlySalesReport;

/// <summary>
/// Month and year are validated rather than clamped.
///
/// <para><c>new DateOnly(year, 13, 1)</c> throws, and an unhandled exception on a report is a 500
/// where a 400 naming the problem belongs. Clamping would be worse still: a request for month 13
/// would silently return December, and the caller would never learn they asked wrongly.</para>
/// </summary>
public sealed class GetMonthlySalesReportQueryValidator
    : AbstractValidator<GetMonthlySalesReportQuery>
{
    public GetMonthlySalesReportQueryValidator()
    {
        RuleFor(q => q.Month)
            .InclusiveBetween(1, 12)
            .When(q => q.Month.HasValue)
            .WithMessage("Month must be between 1 and 12.");

        RuleFor(q => q.Year)
            .InclusiveBetween(2000, 2100)
            .When(q => q.Year.HasValue)
            .WithMessage("Year must be between 2000 and 2100.");
    }
}
