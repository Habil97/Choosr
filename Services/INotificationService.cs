using System.Threading;
using System.Threading.Tasks;
using Choosr.Domain.Entities;

namespace Choosr.Web.Services;

public interface INotificationService
{
    Task CreateAsync(string userName, string title, string? body = null, string? linkUrl = null, CancellationToken cancellationToken = default);
}
