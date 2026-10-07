using BoltAI.Application;
using BoltAI.Domain;
using BoltAI.Infrastructure;
using Xunit;
namespace BoltAI.UnitTests;
public sealed class TestAudit : IAuditService { public List<AuditEvent> Events { get; } = []; public void Record(AuditEvent e) => Events.Add(e); }
public class ScopeTests
{
    internal static UserIdentity User => new("U100", new HashSet<string> { "C100" }, new HashSet<string> { "BoltReader", "InventoryLocation" });
    internal static ToolDispatcher Dispatcher(IBusinessRepository? repo = null, IDocumentSearchService? docs = null, IAuditService? audit = null) => new(repo ?? new InMemoryBusinessRepository(), docs ?? new MockDocumentSearchService(), new CustomerAuthorizationService(), new CustomerScopeService(), audit ?? new TestAudit());
    [Fact] public async Task OwnOrderAllowedAndMinimized()
    {
        var audit = new TestAudit(); var result = await Dispatcher(audit: audit).ExecuteAsync(new("1", "GetOrderStatus", "{\"orderNumber\":\"45821\"}"), User, "r", default);
        Assert.Contains("In Transit", result.Json); Assert.DoesNotContain("C100", result.Json); Assert.DoesNotContain("exceptions", result.Json); Assert.True(audit.Events.Single().Allowed);
    }
    [Theory] [InlineData("99999")] [InlineData("00000")]
    public async Task UnauthorizedAndMissingAreIdentical(string id)
    { var e = await Assert.ThrowsAsync<SafeFailure>(() => Dispatcher().ExecuteAsync(new("1", "GetOrderStatus", $"{{\"orderNumber\":\"{id}\"}}"), User, "r", default)); Assert.Equal("not_found", e.Category); Assert.Equal("The requested information is unavailable.", e.Message); }
    [Fact] public async Task HistoryCannotBroadenAccountScope()
    { var r = await Dispatcher().ExecuteAsync(new("1", "GetOrderHistory", "{}"), User, "r", default); Assert.Contains("45821", r.Json); Assert.DoesNotContain("99999", r.Json); Assert.DoesNotContain("60001", r.Json); }
    [Theory] [InlineData("{\"orderNumber\":\"99999\",\"customerId\":\"C999\"}")] [InlineData("{\"orderNumber\":\"1; DROP TABLE Orders\"}")] [InlineData("not-json")] [InlineData("{\"orderNumber\":123}")] [InlineData("{\"orderNumber\":\"45821\",\"orderNumber\":\"99999\"}")]
    public async Task UntrustedArgumentsRejected(string arguments)
    { var e = await Assert.ThrowsAsync<SafeFailure>(() => Dispatcher().ExecuteAsync(new("1", "GetOrderStatus", arguments), User, "r", default)); Assert.Equal("invalid_tool_arguments", e.Category); }
    [Fact] public async Task UnknownToolRejected()
    { var e = await Assert.ThrowsAsync<SafeFailure>(() => Dispatcher().ExecuteAsync(new("1", "ExecuteSql", "{}"), User, "r", default)); Assert.Equal("unknown_tool", e.Category); }
    [Fact] public async Task LocationRequiresAdditionalRole()
    { var user = User with { Roles = new HashSet<string> { "BoltReader" } }; await Assert.ThrowsAsync<SafeFailure>(() => Dispatcher().ExecuteAsync(new("1", "GetProductLocation", "{\"productCode\":\"ABC123\"}"), user, "r", default)); }
    [Fact] public async Task DocumentsAreAccountScoped()
    { var r = await new MockDocumentSearchService().SearchAsync("damaged goods", User with { AccountIds = new HashSet<string> { "C999" } }, default); Assert.Empty(r); }
}
