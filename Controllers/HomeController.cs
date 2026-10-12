using System.Diagnostics;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly ApplicationDbContext _db;
    public HomeController(ApplicationDbContext db) { _db = db; }

    public async Task<IActionResult> Index()
    {
        bool priv = User.IsInRole("Admin") || User.IsInRole("Manager");
        var me = User.Identity!.Name;

        var custQ = priv ? _db.Customers : _db.Customers.Where(c => c.CreatedBy == me);
        var leadQ = priv ? _db.Leads : _db.Leads.Where(l => l.AssignedTo == me);
        var oppQ = priv ? _db.Opportunities : _db.Opportunities.Where(o => o.AssignedTo == me);

        var leads = await leadQ.Select(l => l.Status).ToListAsync();
        var opps = await oppQ.Select(o => new { o.Stage, o.Status, o.Amount, o.ExpectedCloseDate }).ToListAsync();

        var vm = new DashboardVm
        {
            TotalCustomers = await custQ.CountAsync(),
            TotalLeads = leads.Count,
            OpenLeads = leads.Count(s => s != "Converted" && s != "Lost" && s != "Unqualified"),
            TotalOpps = opps.Count,
            OpenOpps = opps.Count(o => o.Status == "Open"),
            WonOpps = opps.Count(o => o.Status == "Won"),
            LostOpps = opps.Count(o => o.Status == "Lost"),
            PipelineValue = opps.Where(o => o.Status == "Open").Sum(o => o.Amount)
        };

        var leadChart = new[] { "New", "Contacted", "Qualified", "Lost", "Converted" };
        vm.LeadLabels = leadChart;
        vm.LeadCounts = leadChart.Select(s => leads.Count(x => x == s)).ToArray();

        vm.StageLabels = Rules.Stages;
        vm.StageCounts = Rules.Stages.Select(s => opps.Count(o => o.Stage == s)).ToArray();

        var months = Enumerable.Range(0, 6).Select(i => DateTime.Today.AddMonths(i - 5)).ToList();
        vm.MonthLabels = months.Select(m => m.ToString("MMM yyyy")).ToArray();
        vm.MonthTotals = months.Select(m => opps
            .Where(o => o.Status == "Won" && o.ExpectedCloseDate.Year == m.Year && o.ExpectedCloseDate.Month == m.Month)
            .Sum(o => o.Amount)).ToArray();

        return View(vm);
    }

    [AllowAnonymous]
    public IActionResult Privacy() => View();

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}