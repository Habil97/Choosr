using System.Threading;
using System.Threading.Tasks;
using Choosr.Domain.Entities;

namespace Choosr.Web.Services;

public interface IReportNotificationService
{
    Task NotifyStatusChangedAsync(ContentReport report, string? actorUserName, CancellationToken cancellationToken = default);
}
