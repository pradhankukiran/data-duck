using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DataDuck.Models;
using DataDuck.Services;

namespace DataDuck.Browser.Services;

public class GroqAiService : IAiService
{
    private const string ApiKeyStoreKey = "dataduck.groq.apikey";
    private const string Endpoint = "https://api.groq.com/openai/v1/chat/completions";
    private const string Model = "llama-3.3-70b-versatile";

    private readonly ILocalStore _store;
    private readonly HttpClient _http;

    public GroqAiService(ILocalStore store, HttpClient http)
    {
        _store = store;
        _http = http;
    }

    public bool HasApiKey => !string.IsNullOrWhiteSpace(_store.Get(ApiKeyStoreKey));

    public async Task<string> GenerateSqlAsync(string englishQuestion, IReadOnlyList<LoadedFile> tables)
    {
        if (!HasApiKey)
        {
            throw new InvalidOperationException("Groq API key not set. Open Settings to add one.");
        }

        var apiKey = _store.Get(ApiKeyStoreKey)!;
        var systemPrompt = BuildSystemPrompt(tables);

        var payload = new
        {
            model = Model,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = englishQuestion }
            },
            temperature = 0.1
        };

        var json = JsonSerializer.Serialize(payload);

        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new HttpRequestException(
                $"Groq API request failed: {ex.Message}" +
                (ex.StatusCode is { } code ? $" (status {(int)code} {code})" : string.Empty),
                ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            throw new HttpRequestException(
                $"Groq API returned {(int)response.StatusCode} {response.StatusCode}: {errorBody}");
        }

        var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        string content;
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? string.Empty;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
        {
            throw new InvalidOperationException("Groq API returned an unexpected response shape.", ex);
        }

        return StripFences(content).Trim();
    }

    private static string BuildSystemPrompt(IReadOnlyList<LoadedFile> tables)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a SQL assistant for DuckDB. Generate a single DuckDB-compatible SQL query that answers the user's question.");
        sb.AppendLine("Only output the SQL query. Do not include commentary, explanations, or markdown code fences.");
        sb.AppendLine();

        if (tables.Count > 0)
        {
            sb.AppendLine("The following tables are available:");
            foreach (var table in tables)
            {
                sb.Append("CREATE TABLE ").Append(table.TableName).Append(" (");
                for (var i = 0; i < table.Columns.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    var col = table.Columns[i];
                    sb.Append(col.Name).Append(' ').Append(col.Type);
                }
                sb.AppendLine(");");
            }
        }
        else
        {
            sb.AppendLine("No tables are currently loaded.");
        }

        return sb.ToString();
    }

    private static string StripFences(string content)
    {
        var trimmed = content.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            // Remove the opening fence (e.g. ```sql or just ```)
            var firstNewline = trimmed.IndexOf('\n');
            if (firstNewline >= 0)
            {
                trimmed = trimmed[(firstNewline + 1)..];
            }
            else
            {
                trimmed = trimmed[3..];
            }

            // Remove the closing fence
            if (trimmed.EndsWith("```", StringComparison.Ordinal))
            {
                trimmed = trimmed[..^3];
            }
        }

        return trimmed.Trim();
    }
}
