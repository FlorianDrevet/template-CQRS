using Web.Template.CQRS.Application.Common.Interfaces.Services;

namespace Web.Template.CQRS.Infrastructure.Services;

public class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow {
        get
        {
            TimeZoneInfo parisTimeZone = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
            DateTime parisTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, parisTimeZone);
            return TimeZoneInfo.ConvertTimeToUtc(parisTime, parisTimeZone);
        }
    }
}