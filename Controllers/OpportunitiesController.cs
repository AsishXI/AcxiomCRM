using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize]
public class OpportunitiesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly AuditService _audit;
    public OpportunitiesController(ApplicationDbContext db, AuditService audit) { _db = db; _audit = audit; }

    bool Priv => User.IsInRole("Admin") || User.IsInRole("Manager");
    IQueryable<Opportunity> Scoped() =>
        (Priv ? _db.Opportunities : _db.Opportunities.Where(o => o.AssignedTo == User.Identity!.Name)).Include(o => o.Customer);
    async Task LoadCustomers() =>
        ViewBag.Customers = new SelectList(await _db.Customers.OrderBy(c => c.CustomerName).ToListAsync(), "CustomerId", "CustomerName");
    static string Snap(Opportunity o) => $"{o.OpportunityName}|{o.CustomerId}|{o.Amount}|{o.Stage}|{o.Probability}|{o.ExpectedCloseDate:yyyy-MM-dd}|{o.Status}";
    static string StatusFor(string stage) => stage == "Won" ? "Won" : stage == "Lost" ? "Lost" : "Open";

    public async Task<IActionResult> Index(string? search, string? stage)
    {
        var q = Scoped();
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(o => o.OpportunityName.Contains(search) || (o.Customer != null && o.Customer.CustomerName.Contains(search)));
        if (!string.IsNullOrWhiteSpace(stage)) q = q.Where(o => o.Stage == stage);
        ViewBag.Search = search; ViewBag.Stage = stage;
        return View(await q.OrderByDescending(o => o.OpportunityId).ToListAsync());
    }

    public async Task<IActionResult> Create() { await LoadCustomers(); return View("Form", new Opportunity()); }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("OpportunityName,CustomerId,Amount,Stage,Probability,ExpectedCloseDate")] Opportunity o)
    {
        if (o.CustomerId == null || !await _db.Customers.AnyAsync(c => c.CustomerId == o.CustomerId))
            ModelState.AddModelError("CustomerId", "Select a valid customer.");
        if (!Rules.Stages.Contains(o.Stage)) ModelState.AddModelError("Stage", "Invalid stage.");
        if (!ModelState.IsValid) { await LoadCustomers(); return View("Form", o); }

        o.Status = StatusFor(o.Stage);
        o.AssignedTo = User.Identity!.Name;
        o.CreatedDate = DateTime.Now;
        _db.Opportunities.Add(o);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Create", "Opportunity", o.OpportunityId.ToString(), null, Snap(o));
        TempData["Msg"] = "Opportunity created.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var o = await Scoped().FirstOrDefaultAsync(x => x.OpportunityId == id);
        if (o == null) return NotFound();
        await LoadCustomers();
        return View("Form", o);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("OpportunityName,CustomerId,Amount,Stage,Probability,ExpectedCloseDate")] Opportunity input)
    {
        var o = await Scoped().FirstOrDefaultAsync(x => x.OpportunityId == id);
        if (o == null) return NotFound();
        if (input.CustomerId == null || !await _db.Customers.AnyAsync(c => c.CustomerId == input.CustomerId))
            ModelState.AddModelError("CustomerId", "Select a valid customer.");
        if (!Rules.Stages.Contains(input.Stage)) ModelState.AddModelError("Stage", "Invalid stage.");
        if (!ModelState.IsValid) { input.OpportunityId = id; await LoadCustomers(); return View("Form", input); }

        var old = Snap(o);
        o.OpportunityName = input.OpportunityName; o.CustomerId = input.CustomerId; o.Amount = input.Amount;
        o.Stage = input.Stage; o.Probability = input.Probability; o.ExpectedCloseDate = input.ExpectedCloseDate;
        o.Status = StatusFor(o.Stage);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Update", "Opportunity", id.ToString(), old, Snap(o));
        TempData["Msg"] = "Opportunity updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Delete(int id)
    {
        var o = await _db.Opportunities.FindAsync(id);
        if (o == null) return NotFound();
        var old = Snap(o);
        _db.Opportunities.Remove(o);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Delete", "Opportunity", id.ToString(), old, null);
        TempData["Msg"] = "Opportunity deleted.";
        return RedirectToAction(nameof(Index));
    }
}