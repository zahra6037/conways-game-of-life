using System.Diagnostics;
using System.Text.Json;
using OpenTelemetry;

namespace GameOfLife.API.Telemetry;

/// <summary>
/// OpenTelemetry trace exporter that writes spans to a single local JSON file
/// in Jaeger's upload format. The file is atomically rewritten on every flush
/// so it always represents a complete, valid JSON document that the Jaeger UI
/// (Search → "JSON File" upload) can render directly.
/// </summary>
public sealed class FileTraceExporter : BaseExporter<Activity>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _filePath;
    private readonly string _serviceName;
    private readonly object _gate = new();

    // Spans are buffered in memory and serialized on each flush. Jaeger's
    // upload format requires a single JSON document, so we cannot append.
    // Keyed by traceId so we group spans into traces on serialization.
    private readonly Dictionary<string, List<JaegerSpan>> _tracesByTraceId = new();

    public FileTraceExporter(string filePath, string serviceName)
    {
        _filePath = Path.GetFullPath(filePath);
        _serviceName = serviceName;
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
    }

    public override ExportResult Export(in Batch<Activity> batch)
    {
        try
        {
            lock (_gate)
            {
                foreach (var activity in batch)
                {
                    var span = ToJaegerSpan(activity);
                    if (!_tracesByTraceId.TryGetValue(span.TraceID, out var spans))
                    {
                        spans = new List<JaegerSpan>();
                        _tracesByTraceId[span.TraceID] = spans;
                    }
                    spans.Add(span);
                }

                WriteAtomically();
            }
            return ExportResult.Success;
        }
        catch
        {
            return ExportResult.Failure;
        }
    }

    private void WriteAtomically()
    {
        var traces = _tracesByTraceId.Select(kvp => new JaegerTrace
        {
            TraceID = kvp.Key,
            Spans = kvp.Value,
            Processes = new Dictionary<string, JaegerProcess>
            {
                ["p1"] = new() { ServiceName = _serviceName, Tags = Array.Empty<JaegerTag>() }
            }
        }).ToArray();

        var doc = new JaegerDocument { Data = traces };
        var json = JsonSerializer.Serialize(doc, JsonOptions);

        var tmp = _filePath + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, _filePath, overwrite: true);
    }

    private JaegerSpan ToJaegerSpan(Activity a)
    {
        var references = a.ParentSpanId != default
            ? new[] {
                new JaegerReference {
                    RefType = "CHILD_OF",
                    TraceID = a.TraceId.ToString(),
                    SpanID = a.ParentSpanId.ToString()
                }}
            : Array.Empty<JaegerReference>();

        var tags = new List<JaegerTag>
        {
            new() { Key = "span.kind", Type = "string", Value = a.Kind.ToString().ToLowerInvariant() },
            new() { Key = "otel.library.name", Type = "string", Value = a.Source.Name },
            new() { Key = "otel.status_code", Type = "string", Value = a.Status.ToString().ToUpperInvariant() }
        };
        if (!string.IsNullOrEmpty(a.StatusDescription))
            tags.Add(new JaegerTag { Key = "otel.status_description", Type = "string", Value = a.StatusDescription });

        foreach (var t in a.TagObjects)
        {
            if (t.Value is null) continue;
            tags.Add(new JaegerTag
            {
                Key = t.Key,
                Type = JaegerTypeFor(t.Value),
                Value = t.Value
            });
        }

        return new JaegerSpan
        {
            TraceID = a.TraceId.ToString(),
            SpanID = a.SpanId.ToString(),
            OperationName = a.DisplayName,
            References = references,
            // Jaeger expects microseconds since epoch / microsecond duration.
            StartTime = new DateTimeOffset(a.StartTimeUtc).ToUnixTimeMilliseconds() * 1000,
            Duration = (long)(a.Duration.TotalMilliseconds * 1000),
            Tags = tags,
            Logs = Array.Empty<object>(),
            ProcessID = "p1"
        };
    }

    private static string JaegerTypeFor(object value) => value switch
    {
        bool => "bool",
        byte or sbyte or short or ushort or int or uint or long or ulong => "int64",
        float or double or decimal => "float64",
        _ => "string"
    };

    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        try { lock (_gate) WriteAtomically(); } catch { /* best-effort */ }
        return true;
    }

    // --- Jaeger upload schema --------------------------------------------------
    private sealed class JaegerDocument
    {
        public JaegerTrace[] Data { get; set; } = Array.Empty<JaegerTrace>();
    }

    private sealed class JaegerTrace
    {
        public string TraceID { get; set; } = "";
        public List<JaegerSpan> Spans { get; set; } = new();
        public Dictionary<string, JaegerProcess> Processes { get; set; } = new();
    }

    private sealed class JaegerSpan
    {
        public string TraceID { get; set; } = "";
        public string SpanID { get; set; } = "";
        public string OperationName { get; set; } = "";
        public IReadOnlyList<JaegerReference> References { get; set; } = Array.Empty<JaegerReference>();
        public long StartTime { get; set; }
        public long Duration { get; set; }
        public IReadOnlyList<JaegerTag> Tags { get; set; } = Array.Empty<JaegerTag>();
        public IReadOnlyList<object> Logs { get; set; } = Array.Empty<object>();
        public string ProcessID { get; set; } = "";
    }

    private sealed class JaegerReference
    {
        public string RefType { get; set; } = "CHILD_OF";
        public string TraceID { get; set; } = "";
        public string SpanID { get; set; } = "";
    }

    private sealed class JaegerProcess
    {
        public string ServiceName { get; set; } = "";
        public IReadOnlyList<JaegerTag> Tags { get; set; } = Array.Empty<JaegerTag>();
    }

    private sealed class JaegerTag
    {
        public string Key { get; set; } = "";
        public string Type { get; set; } = "string";
        public object Value { get; set; } = "";
    }
}
