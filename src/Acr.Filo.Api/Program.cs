using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Acr.Filo.Api;
using Acr.Filo.Api.Auth;
using Acr.Filo.Api.Middleware;
using Acr.Filo.Application.Abstractions;
using Acr.Filo.Application.Auth;
using Acr.Filo.Infrastructure;
using Acr.Filo.Infrastructure.Identity;
using Acr.Filo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// --- Serilog ---
builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext());

// --- Servisler ---
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddDataProtection().PersistKeysToFileSystem(new System.IO.DirectoryInfo(@"C:\inetpub\acrfilo\keys"));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

// Yetki bazlı policy altyapısı
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionHandler>();
builder.Services.AddAuthorization();

// JWT
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(string.IsNullOrWhiteSpace(jwt.SigningKey)
                    ? new string('0', 32) : jwt.SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

// CORS — yalnız yapılandırılmış origin'ler. Boşsa (aynı site) CORS devre dışı.
var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(o => o.AddPolicy("Frontend", p =>
{
    if (origins.Length > 0)
        p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
}));


builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        // DateOnly zaten System.Text.Json tarafından "yyyy-MM-dd" olarak serialize edilir.
    });

builder.Services.AddEndpointsApiExplorer();

var swaggerEnabled = builder.Configuration.GetValue<bool>("Swagger:Enabled");
if (swaggerEnabled)
    builder.Services.AddSwaggerGen();

builder.Services.AddResponseCompression();

var app = builder.Build();

// --- CLI: yönetici parolası atama (deploy sonrası tek seferlik) ---
if (args.Contains("--set-admin-password"))
{
    await AdminPasswordTool.RunAsync(app.Services);
    return;
}

if (args.Contains("--add-user"))
{
    await AdminPasswordTool.AddUserAsync(app.Services);
    return;
}

if (args.Contains("--list-users"))
{
    await AdminPasswordTool.ListUsersAsync(app.Services);
    return;
}

// --- Pipeline ---
app.UseMiddleware<ExceptionMiddleware>();
app.UseSerilogRequestLogging();

if (app.Environment.IsProduction())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseResponseCompression();

// Frontend statik dosyaları (wwwroot). SPA değil ama tek HTML + varlıklar.
app.UseDefaultFiles();
app.UseStaticFiles();

if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Frontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// SPA/HTML fallback: /orders/123 gibi bilinmeyen yollar index.html'e (frontend router).
// API yolları (/api/*) hariç.
app.MapFallbackToFile("index.html");

app.Run();

// Program sınıfını integration testleri için erişilebilir yap.
public partial class Program { }
