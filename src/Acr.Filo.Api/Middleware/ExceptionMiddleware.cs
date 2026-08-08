using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Acr.Filo.Api.Middleware;

/// <summary>
/// Merkezi hata yakalama. Production'da stack trace GİZLENİR (şartname madde 5).
/// ProblemDetails standardında yanıt döner. CorrelationId eklenir.
/// </summary>
public sealed class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _log;
    private readonly IHostEnvironment _env;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> log, IHostEnvironment env)
    { _next = next; _log = log; _env = env; }

    public async Task Invoke(HttpContext ctx)
    {
        var correlationId = ctx.TraceIdentifier;
        try
        {
            ctx.Response.Headers["X-Correlation-Id"] = correlationId;
            await _next(ctx);
        }
        catch (Exception ex)
        {
            // Şifre/token asla loglanmaz; burada yalnız exception mesajı + correlationId.
            _log.LogError(ex, "İşlenmeyen hata. CorrelationId={CorrelationId}", correlationId);

            var pd = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Beklenmeyen bir hata oluştu.",
                Detail = _env.IsDevelopment() ? ex.ToString() : "İşlem tamamlanamadı. Lütfen tekrar deneyin.",
                Type = "https://httpstatuses.com/500",
            };
            pd.Extensions["correlationId"] = correlationId;

            ctx.Response.StatusCode = pd.Status.Value;
            ctx.Response.ContentType = "application/problem+json";
            await ctx.Response.WriteAsync(JsonSerializer.Serialize(pd,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        }
    }
}
