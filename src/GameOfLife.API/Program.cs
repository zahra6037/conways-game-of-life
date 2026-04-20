using GameOfLife.API.ExceptionHandlers;
using GameOfLife.API.Telemetry;
using GameOfLife.Application.Ports;
using GameOfLife.Application.Services;
using GameOfLife.Application.Telemetry;
using GameOfLife.Domain.Services;
using GameOfLife.Infrastructure;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<GameRulesService>();
builder.Services.AddScoped<IGameService, GameService>();
builder.Services.AddInfrastructure(builder.Configuration);

// Order matters — most specific handlers first.
builder.Services.AddExceptionHandler<BoardNotFoundExceptionHandler>();
builder.Services.AddExceptionHandler<BoardDidNotStabilizeExceptionHandler>();
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
builder.Services.AddProblemDetails();

// --- Telemetry -------------------------------------------------------------
// Logs use the default ASP.NET Core providers (console + debug). Trace spans
// are exported to a local JSON-lines file (default: logs/traces.jsonl).
var traceFilePath = builder.Configuration["Telemetry:TraceFilePath"] ?? "logs/traces.json";
var serviceName = builder.Configuration["Telemetry:ServiceName"] ?? "GameOfLife.API";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(serviceName))
    .WithTracing(tracing => tracing
        .AddSource(GameTelemetry.SourceName)
        .AddAspNetCoreInstrumentation()
        .AddProcessor(new BatchActivityExportProcessor(new FileTraceExporter(traceFilePath, serviceName))));

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.MapControllers();

app.Run();

public partial class Program;