using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize]
public class LeadsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly AuditService _audit;
    public LeadsController(ApplicationDbContext db, AuditService audit) { _db = db; _audit = audit; }

    bool Priv => User.IsInRole("Admin") || User.IsInRole("Manager");
    IQueryable<Lead> Scoped() => Priv ? _db.Leads : _db.Leads.Where(l => l.AssignedTo == User.Identity!.Name);
    static string Snap(Lead l) => $"{l.LeadName}|{l.Email}|{l.Phone}|{l.CompanyName}|{l.Source}|{l.Status}|{l.ExpectedValue}|{l.AssignedTo}";

    static readonly Dictionary<string, string[]> Flow = new()
    {
        ["New"] = new[] { "Contacted", "Unqualified", "Lost" },
        ["Contacted"] = new[] { "Qualified", "Unqualified", "Lost" },
        ["Qualified"] = new[] { "Lost" },
        ["Unqualified"] = new[] { "Contacted", "Lost" },
        ["Converted"] = Array.Empty<string>(),
        ["Lost"] = new[] { "New" }
    };

    public async Task<IActionResult> Index(string? search, string? status)
    {
        var q = Scoped();
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(l => l.LeadName.Contains(search) || (l.CompanyName != null && l.CompanyName.Contains(search)) || (l.AssignedTo != null && l.AssignedTo.Contains(search)));
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(l => l.Status == status);
        ViewBag.Search = search; ViewBag.Status = status;
        return View(await q.OrderByDescending(l => l.LeadId).ToListAsync());
    }

    public IActionResult Create() => View("Form", new Lead());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("LeadName,Email,Phone,CompanyName,Source,Status,ExpectedValue,AssignedTo")] Lead l)
    {
        if (!Rules.LeadStatuses.Contains(l.Status) || l.Status == "Converted")
            ModelState.AddModelError("Status", "Invalid lead status.");
        if (!ModelState.IsValid) return View("Form", l);

        if (!Priv || string.IsNullOrWhiteSpace(l.AssignedTo)) l.AssignedTo = User.Identity!.Name;
        l.CreatedDate = DateTime.Now;
        _db.Leads.Add(l);
        await _db.SaveChangesAsync();
        l.LeadCode = $"LEAD-{l.LeadId:D4}";
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Create", "Lead", l.LeadId.ToString(), null, Snap(l));
        TempData["Msg"] = "Lead created.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var l = await Scoped().FirstOrDefaultAsync(x => x.LeadId == id);
        return l == null ? NotFound() : View("Form", l);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("LeadName,Email,Phone,CompanyName,Source,Status,ExpectedValue,AssignedTo")] Lead input)
    {
        var l = await Scoped().FirstOrDefaultAsync(x => x.LeadId == id);
        if (l == null) return NotFound();

        if (!Rules.LeadStatuses.Contains(input.Status))
            ModelState.AddModelError("Status", "Invalid lead status.");
        else if (input.Status != l.Status)
        {
            if (input.Status == "Converted")
                ModelState.AddModelError("Status", "Use the Convert button to convert a qualified lead.");
            else if (!Flow[l.Status].Contains(input.Status))
                ModelState.AddModelError("Status", $"Invalid status transition from {l.Status} to {input.Status}.");
        }
        if (!ModelState.IsValid) { input.LeadId = id; return View("Form", input); }

        var old = Snap(l);
        l.LeadName = input.LeadName; l.Email = input.Email; l.Phone = input.Phone;
        l.CompanyName = input.CompanyName; l.Source = input.Source;
        l.Status = input.Status; l.ExpectedValue = input.ExpectedValue;
        if (Priv && !string.IsNullOrWhiteSpace(input.AssignedTo)) l.AssignedTo = input.AssignedTo;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Update", "Lead", id.ToString(), old, Snap(l));
        TempData["Msg"] = "Lead updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Convert(int id)
    {
        var l = await Scoped().FirstOrDefaultAsync(x => x.LeadId == id);
        if (l == null) return NotFound();
        if (l.Status != "Qualified") { TempData["Msg"] = "Only Qualified leads can be converted."; return RedirectToAction(nameof(Index)); }
        if (l.ExpectedValue <= 0) { TempData["Msg"] = "Set an Expected Value greater than 0 before converting."; return RedirectToAction(nameof(Index)); }

        var cust = await _db.Customers.FirstOrDefaultAsync(c => c.Email == l.Email || c.Phone == l.Phone);
        if (cust == null)
        {
            cust = new Customer { CustomerName = l.LeadName, Email = l.Email, Phone = l.Phone, CompanyName = l.CompanyName, Status = "Active", CreatedBy = User.Identity!.Name };
            _db.Customers.Add(cust);
            await _db.SaveChangesAsync();
            cust.CustomerCode = $"CUST-{cust.CustomerId:D4}";
        }
        _db.Opportunities.Add(new Opportunity
        {
            OpportunityName = $"{l.LeadName} - Opportunity", CustomerId = cust.CustomerId, LeadId = l.LeadId,
            Amount = l.ExpectedValue, Stage = "Qualification", Probability = 20, Status = "Open",
            ExpectedCloseDate = DateTime.Today.AddDays(30), AssignedTo = l.AssignedTo
        });
        var old = Snap(l);
        l.Status = "Converted";
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Convert", "Lead", id.ToString(), old, $"Converted to customer {cust.CustomerId} + opportunity");
        TempData["Msg"] = "Lead converted to customer and opportunity.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Delete(int id)
    {
        var l = await _db.Leads.FindAsync(id);
        if (l == null) return NotFound();
        var old = Snap(l);
        _db.Leads.Remove(l);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Delete", "Lead", id.ToString(), old, null);
        TempData["Msg"] = "Lead deleted.";
        return RedirectToAction(nameof(Index));
    }
}