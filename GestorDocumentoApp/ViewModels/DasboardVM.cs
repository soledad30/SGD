namespace GestorDocumentoApp.ViewModels
{
    public class DasboardVM
    {
        public List<ChangeRequestSummaryVM> ChangeRequestSummary { get; set; } = new();
        public List<ProjectConfigurationVM> ProjectConfigurations { get; set; } = new();
        public DashboardKpiVM Kpis { get; set; } = new();
    }

    public class DashboardKpiVM
    {
        public int TotalCr { get; set; }
        public int OpenCr { get; set; }
        public int BaselinedCr { get; set; }
        public int ApprovedCr { get; set; }
        public double AvgOpenAgeDays { get; set; }
        public double AvgBaselinedLeadTimeDays { get; set; }
    }

    public class ChangeRequestSummaryVM
    {
        public string ProjectName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int Total { get; set; }
    }

    public class ProjectConfigurationVM
    {
        public string ProjectName { get; set; } = string.Empty;
        public string ElementName { get; set; } = string.Empty;
        public string? LatestVersion { get; set; }
        public DateTime? VersionDate { get; set; }
        public string? Status { get; set; }
    }
}
