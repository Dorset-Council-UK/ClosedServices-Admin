using ClosedServices_Admin.Data.Models;
using NodaTime;
using System.Globalization;

namespace ClosedServices_Admin.Data.Services
{
    public class ServiceOperatingHoursService : IServiceOperatingHoursService
    {
        private static readonly DateTimeZone UkTimeZone = DateTimeZoneProviders.Tzdb["Europe/London"];
        private static readonly LocalTime DefaultOpenTime = new(8, 0);
        private static readonly LocalTime DefaultCloseTime = new(17, 0);

        public ServiceOperatingHoursEvaluation Evaluate(Service service, Instant now)
        {
            ArgumentNullException.ThrowIfNull(service);

            var localNow = now.InZone(UkTimeZone);
            var schedule = BuildSchedule(service.OperatingDays);

            var todayDate = localNow.Date;
            var todayPeriod = TryGetPeriodForDate(todayDate, schedule);

            var isWithinTodayHours = todayPeriod is not null
                && now >= todayPeriod.OpenInstant
                && now <= todayPeriod.CloseInstant;

            var isBeforeTodayOpening = todayPeriod is not null && now < todayPeriod.OpenInstant;
            var isTodayOperationalBeforeClose = todayPeriod is not null && now < todayPeriod.CloseInstant;

            var nextPeriod = FindNextOperatingPeriod(now, todayDate, schedule);
            var nextPeriodAfterToday = FindNextOperatingPeriodAfterToday(todayDate, schedule);

            return new(
                localNow,
                todayPeriod,
                isWithinTodayHours,
                isBeforeTodayOpening,
                isTodayOperationalBeforeClose,
                nextPeriod,
                nextPeriodAfterToday);
        }

        public string FormatOperatingDayLabel(LocalDate targetDate, LocalDate referenceDate)
        {
            if (targetDate == referenceDate)
            {
                return "Today";
            }

            if (targetDate == referenceDate.PlusDays(1))
            {
                return "Tomorrow";
            }

            return $"{targetDate.DayOfWeek} {targetDate.Day}{GetOrdinalSuffix(targetDate.Day)} {targetDate.ToDateTimeUnspecified():MMMM}";
        }

        private static Dictionary<IsoDayOfWeek, (LocalTime Open, LocalTime Close)> BuildSchedule(ICollection<ServiceOperatingDay> operatingDays)
        {
            if (operatingDays.Count == 0)
            {
                return new Dictionary<IsoDayOfWeek, (LocalTime Open, LocalTime Close)>
                {
                    [IsoDayOfWeek.Monday] = (DefaultOpenTime, DefaultCloseTime),
                    [IsoDayOfWeek.Tuesday] = (DefaultOpenTime, DefaultCloseTime),
                    [IsoDayOfWeek.Wednesday] = (DefaultOpenTime, DefaultCloseTime),
                    [IsoDayOfWeek.Thursday] = (DefaultOpenTime, DefaultCloseTime),
                    [IsoDayOfWeek.Friday] = (DefaultOpenTime, DefaultCloseTime)
                };
            }

            return operatingDays
                .Where(day => day.IsOpen && day.OpenTime.HasValue && day.CloseTime.HasValue && day.OpenTime.Value < day.CloseTime.Value)
                .GroupBy(day => ToIsoDay(day.DayOfWeek))
                .ToDictionary(
                    group => group.Key,
                    group =>
                    {
                        var entry = group.First();
                        return (ToLocalTime(entry.OpenTime!.Value), ToLocalTime(entry.CloseTime!.Value));
                    });
        }

        private static OperatingPeriod? TryGetPeriodForDate(LocalDate date, IReadOnlyDictionary<IsoDayOfWeek, (LocalTime Open, LocalTime Close)> schedule)
        {
            if (!schedule.TryGetValue(date.DayOfWeek, out var times))
            {
                return null;
            }

            var openLocal = date.At(times.Open).InZoneLeniently(UkTimeZone).ToInstant();
            var closeLocal = date.At(times.Close).InZoneLeniently(UkTimeZone).ToInstant();

            return new OperatingPeriod(date, times.Open, times.Close, openLocal, closeLocal);
        }

        private static OperatingPeriod? FindNextOperatingPeriod(Instant now, LocalDate startDate, IReadOnlyDictionary<IsoDayOfWeek, (LocalTime Open, LocalTime Close)> schedule)
        {
            for (var offset = 0; offset <= 14; offset++)
            {
                var candidateDate = startDate.PlusDays(offset);
                var period = TryGetPeriodForDate(candidateDate, schedule);
                if (period is null)
                {
                    continue;
                }

                if (now <= period.CloseInstant)
                {
                    return period;
                }
            }

            return null;
        }

        private static OperatingPeriod? FindNextOperatingPeriodAfterToday(LocalDate todayDate, IReadOnlyDictionary<IsoDayOfWeek, (LocalTime Open, LocalTime Close)> schedule)
        {
            for (var offset = 1; offset <= 14; offset++)
            {
                var candidateDate = todayDate.PlusDays(offset);
                var period = TryGetPeriodForDate(candidateDate, schedule);
                if (period is not null)
                {
                    return period;
                }
            }

            return null;
        }

        private static IsoDayOfWeek ToIsoDay(DayOfWeek day)
        {
            return day switch
            {
                DayOfWeek.Monday => IsoDayOfWeek.Monday,
                DayOfWeek.Tuesday => IsoDayOfWeek.Tuesday,
                DayOfWeek.Wednesday => IsoDayOfWeek.Wednesday,
                DayOfWeek.Thursday => IsoDayOfWeek.Thursday,
                DayOfWeek.Friday => IsoDayOfWeek.Friday,
                DayOfWeek.Saturday => IsoDayOfWeek.Saturday,
                DayOfWeek.Sunday => IsoDayOfWeek.Sunday,
                _ => throw new ArgumentOutOfRangeException(nameof(day), day, null)
            };
        }

        private static LocalTime ToLocalTime(TimeOnly time) => new(time.Hour, time.Minute, time.Second);

        private static string GetOrdinalSuffix(int day)
        {
            if (day % 100 is >= 11 and <= 13)
            {
                return "th";
            }

            return (day % 10) switch
            {
                1 => "st",
                2 => "nd",
                3 => "rd",
                _ => "th"
            };
        }
    }
}
