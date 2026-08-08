namespace Acr.Filo.Domain.Enums;

/// <summary>
/// SSH hazırlık adımları. Sıra bağımlıdır: Plaka tamamlanmadan diğerleri yapılamaz
/// (frontend satır 1845 ile aynı kural).
/// VERİTABANI DEĞERLERİ KÜÇÜK HARFTİR: 'plaka','hgs','gps','utts'.
/// Enum ToString() ile YAZILMAZ — VehicleSshTaskConfiguration içindeki açık
/// ValueConverter kullanılır. Converter olmadan 'Plaka' yazılır, frontend kırılır.
/// </summary>
public enum SshTaskType
{
    Plaka = 1,
    Hgs   = 2,
    Gps   = 3,
    Utts  = 4
}

public static class SshTaskTypes
{
    public const string Plaka = "plaka";
    public const string Hgs   = "hgs";
    public const string Gps   = "gps";
    public const string Utts  = "utts";

    /// <summary>Her araçta bulunması gereken tam liste. Sıra = iş akışı sırası.</summary>
    public static readonly IReadOnlyList<SshTaskType> All = new[]
    {
        SshTaskType.Plaka, SshTaskType.Hgs, SshTaskType.Gps, SshTaskType.Utts
    };

    public static string ToDb(SshTaskType t) => t switch
    {
        SshTaskType.Plaka => Plaka,
        SshTaskType.Hgs   => Hgs,
        SshTaskType.Gps   => Gps,
        SshTaskType.Utts  => Utts,
        _ => throw new ArgumentOutOfRangeException(nameof(t), t, "Bilinmeyen SSH adimi")
    };

    public static SshTaskType FromDb(string v) => v switch
    {
        Plaka => SshTaskType.Plaka,
        Hgs   => SshTaskType.Hgs,
        Gps   => SshTaskType.Gps,
        Utts  => SshTaskType.Utts,
        _ => throw new ArgumentOutOfRangeException(nameof(v), v, "Bilinmeyen SSH adimi")
    };
}
