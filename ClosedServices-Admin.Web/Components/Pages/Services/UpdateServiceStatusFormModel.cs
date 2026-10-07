using ClosedServices_Admin.Data.Enums;

namespace ClosedServices_Admin.Components.Pages.Services
{
    public sealed class UpdateServiceStatusFormModel
    {
        public ClosureState SelectedClosureState { get; set; } = ClosureState.Closed;
        public Guid? SelectedClosureReason { get; set; }
        public string SelectedDuration { get; set; } = "custom";
        public DateOnly? StartDate { get; set; }
        public TimeOnly? StartTime { get; set; }
        public DateOnly? EndDate { get; set; }
        public TimeOnly? EndTime { get; set; }
        public string? Message { get; set; }
    }
}
