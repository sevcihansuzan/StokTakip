using System;
using System.Globalization;

namespace SideBar_Nav.Services
{
    public static class DateRangeHelper
    {
        private static readonly CultureInfo Culture = new("tr-TR");

        public static (DateTime Start, DateTime End) GetRange(string? selection, DateTime referenceDate)
        {
            selection = selection?.Trim();
            DateTime start;
            DateTime end;

            switch (selection)
            {
                case "Haftalık":
                    int diff = ((int)referenceDate.DayOfWeek + 6) % 7;
                    start = referenceDate.Date.AddDays(-diff);
                    end = start.AddDays(7);
                    break;
                case "Aylık":
                    start = new DateTime(referenceDate.Year, referenceDate.Month, 1);
                    end = start.AddMonths(1);
                    break;
                case "Yıllık":
                    start = new DateTime(referenceDate.Year, 1, 1);
                    end = start.AddYears(1);
                    break;
                case "Günlük":
                default:
                    start = referenceDate.Date;
                    end = start.AddDays(1);
                    break;
            }

            return (start, end);
        }

        public static (DateTime UtcStart, DateTime UtcEnd) GetUtcRange(string? selection, DateTime referenceDate, TimeZoneInfo? sourceTimeZone = null)
        {
            var (start, end) = GetRange(selection, referenceDate);
            sourceTimeZone ??= GetTurkeyTimeZone();

            start = DateTime.SpecifyKind(start, DateTimeKind.Unspecified);
            end = DateTime.SpecifyKind(end, DateTimeKind.Unspecified);

            return (
                TimeZoneInfo.ConvertTimeToUtc(start, sourceTimeZone),
                TimeZoneInfo.ConvertTimeToUtc(end, sourceTimeZone)
            );
        }

        public static string GetRangeDescription(string? selection, DateTime referenceDate)
        {
            var (start, endExclusive) = GetRange(selection, referenceDate);
            DateTime inclusiveEnd = endExclusive.AddDays(-1);

            return selection switch
            {
                "Haftalık" => string.Format(Culture, "{0:dd MMMM yyyy} - {1:dd MMMM yyyy}", start, inclusiveEnd),
                "Aylık" => string.Format(Culture, "{0:MMMM yyyy}", referenceDate),
                "Yıllık" => referenceDate.Year.ToString(Culture),
                _ => string.Format(Culture, "{0:dd MMMM yyyy}", referenceDate.Date)
            };
        }

        public static TimeZoneInfo GetTurkeyTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.Local;
            }
            catch (InvalidTimeZoneException)
            {
                return TimeZoneInfo.Local;
            }
        }
    }
}
