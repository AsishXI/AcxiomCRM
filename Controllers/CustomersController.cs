using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize]
public class CustomersController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly AuditService _audit;
    public CustomersController(ApplicationDbContext db, AuditService audit) { _db = db; _audit = audit; }

    bool Privileged => User.IsInRole("Admin") || User.IsInRole("Manager");
    IQueryable<Customer> Scoped() =>
        Privileged ? _db.Customers : _db.Customers.Where(c => c.CreatedBy == User.Identity!.Name);

    static string Snap(Customer c) => $"{c.CustomerName}|{c.Email}|{c.Phone}|{c.CompanyName}|{c.City}|{c.State}|{c.Status}";

    public async Task<IActionResult> Index(string? search, string? status)
    {
        var q = Scoped();
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(c => c.CustomerName.Contains(search) || c.Email.Contains(search) ||
                             c.Phone.Contains(search) || (c.CompanyName != null && c.CompanyName.Contains(search)));
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(c => c.Status == status);
        ViewBag.Search = search; ViewBag.Status = status;
        return View(await q.OrderByDescending(c => c.CustomerId).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var c = await Scoped().FirstOrDefaultAsync(x => x.CustomerId == id);
        return c == null ? NotFound() : View(c);
    }

    public IActionResult Create() => View(new Customer());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("CustomerName,Email,Phone,CompanyName,Address,City,State,Status")] Customer c)
    {
        if (await _db.Customers.AnyAsync(x => x.Email == c.Email))
            ModelState.AddModelError("Email", "A customer with this email already exists.");
        if (await _db.Customers.AnyAsync(x => x.Phone == c.Phone))
            ModelState.AddModelError("Phone", "A customer with this phone already exists.");
        if (!ModelState.IsValid) return View(c);

        c.CreatedBy = User.Identity!.Name;
        c.CreatedDate = DateTime.Now;
        _db.Customers.Add(c);
        await _db.SaveChangesAsync();
        c.CustomerCode = $"CUST-{c.CustomerId:D4}";
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Create", "Customer", c.CustomerId.ToString(), null, Snap(c));
        TempData["Msg"] = "Customer created.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var c = await Scoped().FirstOrDefaultAsync(x => x.CustomerId == id);
        return c == null ? NotFound() : View(c);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("CustomerName,Email,Phone,CompanyName,Address,City,State,Status")] Customer input)
    {
        var c = await Scoped().FirstOrDefaultAsync(x => x.CustomerId == id);
        if (c == null) return NotFound();

        if (await _db.Customers.AnyAsync(x => x.Email == input.Email && x.CustomerId != id))
            ModelState.AddModelError("Email", "A customer with this email already exists.");
        if (await _db.Customers.AnyAsync(x => x.Phone == input.Phone && x.CustomerId != id))
            ModelState.AddModelError("Phone", "A customer with this phone already exists.");
        if (!ModelState.IsValid) { input.CustomerId = id; return View(input); }

        var old = Snap(c);
        c.CustomerName = input.CustomerName; c.Email = input.Email; c.Phone = input.Phone;
        c.CompanyName = input.CompanyName; c.Address = input.Address;
        c.City = input.City; c.State = input.State; c.Status = input.Status;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Update", "Customer", id.ToString(), old, Snap(c));
        TempData["Msg"] = "Customer updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Delete(int id)
    {
        var c = await _db.Customers.FindAsync(id);
        if (c == null) return NotFound();
        var old = Snap(c);
        _db.Customers.Remove(c);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Delete", "Customer", id.ToString(), old, null);
        TempData["Msg"] = "Customer deleted.";
        return RedirectToAction(nameof(Index));
    }
}