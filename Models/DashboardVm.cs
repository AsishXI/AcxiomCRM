namespace AcxiomCRM.Models;

public class DashboardVm
{
    public int TotalCustomers, TotalLeads, OpenLeads, TotalOpps, OpenOpps, WonOpps, LostOpps;
    public decimal PipelineValue;
    public string[] LeadLabels = Array.Empty<string>();
    public int[] LeadCounts = Array.Empty<int>();
    public string[] StageLabels = Array.Empty<string>();
    public int[] StageCounts = Array.Empty<int>();
    public string[] MonthLabels = Array.Empty<string>();
    public decimal[] MonthTotals = Array.Empty<decimal>();
}
