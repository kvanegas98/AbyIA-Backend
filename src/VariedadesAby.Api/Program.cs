using System.Data;
using System.IO.Compression;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;
using VariedadesAby.Api.Middleware;
using VariedadesAby.Core.DTOs.FtpDrive;
using VariedadesAby.Core.DTOs.Reporte;
using VariedadesAby.Core.Interfaces;
using VariedadesAby.Infrastructure.Repositories;
using VariedadesAby.Infrastructure.Services;
using VariedadesAby.Infrastructure.Workers;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// SERVICIOS
// ============================================================

// --- Dapper: Conexión principal (lectura general) ---
builder.Services.AddScoped<IDbConnection>(sp =>
    new SqlConnection(builder.Configuration.GetConnectionString("Conexion")));

// --- Dapper: Conexión de solo lectura para IA (aislada) ---
builder.Services.AddKeyedScoped<IDbConnection>("AI_ReadOnly", (sp, _) =>
    new SqlConnection(builder.Configuration.GetConnectionString("AI_ReadOnlyConnection")));

// --- Repositorios ---
builder.Services.AddScoped<IDashboardRepository, DashboardRepository>();
builder.Services.AddScoped<IFinanzasRepository, FinanzasRepository>();

// --- Servicio: Módulo Compras PDF ---
builder.Services.AddScoped<IComprasPdfService, ComprasPdfService>();

// --- Servicio: Módulo Ingresos ---
builder.Services.AddScoped<IIngresosService, IngresosService>();

// --- Servicio: Módulo Análisis ---
builder.Services.AddScoped<IAnalisisService, AnalisisService>();

// --- Servicio: Panel de Vendedores ---
builder.Services.AddScoped<IVendedoresService, VendedoresService>();

// --- Servicio: Módulo Sucursal ---
builder.Services.AddScoped<ISucursalService, SucursalService>();

// --- Servicios y Repositorios: Módulo Aby IA ---
builder.Services.AddScoped<IHistorialChatRepository, HistorialChatRepository>();
builder.Services.AddScoped<IAbyIAService, AbyIAService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IChatExportService, ChatExportService>();

// ── Módulo FTP → Google Drive ──────────────────────────────────────────────
builder.Services.Configure<FtpSettings>(builder.Configuration.GetSection("FtpSettings"));
builder.Services.Configure<GoogleDriveSettings>(builder.Configuration.GetSection("GoogleDriveSettings"));
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.Configure<SchedulerSettings>(builder.Configuration.GetSection("SchedulerSettings"));

builder.Services.AddScoped<IFtpIntegrationService, FtpIntegrationService>();
builder.Services.AddScoped<IGoogleDriveIntegrationService, GoogleDriveIntegrationService>();
builder.Services.AddScoped<IEmailNotificationService, EmailNotificationService>();
builder.Services.AddScoped<IFileTransferOrchestrator, FileTransferOrchestrator>();

// Worker programado (singleton por AddHostedService; resuelve scoped via IServiceScopeFactory)
builder.Services.AddHostedService<FileTransferWorker>();

// ── Módulo Reporte Diario ──────────────────────────────────────────────────
builder.Services.Configure<ReporteSettings>(builder.Configuration.GetSection("ReporteSettings"));
builder.Services.AddScoped<IReporteDiarioService, ReporteDiarioService>();
builder.Services.AddHostedService<ReporteDiarioWorker>();
// ──────────────────────────────────────────────────────────────────────────

// --- Caché en memoria ---
builder.Services.AddMemoryCache();

// --- Compresión de respuestas (Brotli + Gzip) ---
builder.Services.AddResponseCompression(opts =>
{
    opts.EnableForHttps = true;
    opts.Providers.Add<BrotliCompressionProvider>();
    opts.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);

// --- Rate Limiting ---
builder.Services.AddRateLimiter(options =>
{
    // Login: max 10 intentos por minuto por IP (protección fuerza bruta)
    options.AddFixedWindowLimiter("login", o =>
    {
        o.PermitLimit = 10;
        o.Window = TimeSpan.FromMinutes(1);
        o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        o.QueueLimit = 0;
    });

    // General: max 200 requests por 10 segundos por IP
    options.AddFixedWindowLimiter("general", o =>
    {
        o.PermitLimit = 200;
        o.Window = TimeSpan.FromSeconds(10);
        o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        o.QueueLimit = 5;
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// --- CORS ---
// IMPORTANTE: Agregar el dominio de producción cuando esté disponible
builder.Services.AddCors(options =>
{
    options.AddPolicy("Todos", policy =>
    {
        policy.WithOrigins(
                "http://localhost:8080",
                "http://localhost:5173",
                "http://localhost:3000",
                "https://abyia-8dc86.web.app",
                "https://abyia-8dc86.firebaseapp.com")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// --- JWT Authentication ---
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Issuer"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

// --- Controllers + JSON ---
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

// --- Swagger ---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Variedades Aby Admin API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Ingrese el token JWT"
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ============================================================
// PIPELINE
// ============================================================
var app = builder.Build();

app.UseResponseCompression(); // Primero — comprime todo lo que sigue

app.UseMiddleware<GlobalExceptionMiddleware>();

// Swagger solo en Development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Todos");

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Endpoint público para keep-alive (UptimeRobot u otro monitor externo)
app.MapGet("/api/health", () => Results.Ok(new { status = "ok", utc = DateTime.UtcNow }))
   .AllowAnonymous()
   .ExcludeFromDescription();

app.Run();
