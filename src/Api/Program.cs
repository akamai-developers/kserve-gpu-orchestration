using System.ClientModel;
using BeyondTheCode.Api;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

var inference = builder.Configuration.GetSection(InferenceOptions.Section).Get<InferenceOptions>()
    ?? throw new InvalidOperationException($"Missing '{InferenceOptions.Section}' configuration.");

// One OpenAI client per KServe InferenceService: only the endpoint differs.
OpenAIClient CreateClient(Uri endpoint) => new(
    new ApiKeyCredential(inference.ApiKey),
    new OpenAIClientOptions { Endpoint = endpoint, NetworkTimeout = TimeSpan.FromMinutes(2) });

// Model 1: the LLM on the GPU (vLLM, OpenAI-compatible chat completions).
builder.Services
    .AddChatClient(CreateClient(inference.Chat).GetChatClient(inference.ChatModel).AsIChatClient())
    .UseOpenTelemetry(sourceName: Telemetry.SourceName)
    .UseLogging();

// Model 2: the embedding model on CPU (OpenAI-compatible embeddings).
builder.Services
    .AddEmbeddingGenerator(CreateClient(inference.Embeddings).GetEmbeddingClient(inference.EmbeddingModel).AsIEmbeddingGenerator())
    .UseOpenTelemetry(sourceName: Telemetry.SourceName);

builder.Services.AddSingleton<TalkIndex>();
builder.Services.AddHealthChecks();

// Traces, metrics and logs via OTLP (the in-cluster Aspire dashboard).
var otel = builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("beyond-the-code-api"))
    .WithTracing(tracing => tracing
        .AddSource(Telemetry.SourceName)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation())
    .WithMetrics(metrics => metrics
        .AddMeter(Telemetry.SourceName)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation());

builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
});

if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
{
    otel.UseOtlpExporter();
}

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapHealthChecks("/healthz");

// Streams the LLM's answer as plain text (try: curl -N).
app.MapPost("/ask", async (AskRequest request, IChatClient chat, HttpContext http, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.Question))
    {
        return Results.BadRequest("A question is required.");
    }

    List<ChatMessage> messages =
    [
        new(ChatRole.System, Prompts.System),
        new(ChatRole.User, request.Question),
    ];

    http.Response.ContentType = "text/plain; charset=utf-8";
    http.Response.Headers["X-Accel-Buffering"] = "no"; // tell NGINX-based proxies not to buffer the stream
    await foreach (var update in chat.GetStreamingResponseAsync(messages, cancellationToken: ct))
    {
        await http.Response.WriteAsync(update.Text, ct);
        await http.Response.Body.FlushAsync(ct);
    }

    return Results.Empty;
});

// Semantic search over the talk abstracts in Data/talks.json.
app.MapPost("/search", async (SearchRequest request, TalkIndex index, CancellationToken ct) =>
    string.IsNullOrWhiteSpace(request.Query)
        ? Results.BadRequest("A query is required.")
        : Results.Ok(await index.SearchAsync(request.Query, Math.Clamp(request.Top ?? 3, 1, 10), ct)));

app.Run();
