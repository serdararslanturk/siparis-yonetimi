using Acr.Filo.Application.Audit;
using Acr.Filo.Application.Auth;
using Acr.Filo.Application.Definitions;
using Acr.Filo.Application.Orders;
using Acr.Filo.Application.Reports;
using Acr.Filo.Application.Users;
using Acr.Filo.Application.Abstractions;
using Acr.Filo.Infrastructure.Auth;
using Acr.Filo.Infrastructure.Auditing;
using Acr.Filo.Infrastructure.Identity;
using Acr.Filo.Infrastructure.Persistence;
using Acr.Filo.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Acr.Filo.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration cfg)
    {
        var conn = cfg.GetConnectionString("FiloDb");
        if (string.IsNullOrWhiteSpace(conn))
            throw new InvalidOperationException(
                "ConnectionStrings:FiloDb boş. appsettings.Production.json veya ENV ConnectionStrings__FiloDb ayarlayın.");

        services.AddScoped<AuditSaveChangesInterceptor>();

        services.AddDbContext<FiloDbContext>((sp, opt) =>
        {
            opt.UseSqlServer(conn, sql =>
            {
                // sql.EnableRetryOnFailure(...); // KALDIRILDI: elle transaction ile cakisiyordu (siparis olusturma 500 hatasi)
                sql.CommandTimeout(30);
            });
            opt.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());
        });

        // Options
        services.Configure<JwtOptions>(cfg.GetSection("Jwt"));
        services.Configure<SecurityOptions>(cfg.GetSection("Security"));

        // Altyapı
        services.AddSingleton<IDateTimeProvider, SystemClock>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();

        // Servisler
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IDefinitionService, DefinitionService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IAuditService, AuditService>();

        return services;
    }
}
