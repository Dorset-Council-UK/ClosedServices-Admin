using ClosedServices_Admin.Data.Models;
using NodaTime;

namespace ClosedServices_Admin.Data.Services
{
    public interface IServiceOperatingHoursService
    {
        ServiceOperatingHoursEvaluation Evaluate(Service service, Instant now);
        string FormatOperatingDayLabel(LocalDate targetDate, LocalDate referenceDate);
    }

    public sealed record OperatingPeriod(
        LocalDate Date,
        LocalTime OpenTime,
        LocalTime CloseTime,
        Instant OpenInstant,
        Instant CloseInstant);

    public sealed record ServiceOperatingHoursEvaluation(
        ZonedDateTime LocalNow,
        OperatingPeriod? TodayPeriod,
        bool IsWithinTodayOperatingHours,
        bool IsBeforeTodayOpening,
        bool IsTodayOperationalBeforeClose,
        OperatingPeriod? NextOperatingPeriod,
        OperatingPeriod? NextOperatingPeriodAfterToday);
}
