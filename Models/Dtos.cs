using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models;

public class CustomerDto
{
    public int Id { get; set; }
    public string? CustomerCode { get; set; }

    [Required(ErrorMessage = "Customer Name is required."), StringLength(100)]
    public string Name { get; set; } = "";

    [Required, EmailAddress(ErrorMessage = "Enter a valid email address."), StringLength(150)]
    public string Email { get; set; } = "";

    [Required, RegularExpression(Rules.PhonePattern, ErrorMessage = Rules.PhoneMessage)]
    public string Phone { get; set; } = "";

    [StringLength(100)] public string? CompanyName { get; set; }
    [StringLength(250)] public string? Address { get; set; }
    [StringLength(60)] public string? City { get; set; }
    [StringLength(60)] public string? State { get; set; }
    [Required, RegularExpression("Active|Inactive", ErrorMessage = "Status must be Active or Inactive.")]
    public string Status { get; set; } = "Active";
}

public class LeadDto
{
    public int Id { get; set; }
    public string? LeadCode { get; set; }

    [Required(ErrorMessage = "Lead name is required."), StringLength(100)]
    public string Name { get; set; } = "";

    [Required, EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string Email { get; set; } = "";

    [Required, RegularExpression(Rules.PhonePattern, ErrorMessage = Rules.PhoneMessage)]
    public string Phone { get; set; } = "";

    [StringLength(100)] public string? CompanyName { get; set; }
    [StringLength(60)] public string? Source { get; set; }
    [Required] public string Status { get; set; } = "New";

    [Range(0, 100000000, ErrorMessage = "Expected value must be between 0 and 100000000.")]
    public decimal ExpectedValue { get; set; }
    public string? AssignedTo { get; set; }
}

public class OpportunityDto : IValidatableObject
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Opportunity name is required."), StringLength(100)]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "CustomerId is required.")] public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public decimal Amount { get; set; }
    [Required] public string Stage { get; set; } = "Qualification";

    [Range(0, 100, ErrorMessage = "Probability must be between 0 and 100.")]
    public int Probability { get; set; }

    public DateTime ExpectedCloseDate { get; set; }
    public string? Status { get; set; }
    public string? AssignedTo { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext ctx)
    {
        bool active = Stage != "Won" && Stage != "Lost";
        if (Amount < 0) yield return new ValidationResult("Opportunity Amount cannot be negative.", new[] { nameof(Amount) });
        if (active && Amount <= 0) yield return new ValidationResult("Opportunity Amount must be greater than 0.", new[] { nameof(Amount) });
        if (active && ExpectedCloseDate.Date < DateTime.Today)
            yield return new ValidationResult("Expected Close Date cannot be in the past.", new[] { nameof(ExpectedCloseDate) });
    }
}
