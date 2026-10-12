using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize]
public class FollowUpsController : Controller
{
    static readonly string[] Statuses = { "Planned", "Completed", "Missed", "Cancelled" };
    private readonly ApplicationDbContext _db;
    private readonly AuditService _audit;
    public FollowUpsController(ApplicationDbContext db, AuditService audit) { _db = db; _audit = audit; }

    bool Priv => User.IsInRole("Admin") || User.IsInRole("Manager");
    IQueryable<FollowUp> Scoped() =>
        (Priv ? _db.FollowUps : _db.FollowUps.Where(f => f.AssignedTo == User.Identity!.Name)).Include(f => f.Customer);
    async Task Load()
    {
        ViewBag.Customers = new SelectList(await _db.Customers.OrderBy(c => c.CustomerName).ToListAsync(), "CustomerId", "CustomerName");
        ViewBag.Statuses = new SelectList(Statuses);
    }
    static string Snap(FollowUp f) => $"{f.CustomerId}|{f.FollowUpDate:yyyy-MM-dd}|{f.FollowUpType}|{f.Status}|{f.Remarks}";

    public async Task<IActionResult> Index(string? status, DateTime? date, string? search)
    {
        var q = Scoped();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(f => f.Status == status);
        if (date != null) q = q.Where(f => f.FollowUpDate.Date == date.Value.Date);
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(f => (f.Customer != null && f.Customer.CustomerName.Contains(search)) || (f.AssignedTo != null && f.AssignedTo.Contains(search)));
        ViewBag.Status = status; ViewBag.Date = date; ViewBag.Search = search; ViewBag.Statuses = new SelectList(Statuses, (object?)status);
        return View(await q.OrderBy(f => f.FollowUpDate).ToListAsync());
    }

    public async Task<IActionResult> Create() { await Load(); return View("Form", new FollowUp()); }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("CustomerId,FollowUpDate,FollowUpType,Remarks,Status")] FollowUp f)
    {
        if (f.CustomerId == null || !await _db.Customers.AnyAsync(c => c.CustomerId == f.CustomerId))
            ModelState.AddModelError("CustomerId", "Select a valid customer.");
        if (!Statuses.Contains(f.Status)) ModelState.AddModelError("Status", "Invalid status.");
        if (!ModelState.IsValid) { await Load(); return View("Form", f); }
        f.AssignedTo = User.Identity!.Name;
        _db.FollowUps.Add(f);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Create", "FollowUp", f.FollowUpId.ToString(), null, Snap(f));
        TempData["Msg"] = "Follow-up scheduled.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var f = await Scoped().FirstOrDefaultAsync(x => x.FollowUpId == id);
        if (f == null) return NotFound();
        await Load();
        return View("Form", f);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("CustomerId,FollowUpDate,FollowUpType,Remarks,Status")] FollowUp input)
    {
        var f = await Scoped().FirstOrDefaultAsync(x => x.FollowUpId == id);
        if (f == null) return NotFound();
        if (input.CustomerId == null || !await _db.Customers.AnyAsync(c => c.CustomerId == input.CustomerId))
            ModelState.AddModelError("CustomerId", "Select a valid customer.");
        if (!Statuses.Contains(input.Status)) ModelState.AddModelError("Status", "Invalid status.");
        if (!ModelState.IsValid) { input.FollowUpId = id; await Load(); return View("Form", input); }
        var old = Snap(f);
        f.CustomerId = input.CustomerId; f.FollowUpDate = input.FollowUpDate; f.FollowUpType = input.FollowUpType;
        f.Remarks = input.Remarks; f.Status = input.Status;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Update", "FollowUp", id.ToString(), old, Snap(f));
        TempData["Msg"] = "Follow-up updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int id)
    {
        var f = await Scoped().FirstOrDefaultAsync(x => x.FollowUpId == id);
        if (f == null) return NotFound();
        var old = Snap(f);
        f.Status = "Completed";
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Update", "FollowUp", id.ToString(), old, Snap(f));
        TempData["Msg"] = "Follow-up completed.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Delete(int id)
    {
        var f = await _db.FollowUps.FindAsync(id);
        if (f == null) return NotFound();
        var old = Snap(f);
        _db.FollowUps.Remove(f);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Delete", "FollowUp", id.ToString(), old, null);
        TempData["Msg"] = "Follow-up deleted.";
        return RedirectToAction(nameof(Index));
    }
}