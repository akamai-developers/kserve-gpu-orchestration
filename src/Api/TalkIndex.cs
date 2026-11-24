using System.Text.Json;
using Microsoft.Extensions.AI;

namespace BeyondTheCode.Api;

/// <summary>
/// A tiny in-memory vector index: embeds all talk abstracts once (on first use)
/// and ranks them by cosine similarity to the query.
/// </summary>
public sealed class TalkIndex(
    IEmbeddingGenerator<string, Embedding<float>> embeddings,
    IWebHostEnvironment environment,
    ILogger<TalkIndex> logger)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyList<(Talk Talk, ReadOnlyMemory<float> Vector)>? _entries;

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string query, int top, CancellationToken ct)
    {
        var entries = await GetEntriesAsync(ct);

        // e5 models expect "query: " and "passage: " prefixes.
        var queryVector = await embeddings.GenerateVectorAsync($"query: {query}", cancellationToken: ct);

        return entries
            .Select(e => new SearchHit(e.Talk.Title, e.Talk.Speaker, e.Talk.Language,
                Math.Round(CosineSimilarity(queryVector.Span, e.Vector.Span), 4)))
            .OrderByDescending(hit => hit.Score)
            .Take(top)
            .ToList();
    }

    private async Task<IReadOnlyList<(Talk Talk, ReadOnlyMemory<float> Vector)>> GetEntriesAsync(CancellationToken ct)
    {
        if (_entries is not null)
        {
            return _entries;
        }

        await _gate.WaitAsync(ct);
        try
        {
            if (_entries is null)
            {
                var path = Path.Combine(environment.ContentRootPath, "Data", "talks.json");
                await using var stream = File.OpenRead(path);
                var talks = await JsonSerializer.DeserializeAsync<Talk[]>(stream, JsonSerializerOptions.Web, ct) ?? [];

                var vectors = await embeddings.GenerateAsync(
                    talks.Select(t => $"passage: {t.Title}. {t.Abstract}"), cancellationToken: ct);

                _entries = talks
                    .Zip(vectors, (talk, embedding) => (Talk: talk, Vector: embedding.Vector))
                    .ToList();

                logger.LogInformation("Indexed {Count} talks", _entries.Count);
            }

            return _entries;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static double CosineSimilarity(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        double dot = 0, normA = 0, normB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }
}
