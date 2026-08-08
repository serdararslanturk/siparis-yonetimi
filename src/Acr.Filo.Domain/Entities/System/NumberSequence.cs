namespace Acr.Filo.Domain.Entities.System;

/// <summary>
/// SQL: dbo.NumberSequences. Sipariş numarası sayacı.
/// Değer üretimi EF ile DEĞİL, dbo.sp_NextFleetOrderNo ile yapılır (UPDLOCK+HOLDLOCK).
/// Bu entity yalnızca okuma/migration amaçlı haritalanır.
/// </summary>
public class NumberSequence
{
    public string Key { get; set; } = null!;
    public short Year { get; set; }
    public int LastValue { get; set; }
}
