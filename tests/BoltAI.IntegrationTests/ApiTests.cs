using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;
namespace BoltAI.IntegrationTests;
public sealed class DemoFactory(bool devAuth = true, string environment = "Development") : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        foreach (var setting in new Dictionary<string,string?> {
            ["DevAuth:Enabled"] = devAuth.ToString(), ["AI:Provider"] = "Mock",
            ["Authentication:Authority"] = "https://fictional.invalid", ["Authentication:Audience"] = "test-bolt",
            ["DevAuth:Accounts:0"] = "C100", ["DevAuth:Roles:0"] = "BoltReader", ["DevAuth:Roles:1"] = "InventoryLocation" }) builder.UseSetting(setting.Key, setting.Value);
    }
}
public class ApiTests
{
    [Fact] public async Task RealApiUsesScopedToolAndHasCorrelation()
    { await using var f = new DemoFactory(); using var c = f.CreateClient(); var r = await c.PostAsJsonAsync("/api/chat/message", new { message = "Where is order 45821?" }); Assert.Equal(HttpStatusCode.OK, r.StatusCode); var json = await r.Content.ReadFromJsonAsync<JsonElement>(); Assert.Contains("In Transit", json.GetProperty("message").GetString()); Assert.NotEmpty(json.GetProperty("conversationId").GetString()!); Assert.True(r.Headers.Contains("X-Request-ID")); }
    [Theory] [InlineData("99999")] [InlineData("00000")]
    public async Task InaccessibleOrMissingOrderIsSafe404(string number)
    { await using var f = new DemoFactory(); var r = await f.CreateClient().PostAsJsonAsync("/api/chat/message", new { message = "order " + number }); Assert.Equal(HttpStatusCode.NotFound, r.StatusCode); var json = await r.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal("not_found", json.GetProperty("error").GetProperty("code").GetString()); Assert.DoesNotContain(number, json.GetProperty("error").GetRawText()); }
    [Fact] public async Task CustomerScopeInBodyIsRejected()
    { await using var f = new DemoFactory(); var r = await f.CreateClient().PostAsJsonAsync("/api/chat/message", new { message = "order 99999", customerId = "C999" }); Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode); }
    [Fact] public async Task UnauthenticatedRequestCannotUseChat()
    { await using var f = new DemoFactory(false); var r = await f.CreateClient().PostAsJsonAsync("/api/chat/message", new { message = "help" }); Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode); }
    [Fact] public async Task HeadersCannotChangeDevIdentity()
    { await using var f = new DemoFactory(); var c = f.CreateClient(); c.DefaultRequestHeaders.Add("X-Customer-Id", "C999"); c.DefaultRequestHeaders.Add("X-User-Id", "admin"); var r = await c.PostAsJsonAsync("/api/chat/message", new { message = "order 99999" }); Assert.Equal(HttpStatusCode.NotFound, r.StatusCode); }
    [Theory] [InlineData("")] [InlineData(" ")]
    public async Task BlankMessageRejected(string message)
    { await using var f = new DemoFactory(); var r = await f.CreateClient().PostAsJsonAsync("/api/chat/message", new { message }); Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode); }
    [Fact] public async Task LongMessageRejected()
    { await using var f = new DemoFactory(); var r = await f.CreateClient().PostAsJsonAsync("/api/chat/message", new { message = new string('x', 2001) }); Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode); }
    [Fact] public async Task RateLimitIsEnforced()
    { await using var f = new DemoFactory(); var c = f.CreateClient(); HttpResponseMessage? last = null; for (var i = 0; i < 21; i++) { last?.Dispose(); last = await c.PostAsJsonAsync("/api/chat/message", new { message = "help" }); } Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode); last.Dispose(); }
    [Fact] public async Task ConversationCanContinue()
    { await using var f = new DemoFactory(); var c = f.CreateClient(); var first = await c.PostAsJsonAsync("/api/chat/message", new { message = "help" }); var j = await first.Content.ReadFromJsonAsync<JsonElement>(); var second = await c.PostAsJsonAsync("/api/chat/message", new { message = "order 45821", conversationId = j.GetProperty("conversationId").GetString() }); Assert.Equal(HttpStatusCode.OK, second.StatusCode); }
    [Fact] public async Task HealthAndOpenApiAreAvailableInDev()
    { await using var f = new DemoFactory(); var c = f.CreateClient(); Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/internal/health")).StatusCode); Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/openapi/v1.json")).StatusCode); }
    [Fact] public void DevelopmentIdentityCannotStartInProduction()
    { using var f = new DemoFactory(environment: "Production"); Assert.Throws<InvalidOperationException>(() => f.CreateClient()); }
}
