using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Settings;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Alerts.Queries.GetAlertSummary;

/// <summary>
/// The counts behind the dashboard cards and the sidebar badge.
///
/// <para>The expiry window is the pharmacy's own since Module 10. It travels back on the DTO as
/// well as into the query, because the dashboard says "within N days" and the number it prints
/// has to be the number it counted with.</para>
/// </summary>
public sealed class GetAlertSummaryQueryHandler
    : IRequestHandler<GetAlertSummaryQuery, Result<AlertSummaryDto>>
{
    private readonly IAlertQueries _alerts;
    private readonly ISettingsService _settings;

    public GetAlertSummaryQueryHandler(IAlertQueries alerts, ISettingsService settings)
    {
        _alerts = alerts;
        _settings = settings;
    }

    public async Task<Result<AlertSummaryDto>> Handle(
        GetAlertSummaryQuery request, CancellationToken cancellationToken)
    {
        var window = await _settings.GetIntAsync(
            SettingKeys.ExpiryAlertWindowDays, cancellationToken);

        return Result.Success(
            await _alerts.GetAlertSummaryAsync(window, cancellationToken));
    }
}
