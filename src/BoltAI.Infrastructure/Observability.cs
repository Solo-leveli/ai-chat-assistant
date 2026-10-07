using System.Diagnostics.Metrics;
using BoltAI.Application;
using BoltAI.Domain;
using Microsoft.Extensions.Logging;
namespace BoltAI.Infrastructure;
public sealed class LoggingAuditService(ILogger<LoggingAuditService> logger) : IAuditService
{
    private static readonly Meter Meter = new("BoltAI", "1.0");
    private static readonly Counter<long> Operations = Meter.CreateCounter<long>("bolt.operations");
    private static readonly Histogram<double> Latency = Meter.CreateHistogram<double>("bolt.operation.duration", "ms");
    public void Record(AuditEvent e)
    {
        logger.LogInformation("Audit RequestId={RequestId} UserId={UserId} Tool={Tool} Resource={Resource} Allowed={Allowed} Success={Success} DurationMs={DurationMs} ErrorCategory={ErrorCategory} Timestamp={Timestamp}",
            e.RequestId, e.UserId, e.Tool, e.ResourceType, e.Allowed, e.Success, e.DurationMs, e.ErrorCategory, e.Timestamp);
        var tags = new KeyValuePair<string, object?>[] { new("operation", e.Tool), new("success", e.Success), new("category", e.ErrorCategory ?? "none") };
        Operations.Add(1, tags); Latency.Record(e.DurationMs, tags);
    }
}
public sealed class MeasuredAiClient(IAiClient inner, IAuditService audit, IUserContext context) : IAiClient
{
    public async Task<AiTurn> RespondAsync(IReadOnlyList<AiItem> input, IReadOnlyList<ToolDefinition> tools, CancellationToken ct)
    {
        var start = System.Diagnostics.Stopwatch.StartNew(); bool success = false;
        try { var result = await inner.RespondAsync(input, tools, ct); success = true; return result; }
        finally { audit.Record(new(System.Diagnostics.Activity.Current?.TraceId.ToString() ?? "none", context.GetUser().UserId, "AI", "Provider", true, success, start.ElapsedMilliseconds, success ? null : "ai_failure", DateTimeOffset.UtcNow)); }
    }
}
