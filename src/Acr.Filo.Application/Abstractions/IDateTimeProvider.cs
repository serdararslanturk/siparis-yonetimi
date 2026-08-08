namespace Acr.Filo.Application.Abstractions;

/// <summary>Test edilebilirlik için zamanı soyutlar (parity testlerinde sabit tarih).</summary>
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
    DateOnly Today { get; }
}
