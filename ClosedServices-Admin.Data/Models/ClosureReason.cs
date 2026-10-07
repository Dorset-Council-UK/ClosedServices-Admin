namespace ClosedServices_Admin.Data.Models
{
    public class ClosureReason
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Order { get; set; }
    }
}
