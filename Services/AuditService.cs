using System.Security.Claims;
using AcxiomCRM.Data;
using AcxiomCRM.Models;

namespace AcxiomCRM.Services;

public class AuditService
{
    private readonly ApplicationDbContext _db;
    private readonly IHttpContextAccessor _http;
    public AuditService(ApplicationDbContext db, IHttpContextAccessor http) { _db = db; _http = http; }

    public async Task LogAsync(string action, string entity, string? recordId, string? oldV = null, string? newV = null)
    {
        var ctx = _http.HttpContext;
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = ctx?.User.FindFirstValue(ClaimTypes.NameIdentifier),
            UserName = ctx?.User.Identity?.Name,
            Action = action,
            EntityName = entity,
            RecordId = recordId,
            OldValue = oldV,
            NewValue = newV,
            IpAddress = ctx?.Connection.RemoteIpAddress?.ToString()
        });
        await _db.SaveChangesAsync();
    }
}