using System.Diagnostics;

namespace GameOfLife.Application.Telemetry;
// Activities are exported to Jaeger for observability, allowing tracing
public static class GameTelemetry
{
    public const string SourceName = "GameOfLife";
    public static readonly ActivitySource ActivitySource = new(SourceName);
}