using AcxiomCRM.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize(Roles = "Admin")]
public class AuditController : Controller
{
    private readonly ApplicationDbContext _db;
    public AuditController(ApplicationDbContext db) { _db = db; }

    public async Task<IActionResult> Index(string? user, string? entity, string? action, DateTime? from, DateTime? to)
    {
        var q = _db.AuditLogs.AsQueryable();
        if (!string.IsNullOrWhiteSpace(user)) q = q.Where(a => a.UserName != null && a.UserName.Contains(user));
        if (!string.IsNullOrWhiteSpace(entity)) q = q.Where(a => a.EntityName == entity);
        if (!string.IsNullOrWhiteSpace(action)) q = q.Where(a => a.Action == action);
        if (from != null) q = q.Where(a => a.CreatedDate >= from.Value.Date);
        if (to != null) q = q.Where(a => a.CreatedDate < to.Value.Date.AddDays(1));
        ViewBag.User = user; ViewBag.Entity = entity; ViewBag.Action = action; ViewBag.From = from; ViewBag.To = to;
        return View(await q.OrderByDescending(a => a.AuditLogId).Take(300).ToListAsync());
    }
}