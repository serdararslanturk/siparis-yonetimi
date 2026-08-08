using Acr.Filo.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace Acr.Filo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>Result&lt;T&gt; -> uygun HTTP durumu. ResultCode doğrudan HTTP koduna eşlenir.</summary>
    protected IActionResult ToResponse<T>(Result<T> r)
        => r.Ok ? Ok(r.Value) : Fail(r.Error, (int)r.Code);

    protected IActionResult ToResponse(Result r)
        => r.Ok ? NoContent() : Fail(r.Error, (int)r.Code);

    protected IActionResult Fail(string? detail, int status)
    {
        var pd = new ProblemDetails
        {
            Detail = detail,
            Status = status,
            Title = status switch
            {
                400 => "Geçersiz istek",
                401 => "Kimlik doğrulama gerekli",
                403 => "Yetkiniz yok",
                404 => "Bulunamadı",
                409 => "Çakışma",
                422 => "Doğrulama hatası",
                _ => "Hata"
            }
        };
        return new ObjectResult(pd) { StatusCode = status, ContentTypes = { "application/problem+json" } };
    }
}
