using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Serilog;
using Serilog.Sinks.Grafana.Loki;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.GrafanaLoki(
        Environment.GetEnvironmentVariable("LOKI_URL") ?? "http://localhost:3100",
        labels: new List<LokiLabel>
        {
            new() { Key = "app", Value = "km-gateway-operaciones" },
            new() { Key = "env", Value = builder.Environment.EnvironmentName.ToLower() },
        }
    )
    .CreateLogger();

builder.Host.UseSerilog();


builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policy =>
    {
        policy
            .WithOrigins("http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);

var jwtSettings = builder.Configuration.GetSection("Jwt");

var jwtKey = jwtSettings.GetSection("Key").Get<string>();
var jwtIssuer = jwtSettings.GetSection("Issuer").Get<string>();
var jwtAudience = jwtSettings.GetSection("Audience").Get<List<string>>();

// Configurar autenticación JWT
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,

            ValidateAudience = true,
            ValidAudiences = jwtAudience,


            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),

            ValidateLifetime = true
        };
    });

builder.Services.AddOcelot();

var app = builder.Build();

// Habilitar autenticación en el API Gateway
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseSerilogRequestLogging();
app.UseCors("CorsPolicy");
app.UseAuthentication();
app.UseAuthorization();


app.UseOcelot().Wait();

app.Run();
