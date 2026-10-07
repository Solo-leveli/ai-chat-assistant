using System.Text.Json;
using BoltAI.Domain;
namespace BoltAI.Application;

public interface IUserContext { UserIdentity GetUser(); }
public interface ICustomerScopeService { IReadOnlySet<string> GetScope(UserIdentity user); }
public interface IAuthorizationService { bool CanRead(UserIdentity user); bool CanReadLocations(UserIdentity user); }
public interface IBusinessRepository
{
    Task<OrderRecord?> GetOrderAsync(string number, IReadOnlySet<string> accounts, CancellationToken ct);
    Task<IReadOnlyList<OrderRecord>> GetHistoryAsync(IReadOnlySet<string> accounts, CancellationToken ct);
    Task<StockRecord?> GetStockAsync(string code, IReadOnlySet<string> accounts, CancellationToken ct);
    Task<OrderRecord?> GetShipmentAsync(string shipment, IReadOnlySet<string> accounts, CancellationToken ct);
}
public interface IDocumentSearchService { Task<IReadOnlyList<DocumentSection>> SearchAsync(string query, UserIdentity user, CancellationToken ct); }
public interface IAuditService { void Record(AuditEvent auditEvent); }
public sealed record ToolDefinition(string Name, string Description, JsonElement Parameters);
public sealed record ToolCall(string CallId, string Name, string Arguments);
public sealed record AiTurn(string? Text, IReadOnlyList<ToolCall> Calls, JsonElement[] Continuation);
public sealed record AiItem(string Type, string? Role = null, string? Content = null, string? CallId = null, JsonElement? Raw = null);
public interface IAiClient { Task<AiTurn> RespondAsync(IReadOnlyList<AiItem> input, IReadOnlyList<ToolDefinition> tools, CancellationToken ct); }
public sealed record ToolResult(string Json, IReadOnlyList<DocumentSource> Sources);
public interface IToolDispatcher
{
    IReadOnlyList<ToolDefinition> Definitions { get; }
    Task<ToolResult> ExecuteAsync(ToolCall call, UserIdentity user, string requestId, CancellationToken ct);
}
public sealed record ChatMessage(string Role, string Content);
public sealed record ConversationLease(string Id, string Owner, IReadOnlyList<ChatMessage> Messages);
public interface IConversationStore
{
    ConversationLease Open(string? id, UserIdentity user);
    void Append(ConversationLease lease, string question, string answer);
}
public sealed record ChatReply(string ConversationId, string Message, string RequestId, IReadOnlyList<DocumentSource> Sources);
public interface IChatOrchestrator { Task<ChatReply> ChatAsync(string message, string? conversationId, string requestId, CancellationToken ct); }
public sealed class AiOptions
{
    public string Provider { get; set; } = "Mock";
    public string Model { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxToolIterations { get; set; } = 4;
    public int MaxToolCalls { get; set; } = 12;
    public string ApiKey { get; set; } = "";
}
