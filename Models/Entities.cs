using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models;

public static class Rules
{
    public const string PhonePattern = @"^[6-9]\d{9}$";
    public const string PhoneMessage = "Enter a valid 10-digit phone number.";
    public static readonly string[] LeadStatuses = { "New", "Contacted", "Qualified", "Unqualified", "Converted", "Lost" };
    public static readonly string[] Stages = { "Qualification", "Proposal", "Negotiation", "Won", "Lost" };
}

public class Customer
{
    public int CustomerId { get; set; }
    [StringLength(20)] public string? CustomerCode { get; set; }

    [Required(ErrorMessage = "Customer Name is required."), StringLength(100)]
    public string CustomerName { get; set; } = "";

    [Required, EmailAddress(ErrorMessage = "Enter a valid email address."), StringLength(150)]
    public string Email { get; set; } = "";

    [Required, RegularExpression(Rules.PhonePattern, ErrorMessage = Rules.PhoneMessage)]
    public string Phone { get; set; } = "";

    [StringLength(100)] public string? CompanyName { get; set; }
    [StringLength(250)] public string? Address { get; set; }
    [StringLength(60)] public string? City { get; set; }
    [StringLength(60)] public string? State { get; set; }
    [Required] public string Status { get; set; } = "Active";
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public string? CreatedBy { get; set; }
}

public class Lead
{
    public int LeadId { get; set; }
    [StringLength(20)] public string? LeadCode { get; set; }

    [Required(ErrorMessage = "Lead name is required."), StringLength(100)]
    public string LeadName { get; set; } = "";

    [Required, EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string Email { get; set; } = "";

    [Required, RegularExpression(Rules.PhonePattern, ErrorMessage = Rules.PhoneMessage)]
    public string Phone { get; set; } = "";

    [StringLength(100)] public string? CompanyName { get; set; }
    [StringLength(60)] public string? Source { get; set; }
    [Required] public string Status { get; set; } = "New";

    [Range(0, 100000000, ErrorMessage = "Expected value must be between 0 and 100000000.")]
    public decimal ExpectedValue { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public string? AssignedTo { get; set; }
}

public class Opportunity : IValidatableObject
{
    public int OpportunityId { get; set; }

    [Required(ErrorMessage = "Opportunity name is required."), StringLength(100)]
    public string OpportunityName { get; set; } = "";

    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int? LeadId { get; set; }

    [Range(0.01, 100000000, ErrorMessage = "Opportunity Amount must be greater than 0.")]

    public decimal Amount { get; set; }

    [Required] public string Stage { get; set; } = "Qualification";

    [Range(0, 100, ErrorMessage = "Probability must be between 0 and 100.")]
    public int Probability { get; set; }

    [DataType(DataType.Date)] public DateTime ExpectedCloseDate { get; set; } = DateTime.Today.AddDays(30);
    [Required] public string Status { get; set; } = "Open";
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public string? AssignedTo { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext ctx)
    {
        bool active = Status == "Open" && Stage != "Won" && Stage != "Lost";
        if (Amount < 0)
            yield return new ValidationResult("Opportunity Amount cannot be negative.", new[] { nameof(Amount) });
        if (active && Amount <= 0)
            yield return new ValidationResult("Opportunity Amount must be greater than 0.", new[] { nameof(Amount) });
        if (active && ExpectedCloseDate.Date < DateTime.Today)
            yield return new ValidationResult("Expected Close Date cannot be in the past.", new[] { nameof(ExpectedCloseDate) });
    }
}

public class FollowUp : IValidatableObject
{
    public int FollowUpId { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int? LeadId { get; set; }

    [DataType(DataType.Date)] public DateTime FollowUpDate { get; set; } = DateTime.Today;
    [Required, StringLength(40)] public string FollowUpType { get; set; } = "Call";
    [StringLength(500)] public string? Remarks { get; set; }
    [Required] public string Status { get; set; } = "Planned";
    public string? AssignedTo { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext ctx)
    {
        if (Status == "Planned" && FollowUpDate.Date < DateTime.Today)
            yield return new ValidationResult("Follow-up date cannot be earlier than today.", new[] { nameof(FollowUpDate) });
    }
}

public class Activity
{
    public int ActivityId { get; set; }
    [Required] public string ActivityType { get; set; } = "Call";
    [Required, StringLength(150)] public string Subject { get; set; } = "";
    [StringLength(500)] public string? Description { get; set; }
    [DataType(DataType.Date)] public DateTime ActivityDate { get; set; } = DateTime.Today;
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int? LeadId { get; set; }
    public string? AssignedTo { get; set; }
    [Required] public string Status { get; set; } = "Open";
}

public class AuditLog
{
    public int AuditLogId { get; set; }
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string Action { get; set; } = "";
    public string EntityName { get; set; } = "";
    public string? RecordId { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public string? IpAddress { get; set; }
}