using System.Net;
using System.Text.Json;
using BoltAI.Application;
using BoltAI.Domain;
using BoltAI.Infrastructure;
using Xunit;
namespace BoltAI.UnitTests;
public sealed class StubHttp(string body, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
{
    public JsonElement Sent { get; private set; }
    public string? Endpoint { get; private set; }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    { Endpoint = request.RequestUri!.ToString(); Sent = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct)).RootElement.Clone(); return new(status) { Content = new StringContent(body) }; }
}
public class OpenAiAdapterTests
{
    [Fact] public async Task ResponsesUsesStrictSchemasAndNoCredentialInBody()
    {
        using var h = new StubHttp("""{"status":"completed","output":[{"type":"function_call","call_id":"c1","name":"GetOrderStatus","arguments":"{\"orderNumber\":\"45821\"}"}]}""");
        using var http = new HttpClient(h); var c = new OpenAiResponsesClient(http, new() { Model = "configured-model", ApiKey = "test-only-placeholder" });
        var result = await c.RespondAsync([new("message", "user", "order 45821")], ScopeTests.Dispatcher().Definitions, default);
        Assert.Equal("https://api.openai.com/v1/responses", h.Endpoint); Assert.Equal("GetOrderStatus", result.Calls.Single().Name);
        Assert.Equal("configured-model", h.Sent.GetProperty("model").GetString()); Assert.False(h.Sent.GetProperty("store").GetBoolean());
        Assert.All(h.Sent.GetProperty("tools").EnumerateArray(), t => Assert.True(t.GetProperty("strict").GetBoolean()));
        Assert.DoesNotContain("test-only-placeholder", h.Sent.GetRawText()); Assert.DoesNotContain("customerId", h.Sent.GetProperty("tools").GetRawText());
    }
    [Fact] public async Task ResponsesCarriesToolOutputAndExtractsFinalText()
    {
        using var h = new StubHttp("""{"status":"completed","output":[{"type":"message","role":"assistant","content":[{"type":"output_text","text":"Order is in transit."}]}]}""");
        using var http = new HttpClient(h); var c = new OpenAiResponsesClient(http, new() { Model = "configured", ApiKey = "placeholder" });
        var r = await c.RespondAsync([new("function_call_output", Content: "{\"status\":\"In Transit\"}", CallId: "c1")], ScopeTests.Dispatcher().Definitions, default);
        Assert.Equal("Order is in transit.", r.Text); Assert.Equal("function_call_output", h.Sent.GetProperty("input")[0].GetProperty("type").GetString());
    }
    [Fact] public async Task ProviderErrorBodyIsNotExposed()
    { using var h = new StubHttp("private error SECRET", HttpStatusCode.TooManyRequests); using var http = new HttpClient(h); var e = await Assert.ThrowsAsync<SafeFailure>(() => new OpenAiResponsesClient(http, new() { Model = "configured", ApiKey = "placeholder" }).RespondAsync([], [], default)); Assert.DoesNotContain("SECRET", e.Message); }
    [Fact] public async Task IncompleteProviderResponseRejected()
    { using var h = new StubHttp("""{"status":"incomplete","output":[]}"""); using var http = new HttpClient(h); await Assert.ThrowsAsync<SafeFailure>(() => new OpenAiResponsesClient(http, new() { Model = "configured", ApiKey = "placeholder" }).RespondAsync([], [], default)); }
}
