using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using BoltAI.Api;
using BoltAI.Application;
using BoltAI.Domain;
using BoltAI.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
var aiOptions = builder.Configuration.GetSection("AI").Get<AiOptions>() ?? new();
if (aiOptions.TimeoutSeconds is < 1 or > 120 || aiOptions.MaxToolIterations is < 1 or > 8 || aiOptions.MaxToolCalls is < 1 or > 24)
    throw new InvalidOperationException("AI limits must be within supported bounds.");
if (aiOptions.Provider is not ("Mock" or "OpenAI")) throw new InvalidOperationException("Unsupported AI provider.");
if (aiOptions.Provider == "OpenAI" && (string.IsNullOrWhiteSpace(aiOptions.Model) || string.IsNullOrWhiteSpace(aiOptions.ApiKey)))
    throw new InvalidOperationException("OpenAI provider requires server-side model and API key configuration.");
var devAuth = builder.Configuration.GetValue<bool>("DevAuth:Enabled");
if (devAuth && !builder.Environment.IsDevelopment()) throw new InvalidOperationException("Development authentication is prohibited outside Development.");
if (!builder.Environment.IsDevelopment()) throw new InvalidOperationException("This fictional-data prototype is development-only until BOLT integrations are approved.");
if (builder.Configuration.GetValue<bool>("Sql:Enabled")) throw new InvalidOperationException("SQL contracts have not been approved. SQL activation is intentionally blocked.");
if (devAuth)
    builder.Services.AddAuthentication("Development").AddScheme<AuthenticationSchemeOptions, DevAuthHandler>("Development", _ => { });
else
{
    var authority = builder.Configuration["Authentication:Authority"];
    var audience = builder.Configuration["Authentication:Audience"];
    if (string.IsNullOrWhiteSpace(authority) || !Uri.TryCreate(authority, UriKind.Absolute, out var issuer) || issuer.Scheme != "https" || string.IsNullOrWhiteSpace(audience))
        throw new InvalidOperationException("Configure a confirmed HTTPS BOLT token authority and audience, or explicitly enable Development authentication.");
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => { o.Authority = authority; o.Audience = audience; o.RequireHttpsMetadata = true; o.MapInboundClaims = true; o.TokenValidationParameters.ValidateIssuer = true; o.TokenValidationParameters.ValidateAudience = true; o.TokenValidationParameters.ValidateLifetime = true; });
}
builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUserContext, HttpUserContext>();
builder.Services.AddSingleton(aiOptions);
builder.Services.AddSingleton<ICustomerScopeService, CustomerScopeService>();
builder.Services.AddSingleton<BoltAI.Application.IAuthorizationService, CustomerAuthorizationService>();
builder.Services.AddSingleton<InMemoryBusinessRepository>();
builder.Services.AddSingleton<IBusinessRepository>(sp => new MeasuredRepository(sp.GetRequiredService<InMemoryBusinessRepository>(), sp.GetRequiredService<IAuditService>()));
builder.Services.AddSingleton<MockDocumentSearchService>();
builder.Services.AddSingleton<IDocumentSearchService>(sp => new MeasuredDocuments(sp.GetRequiredService<MockDocumentSearchService>(), sp.GetRequiredService<IAuditService>()));
builder.Services.AddSingleton<IConversationStore, InMemoryConversationStore>();
builder.Services.AddSingleton<IAuditService, LoggingAuditService>();
builder.Services.AddScoped<IToolDispatcher, ToolDispatcher>();
builder.Services.AddSingleton<MockAiClient>();
builder.Services.AddHttpClient<OpenAiResponsesClient>(c => c.Timeout = TimeSpan.FromSeconds(aiOptions.TimeoutSeconds)).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddScoped<IAiClient>(sp => new MeasuredAiClient(aiOptions.Provider == "Mock" ? sp.GetRequiredService<MockAiClient>() : sp.GetRequiredService<OpenAiResponsesClient>(), sp.GetRequiredService<IAuditService>(), sp.GetRequiredService<IUserContext>()));
builder.Services.AddScoped<IChatOrchestrator, ChatOrchestrator>();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 8192);
builder.Services.AddOpenApi();
builder.Services.Configure<Microsoft.AspNetCore.Routing.RouteHandlerOptions>(o => o.ThrowOnBadRequest = true);
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.OnRejected = async (ctx, ct) => await ctx.HttpContext.Response.WriteAsJsonAsync(new { error = new { code = "rate_limited", message = "Please wait before sending another message." }, requestId = ctx.HttpContext.TraceIdentifier }, ct);
    o.AddPolicy("chat", http => RateLimitPartition.GetFixedWindowLimiter(http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous", _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(_ => RateLimitPartition.GetConcurrencyLimiter("global", _ => new ConcurrencyLimiterOptions { PermitLimit = 16, QueueLimit = 0 }));
});
var app = builder.Build();
app.Use(async (http, next) =>
{
    using var requestActivity = new System.Diagnostics.Activity("BoltAI.Request").Start();
    http.TraceIdentifier = requestActivity.TraceId.ToString();
    http.Response.Headers["X-Content-Type-Options"] = "nosniff";
    http.Response.Headers["Cache-Control"] = "no-store";
    http.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'";
    http.Response.Headers["X-Request-ID"] = http.TraceIdentifier;
    try { await next(http); }
    catch (SafeFailure failure)
    {
        http.Response.StatusCode = failure.Category switch { "not_found" or "conversation_unavailable" => 404, "unauthenticated" => 401, "invalid_request" => 400, "timeout" => 504, _ => 503 };
        await http.Response.WriteAsJsonAsync(new { error = new { code = failure.Category, message = failure.Message }, requestId = http.TraceIdentifier }, http.RequestAborted);
    }
    catch (BadHttpRequestException)
    {
        http.Response.StatusCode = 400;
        await http.Response.WriteAsJsonAsync(new { error = new { code = "invalid_request", message = "The request is invalid." }, requestId = http.TraceIdentifier });
    }
    catch (OperationCanceledException) when (http.RequestAborted.IsCancellationRequested) { }
    catch (Exception)
    {
        app.Logger.LogError("Request failed RequestId={RequestId} Category=internal_error", http.TraceIdentifier);
        http.Response.StatusCode = 503;
        await http.Response.WriteAsJsonAsync(new { error = new { code = "service_unavailable", message = "The assistant is currently unavailable." }, requestId = http.TraceIdentifier });
    }
});
if (!app.Environment.IsDevelopment()) app.UseHsts();
app.UseAuthentication(); app.UseAuthorization(); app.UseRateLimiter();
if (app.Environment.IsDevelopment())
{
    app.UseDefaultFiles(); app.UseStaticFiles(); app.MapOpenApi();
}
app.MapGet("/internal/health", () => Results.Ok(new { status = "healthy", dataMode = "fictional-mock", aiProvider = aiOptions.Provider })).RequireAuthorization();
app.MapPost("/api/chat/message", async (ChatRequest request, IChatOrchestrator chat, HttpContext http) =>
    Results.Ok(await chat.ChatAsync(request.Message, request.ConversationId, http.TraceIdentifier, http.RequestAborted)))
    .RequireAuthorization().RequireRateLimiting("chat");
app.Run();
public sealed record ChatRequest(string Message, string? ConversationId = null);
public partial class Program { }
