using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BoltAI.Application;
using BoltAI.Domain;
namespace BoltAI.Infrastructure;
public sealed class OpenAiResponsesClient(HttpClient http, AiOptions options) : IAiClient
{
    public async Task<AiTurn> RespondAsync(IReadOnlyList<AiItem> input, IReadOnlyList<ToolDefinition> tools, CancellationToken ct)
    {
        try { return await RespondCoreAsync(input, tools, ct); }
        catch (SafeFailure) { throw; }
        catch (OperationCanceledException) { throw; }
        catch { throw Failure(); }
    }
    private async Task<AiTurn> RespondCoreAsync(IReadOnlyList<AiItem> input, IReadOnlyList<ToolDefinition> tools, CancellationToken ct)
    {
        var items = input.Select<AiItem, object>(item => item.Type switch
        {
            "raw" => item.Raw!.Value,
            "function_call_output" => new { type = "function_call_output", call_id = item.CallId, output = item.Content },
            _ => new { role = item.Role, content = item.Content }
        }).ToArray();
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        request.Content = JsonContent.Create(new { model = options.Model, instructions = AssistantInstructions.Text, input = items,
            tools = tools.Select(t => new { type = "function", name = t.Name, description = t.Description, parameters = t.Parameters, strict = true }),
            store = false, max_output_tokens = 1500, parallel_tool_calls = false });
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode) throw Failure();
        // Bound provider body before parsing; no raw provider errors reach logs or clients.
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream(); var chunk = new byte[8192]; int read;
        while ((read = await stream.ReadAsync(chunk, ct)) > 0) { if (buffer.Length + read > 262144) throw Failure(); buffer.Write(chunk, 0, read); }
        using var doc = JsonDocument.Parse(buffer.ToArray(), new JsonDocumentOptions { MaxDepth = 32 });
        var root = doc.RootElement;
        if (!root.TryGetProperty("status", out var status) || status.GetString() != "completed" || !root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array) throw Failure();
        var calls = new List<ToolCall>(); var text = new List<string>(); var continuation = new List<JsonElement>();
        foreach (var item in output.EnumerateArray())
        {
            continuation.Add(item.Clone());
            switch (item.GetProperty("type").GetString())
            {
                case "function_call":
                    var call = new ToolCall(item.GetProperty("call_id").GetString()!, item.GetProperty("name").GetString()!, item.GetProperty("arguments").GetString()!);
                    if (string.IsNullOrWhiteSpace(call.CallId) || call.CallId.Length > 200 || call.Name.Length > 100 || call.Arguments.Length > 4096 || calls.Count >= 12) throw Failure();
                    calls.Add(call); break;
                case "message":
                    foreach (var part in item.GetProperty("content").EnumerateArray())
                        if (part.GetProperty("type").GetString() == "output_text") text.Add(part.GetProperty("text").GetString()!);
                    break;
            }
        }
        return new(string.Join("\n", text), calls, continuation.ToArray());
    }
    private static SafeFailure Failure() => new("ai_unavailable", "The assistant provider is currently unavailable.");
}
