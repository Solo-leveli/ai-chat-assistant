using System.Diagnostics;
using BoltAI.Application;
using BoltAI.Domain;
namespace BoltAI.Infrastructure;
public sealed class MeasuredRepository(IBusinessRepository inner, IAuditService audit) : IBusinessRepository
{
    private async Task<T> Measure<T>(string operation, Func<Task<T>> action)
    {
        var elapsed = Stopwatch.StartNew(); var success = false;
        try { var result = await action(); success = true; return result; }
        finally { audit.Record(new(Activity.Current?.TraceId.ToString() ?? "none", "server", operation, "Repository", true, success, elapsed.ElapsedMilliseconds, success ? null : "repository_failure", DateTimeOffset.UtcNow)); }
    }
    public Task<OrderRecord?> GetOrderAsync(string n, IReadOnlySet<string> a, CancellationToken ct) => Measure("RepositoryOrder", () => inner.GetOrderAsync(n, a, ct));
    public Task<IReadOnlyList<OrderRecord>> GetHistoryAsync(IReadOnlySet<string> a, CancellationToken ct) => Measure("RepositoryHistory", () => inner.GetHistoryAsync(a, ct));
    public Task<StockRecord?> GetStockAsync(string n, IReadOnlySet<string> a, CancellationToken ct) => Measure("RepositoryStock", () => inner.GetStockAsync(n, a, ct));
    public Task<OrderRecord?> GetShipmentAsync(string n, IReadOnlySet<string> a, CancellationToken ct) => Measure("RepositoryShipment", () => inner.GetShipmentAsync(n, a, ct));
}
public sealed class MeasuredDocuments(IDocumentSearchService inner, IAuditService audit) : IDocumentSearchService
{
    public async Task<IReadOnlyList<DocumentSection>> SearchAsync(string query, UserIdentity user, CancellationToken ct)
    {
        var elapsed = Stopwatch.StartNew(); var success = false;
        try { var result = await inner.SearchAsync(query, user, ct); success = true; return result; }
        finally { audit.Record(new(Activity.Current?.TraceId.ToString() ?? "none", user.UserId, "RAG", "DocumentSearch", true, success, elapsed.ElapsedMilliseconds, success ? null : "rag_failure", DateTimeOffset.UtcNow)); }
    }
}
