namespace Acr.Filo.Domain.Enums;

/// <summary>
/// Araç durumu SAKLANMAZ — hesaplanır. Frontend vehicleStatus() (satır 442) ve
/// SQL vw_VehicleStatus ile birebir aynı mantık. Değerler frontend'in beklediği
/// küçük harfli anahtarlardır.
/// </summary>
public static class VehicleStatuses
{
    public const string Overdue = "overdue";   // Gecikti
    public const string Soon    = "soon";      // Yaklaşıyor
    public const string Neutral = "neutral";   // Bekliyor
    public const string Ready   = "ready";     // Teslime hazır
    public const string Done    = "done";      // Tamamlandı

    /// <summary>Frontend STATUS_RANK (satır 440) birebir karşılığı.</summary>
    public static int Rank(string s) => s switch
    {
        Overdue => 4, Soon => 3, Neutral => 2, Ready => 1, Done => 0, _ => 2
    };

    /// <summary>Sipariş genel durumu = araçların en kötüsü (frontend orderOverallStatus, satır 484).</summary>
    public static string Worst(IEnumerable<string> statuses)
    {
        var worst = Done;
        var any = false;
        foreach (var s in statuses) { any = true; if (Rank(s) > Rank(worst)) worst = s; }
        return any ? worst : Neutral;   // araçsız sipariş → neutral (frontend satır 485)
    }
}
