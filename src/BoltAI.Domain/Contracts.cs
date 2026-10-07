namespace BoltAI.Domain;

public sealed record UserIdentity(string UserId, IReadOnlySet<string> AccountIds, IReadOnlySet<string> Roles);
public sealed record OrderRecord(string AccountId, string OrderNumber, string Status, DateTimeOffset LastUpdated, string? EstimatedDelivery, string ShipmentId, string[] Exceptions);
public sealed record StockRecord(string AccountId, string ProductCode, int Quantity, string Location);
public sealed record OrderStatus(string OrderNumber, string Status, DateTimeOffset LastUpdated, string? EstimatedDelivery);
public sealed record OrderSummary(string OrderNumber, string Status, string ShipmentId);
public sealed record StockResult(string ProductCode, int Quantity);
public sealed record AvailabilityResult(string ProductCode, bool Available);
public sealed record LocationResult(string ProductCode, string Location);
public sealed record TrackingResult(string ShipmentId, string Status, string? EstimatedDelivery);
public sealed record ExceptionsResult(string OrderNumber, string[] Exceptions);
public sealed record DocumentSource(string DocumentId, string Title, string Section, DateTimeOffset LastUpdated, string SourceReference);
public sealed record DocumentSection(DocumentSource Source, string Text);
public sealed record AuditEvent(string RequestId, string UserId, string Tool, string ResourceType, bool Allowed, bool Success, long DurationMs, string? ErrorCategory, DateTimeOffset Timestamp);
public sealed class SafeFailure(string category, string message) : Exception(message)
{
    public string Category { get; } = category;
}
