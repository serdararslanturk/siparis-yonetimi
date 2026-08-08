using Acr.Filo.Application.Abstractions;

namespace Acr.Filo.Infrastructure.Services;

public sealed class SystemClock : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateOnly Today => DateOnly.FromDateTime(DateTime.Now); // yerel gün (teslim tarihleri yerel)
}
