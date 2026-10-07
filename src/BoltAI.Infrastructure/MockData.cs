using BoltAI.Application;
using BoltAI.Domain;
namespace BoltAI.Infrastructure;

public sealed class InMemoryBusinessRepository : IBusinessRepository
{
    private readonly OrderRecord[] orders = [
        new("C100", "45821", "In Transit", DateTimeOffset.Parse("2026-01-01T12:00:00Z"), "2026-01-05", "SH12345", ["Fictional weather delay"]),
        new("C200", "60001", "Processing", DateTimeOffset.Parse("2026-01-01T12:00:00Z"), null, "SH60001", []),
        new("C999", "99999", "Delivered", DateTimeOffset.Parse("2026-01-01T12:00:00Z"), null, "SH99999", [])];
    private readonly StockRecord[] stock = [new("C100", "ABC123", 42, "Fictional Warehouse A / Bin 7"), new("C999", "SECRET999", 900, "Restricted Fictional Warehouse")];
    public Task<OrderRecord?> GetOrderAsync(string number, IReadOnlySet<string> accounts, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); return Task.FromResult(orders.FirstOrDefault(o => accounts.Contains(o.AccountId) && o.OrderNumber == number)); }
    public Task<IReadOnlyList<OrderRecord>> GetHistoryAsync(IReadOnlySet<string> accounts, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); return Task.FromResult<IReadOnlyList<OrderRecord>>(orders.Where(o => accounts.Contains(o.AccountId)).OrderByDescending(o => o.LastUpdated).Take(10).ToArray()); }
    public Task<StockRecord?> GetStockAsync(string code, IReadOnlySet<string> accounts, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); var rows = stock.Where(s => accounts.Contains(s.AccountId) && s.ProductCode == code).ToArray(); return Task.FromResult(rows.Length == 0 ? null : rows[0] with { Quantity = rows.Sum(s => s.Quantity), Location = string.Join("; ", rows.Select(s => s.Location)) }); }
    public Task<OrderRecord?> GetShipmentAsync(string shipment, IReadOnlySet<string> accounts, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); return Task.FromResult(orders.FirstOrDefault(o => accounts.Contains(o.AccountId) && o.ShipmentId == shipment)); }
}
public sealed class MockDocumentSearchService : IDocumentSearchService
{
    public Task<IReadOnlyList<DocumentSection>> SearchAsync(string query, UserIdentity user, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        IReadOnlyList<DocumentSection> results = user.AccountIds.Contains("C100") && user.Roles.Contains("BoltReader") && query.Contains("damaged", StringComparison.OrdinalIgnoreCase)
            ? [new(new("DEMO-DAMAGE", "Fictional damaged goods procedure — not Sprint policy", "Prototype example", DateTimeOffset.Parse("2026-01-01T00:00:00Z"), "demo:damaged-goods"), "FICTIONAL DEMO ONLY: Quarantine the damaged item, record photographs and contact your account representative. This is not an approved Sprint procedure.")] : [];
        return Task.FromResult(results);
    }
}
