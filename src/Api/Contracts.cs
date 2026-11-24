namespace BeyondTheCode.Api;

public sealed class InferenceOptions
{
    public const string Section = "Inference";

    /// <summary>OpenAI-compatible base URL of the LLM, e.g. http://qwen-predictor.models.svc.cluster.local/openai/v1</summary>
    public Uri Chat { get; init; } = null!;

    /// <summary>The --model_name of the LLM InferenceService.</summary>
    public string ChatModel { get; init; } = "qwen";

    /// <summary>OpenAI-compatible base URL of the embedding model.</summary>
    public Uri Embeddings { get; init; } = null!;

    /// <summary>The --model_name of the embedding InferenceService.</summary>
    public string EmbeddingModel { get; init; } = "embed";

    /// <summary>KServe doesn't check it; the OpenAI SDK just requires a value.</summary>
    public string ApiKey { get; init; } = "not-needed";
}

public static class Telemetry
{
    public const string SourceName = "BeyondTheCode.AI";
}

public static class Prompts
{
    public const string System =
        "You are a helpful assistant at a developer conference. " +
        "Answer concisely and in the same language as the question.";
}

public sealed record AskRequest(string Question);

public sealed record SearchRequest(string Query, int? Top);

public sealed record Talk(string Title, string Speaker, string Language, string Abstract);

public sealed record SearchHit(string Title, string Speaker, string Language, double Score);
