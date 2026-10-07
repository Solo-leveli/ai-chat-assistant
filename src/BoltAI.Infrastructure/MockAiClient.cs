using System.Text.Json;
using System.Text.RegularExpressions;
using BoltAI.Application;
namespace BoltAI.Infrastructure;
public sealed class MockAiClient : IAiClient
{
    public Task<AiTurn> RespondAsync(IReadOnlyList<AiItem> input, IReadOnlyList<ToolDefinition> tools, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (input.Last().Type == "function_call_output")
        {
            var result = input.Last().Content!;
            return Task.FromResult(new AiTurn("Fictional prototype data: " + result, [], []));
        }
        var question = input.Last().Content ?? "";
        var lower = question.ToLowerInvariant();
        string? tool = null; string? field = null; string? value = null;
        if (lower.Contains("procedure") || lower.Contains("damaged")) { tool = "SearchDocumentation"; field = "query"; value = question[..Math.Min(500, question.Length)]; }
        else if (lower.Contains("recent") || lower.Contains("history") || lower.Contains("every customer")) tool = "GetOrderHistory";
        else if (Regex.Match(question, "\\bSH[0-9]+\\b", RegexOptions.IgnoreCase) is { Success: true } shipment) { tool = "GetShipmentTracking"; field = "shipmentId"; value = shipment.Value.ToUpperInvariant(); }
        else if (Regex.Match(question, "\\b[A-Za-z]{2,}[0-9]+\\b") is { Success: true } product)
        { tool = lower.Contains("where") || lower.Contains("location") ? "GetProductLocation" : lower.Contains("available") ? "GetProductAvailability" : "GetStock"; field = "productCode"; value = product.Value.ToUpperInvariant(); }
        else if (Regex.Match(question, "\\b[0-9]{3,}\\b") is { Success: true } order)
        { tool = lower.Contains("exception") ? "GetOrderExceptions" : "GetOrderStatus"; field = "orderNumber"; value = order.Value; }
        if (tool is null) return Task.FromResult(new AiTurn("I can help with logistics through approved tools.", [], []));
        var args = JsonSerializer.Serialize(field is null ? new Dictionary<string, string>() : new Dictionary<string, string> { [field] = value! });
        var callId = Guid.NewGuid().ToString("N");
        var raw = JsonSerializer.SerializeToElement(new { type = "function_call", call_id = callId, name = tool, arguments = args });
        return Task.FromResult(new AiTurn(null, [new(callId, tool, args)], [raw]));
    }
}
