namespace Acr.Filo.Application.Common;

/// <summary>
/// Servis katmanı sonucu. Exception'ı akış kontrolü için kullanmak yerine
/// başarı/başarısızlığı açıkça taşır. Controller bunu HTTP durumuna çevirir.
/// </summary>
public class Result
{
    public bool Ok { get; protected set; }
    public string? Error { get; protected set; }
    public ResultCode Code { get; protected set; } = ResultCode.Ok;

    public static Result Success() => new() { Ok = true };
    public static Result Fail(string error, ResultCode code = ResultCode.BadRequest)
        => new() { Ok = false, Error = error, Code = code };
}

public sealed class Result<T> : Result
{
    public T? Value { get; private set; }
    public static Result<T> Success(T value) => new() { Ok = true, Value = value };
    public static new Result<T> Fail(string error, ResultCode code = ResultCode.BadRequest)
        => new() { Ok = false, Error = error, Code = code };
}

/// <summary>HTTP durumuna eşlenecek sonuç türü. Controller katmanı çevirir.</summary>
public enum ResultCode
{
    Ok = 0,
    BadRequest = 400,
    Unauthorized = 401,
    Forbidden = 403,
    NotFound = 404,
    Conflict = 409,          // rowversion çakışması + iş kuralı ihlali
    Validation = 422
}
