using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers.Api;

[ApiController, Authorize, Route("api/customers")]
public class CustomersApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly AuditService _audit;
    public CustomersApiController(ApplicationDbContext db, AuditService audit) { _db = db; _audit = audit; }

    bool Priv => User.IsInRole("Admin") || User.IsInRole("Manager");
    IQueryable<Customer> Scoped() => Priv ? _db.Customers : _db.Customers.Where(c => c.CreatedBy == User.Identity!.Name);
    static CustomerDto ToDto(Customer c) => new()
    {
        Id = c.CustomerId, CustomerCode = c.CustomerCode, Name = c.CustomerName, Email = c.Email, Phone = c.Phone,
        CompanyName = c.CompanyName, Address = c.Address, City = c.City, State = c.State, Status = c.Status
    };
    static string Snap(Customer c) => $"{c.CustomerName}|{c.Email}|{c.Phone}|{c.CompanyName}|{c.City}|{c.State}|{c.Status}";

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CustomerDto>>> Get([FromQuery] string? search)
    {
        var q = Scoped();
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(c => c.CustomerName.Contains(search) || c.Email.Contains(search) || c.Phone.Contains(search));
        return Ok((await q.OrderBy(c => c.CustomerId).ToListAsync()).Select(ToDto));
    }

    [HttpGet("{id:int}", Name = "GetCustomer")]
    public async Task<ActionResult<CustomerDto>> GetById(int id)
    {
        var c = await Scoped().FirstOrDefaultAsync(x => x.CustomerId == id);
        return c == null ? NotFound(new { error = "Customer not found." }) : Ok(ToDto(c));
    }

    [HttpPost]
    public async Task<ActionResult<CustomerDto>> Create(CustomerDto dto)
    {
        if (await _db.Customers.AnyAsync(x => x.Email == dto.Email || x.Phone == dto.Phone))
            return Conflict(new { error = "A customer with this email or phone already exists." });
        var c = new Customer
        {
            CustomerName = dto.Name, Email = dto.Email, Phone = dto.Phone, CompanyName = dto.CompanyName,
            Address = dto.Address, City = dto.City, State = dto.State, Status = dto.Status, CreatedBy = User.Identity!.Name
        };
        _db.Customers.Add(c);
        await _db.SaveChangesAsync();
        c.CustomerCode = $"CUST-{c.CustomerId:D4}";
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Create", "Customer", c.CustomerId.ToString(), null, Snap(c) + " (API)");
        return CreatedAtRoute("GetCustomer", new { id = c.CustomerId }, ToDto(c));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<CustomerDto>> Update(int id, CustomerDto dto)
    {
        var c = await Scoped().FirstOrDefaultAsync(x => x.CustomerId == id);
        if (c == null) return NotFound(new { error = "Customer not found." });
        if (await _db.Customers.AnyAsync(x => (x.Email == dto.Email || x.Phone == dto.Phone) && x.CustomerId != id))
            return Conflict(new { error = "A customer with this email or phone already exists." });
        var old = Snap(c);
        c.CustomerName = dto.Name; c.Email = dto.Email; c.Phone = dto.Phone; c.CompanyName = dto.CompanyName;
        c.Address = dto.Address; c.City = dto.City; c.State = dto.State; c.Status = dto.Status;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Update", "Customer", id.ToString(), old, Snap(c) + " (API)");
        return Ok(ToDto(c));
    }

    [HttpDelete("{id:int}"), Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Delete(int id)
    {
        var c = await _db.Customers.FindAsync(id);
        if (c == null) return NotFound(new { error = "Customer not found." });
        var old = Snap(c);
        _db.Customers.Remove(c);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Delete", "Customer", id.ToString(), old, "(API)");
        return NoContent();
    }
}

[ApiController, Authorize, Route("api/leads")]
public class LeadsApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly AuditService _audit;
    public LeadsApiController(ApplicationDbContext db, AuditService audit) { _db = db; _audit = audit; }

    bool Priv => User.IsInRole("Admin") || User.IsInRole("Manager");
    static LeadDto ToDto(Lead l) => new()
    {
        Id = l.LeadId, LeadCode = l.LeadCode, Name = l.LeadName, Email = l.Email, Phone = l.Phone,
        CompanyName = l.CompanyName, Source = l.Source, Status = l.Status, ExpectedValue = l.ExpectedValue, AssignedTo = l.AssignedTo
    };

    [HttpGet]
    public async Task<ActionResult<IEnumerable<LeadDto>>> Get([FromQuery] string? status)
    {
        var q = Priv ? _db.Leads : _db.Leads.Where(l => l.AssignedTo == User.Identity!.Name);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(l => l.Status == status);
        return Ok((await q.OrderBy(l => l.LeadId).ToListAsync()).Select(ToDto));
    }

    [HttpPost]
    public async Task<ActionResult<LeadDto>> Create(LeadDto dto)
    {
        if (!Rules.LeadStatuses.Contains(dto.Status) || dto.Status == "Converted")
            return BadRequest(new { error = "Invalid lead status.", allowed = Rules.LeadStatuses.Where(s => s != "Converted") });
        var l = new Lead
        {
            LeadName = dto.Name, Email = dto.Email, Phone = dto.Phone, CompanyName = dto.CompanyName, Source = dto.Source,
            Status = dto.Status, ExpectedValue = dto.ExpectedValue,
            AssignedTo = Priv && !string.IsNullOrWhiteSpace(dto.AssignedTo) ? dto.AssignedTo : User.Identity!.Name
        };
        _db.Leads.Add(l);
        await _db.SaveChangesAsync();
        l.LeadCode = $"LEAD-{l.LeadId:D4}";
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Create", "Lead", l.LeadId.ToString(), null, $"{l.LeadName}|{l.Status} (API)");
        return Created($"/api/leads/{l.LeadId}", ToDto(l));
    }
}

[ApiController, Authorize, Route("api/opportunities")]
public class OpportunitiesApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly AuditService _audit;
    public OpportunitiesApiController(ApplicationDbContext db, AuditService audit) { _db = db; _audit = audit; }

    bool Priv => User.IsInRole("Admin") || User.IsInRole("Manager");
    static OpportunityDto ToDto(Opportunity o) => new()
    {
        Id = o.OpportunityId, Name = o.OpportunityName, CustomerId = o.CustomerId, CustomerName = o.Customer?.CustomerName,
        Amount = o.Amount, Stage = o.Stage, Probability = o.Probability, ExpectedCloseDate = o.ExpectedCloseDate,
        Status = o.Status, AssignedTo = o.AssignedTo
    };

    [HttpGet]
    public async Task<ActionResult<IEnumerable<OpportunityDto>>> Get([FromQuery] string? stage)
    {
        var q = (Priv ? _db.Opportunities : _db.Opportunities.Where(o => o.AssignedTo == User.Identity!.Name)).Include(o => o.Customer);
        var list = string.IsNullOrWhiteSpace(stage) ? q : q.Where(o => o.Stage == stage);
        return Ok((await list.OrderBy(o => o.OpportunityId).ToListAsync()).Select(ToDto));
    }

    [HttpPost]
    public async Task<ActionResult<OpportunityDto>> Create(OpportunityDto dto)
    {
        if (!Rules.Stages.Contains(dto.Stage))
            return BadRequest(new { error = "Invalid stage.", allowed = Rules.Stages });
        if (!await _db.Customers.AnyAsync(c => c.CustomerId == dto.CustomerId))
            return BadRequest(new { error = "Customer does not exist." });
        var o = new Opportunity
        {
            OpportunityName = dto.Name, CustomerId = dto.CustomerId, Amount = dto.Amount, Stage = dto.Stage,
            Probability = dto.Probability, ExpectedCloseDate = dto.ExpectedCloseDate,
            Status = dto.Stage == "Won" ? "Won" : dto.Stage == "Lost" ? "Lost" : "Open",
            AssignedTo = User.Identity!.Name
        };
        _db.Opportunities.Add(o);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Create", "Opportunity", o.OpportunityId.ToString(), null, $"{o.OpportunityName}|{o.Amount}|{o.Stage} (API)");
        return Created($"/api/opportunities/{o.OpportunityId}", ToDto(o));
    }
}