using ClosedServices_Admin.Data.Models;
using ClosedServices_Admin.Data.Services;
using NodaTime;

namespace ClosedServices_Admin.Tests;

public class ServiceOperatingHoursServiceTests
{
    private static readonly DateTimeZone UkZone = DateTimeZoneProviders.Tzdb["Europe/London"];

    [Fact]
    public void Evaluate_UsesWeekdayFallback_WhenNoOperatingDaysConfigured()
    {
        var service = new Service();
        var sut = new ServiceOperatingHoursService();
        var now = ToInstant(2025, 1, 6, 7, 30); // Monday, before default opening

        var evaluation = sut.Evaluate(service, now);

        Assert.NotNull(evaluation.TodayPeriod);
        Assert.True(evaluation.IsTodayOperationalBeforeClose);
        Assert.True(evaluation.IsBeforeTodayOpening);
        Assert.False(evaluation.IsWithinTodayOperatingHours);
        Assert.Equal(new LocalTime(8, 0), evaluation.TodayPeriod!.OpenTime);
        Assert.Equal(new LocalTime(17, 0), evaluation.TodayPeriod.CloseTime);
    }

    [Fact]
    public void Evaluate_UsesConfiguredOperatingDays_WhenProvided()
    {
        var service = new Service
        {
            OperatingDays =
            [
                new ServiceOperatingDay
                {
                    DayOfWeek = DayOfWeek.Wednesday,
                    IsOpen = true,
                    OpenTime = new TimeOnly(9, 0),
                    CloseTime = new TimeOnly(12, 0)
                }
            ]
        };

        var sut = new ServiceOperatingHoursService();
        var now = ToInstant(2025, 1, 7, 10, 0); // Tuesday

        var evaluation = sut.Evaluate(service, now);

        Assert.Null(evaluation.TodayPeriod);
        Assert.NotNull(evaluation.NextOperatingPeriod);
        Assert.Equal(IsoDayOfWeek.Wednesday, evaluation.NextOperatingPeriod!.Date.DayOfWeek);
        Assert.Equal(new LocalTime(9, 0), evaluation.NextOperatingPeriod.OpenTime);
        Assert.Equal(new LocalTime(12, 0), evaluation.NextOperatingPeriod.CloseTime);
    }

    [Fact]
    public void Evaluate_AfterClosing_SelectsNextOperationalDay()
    {
        var service = new Service();
        var sut = new ServiceOperatingHoursService();
        var now = ToInstant(2025, 1, 6, 18, 0); // Monday, after default closing

        var evaluation = sut.Evaluate(service, now);

        Assert.False(evaluation.IsTodayOperationalBeforeClose);
        Assert.NotNull(evaluation.NextOperatingPeriod);
        Assert.Equal(IsoDayOfWeek.Tuesday, evaluation.NextOperatingPeriod!.Date.DayOfWeek);
    }

    [Fact]
    public void FormatOperatingDayLabel_UsesTomorrowOtherwiseLongDate()
    {
        var sut = new ServiceOperatingHoursService();
        var referenceDate = new LocalDate(2025, 1, 10); // Friday

        var tomorrowLabel = sut.FormatOperatingDayLabel(referenceDate.PlusDays(1), referenceDate);
        var nextWeekLabel = sut.FormatOperatingDayLabel(referenceDate.PlusDays(3), referenceDate);

        Assert.Equal("Tomorrow", tomorrowLabel);
        Assert.Equal("Monday 13th January", nextWeekLabel);
    }

    [Fact]
    public void Evaluate_ProvidesStandaloneNextOperatingDayPeriod()
    {
        var service = new Service();
        var sut = new ServiceOperatingHoursService();
        var now = ToInstant(2025, 1, 10, 10, 0); // Friday

        var evaluation = sut.Evaluate(service, now);

        Assert.NotNull(evaluation.NextOperatingPeriodAfterToday);
        Assert.Equal(IsoDayOfWeek.Monday, evaluation.NextOperatingPeriodAfterToday!.Date.DayOfWeek);
        Assert.Equal(new LocalTime(8, 0), evaluation.NextOperatingPeriodAfterToday.OpenTime);
        Assert.Equal(new LocalTime(17, 0), evaluation.NextOperatingPeriodAfterToday.CloseTime);
        Assert.True(evaluation.NextOperatingPeriodAfterToday.OpenInstant < evaluation.NextOperatingPeriodAfterToday.CloseInstant);
    }

    private static Instant ToInstant(int year, int month, int day, int hour, int minute)
    {
        var local = new LocalDateTime(year, month, day, hour, minute);
        return local.InZoneLeniently(UkZone).ToInstant();
    }
}
