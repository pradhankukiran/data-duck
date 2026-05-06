using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DataDuck.Models;
using DataDuck.Services;

namespace DataDuck.Desktop.Services;

public class EnvVarGroqAiService : IAiService
{
    private const string ApiKeyEnvVar = "GROQ_API_KEY";
    private const string Endpoint = "https://api.groq.com/openai/v1/chat/completions";
    private const string Model = "llama-3.3-70b-versatile";

    private readonly HttpClient _http;

    public EnvVarGroqAiService(HttpClient http)
    {
        _http = http;
    }

    public bool HasApiKey => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ApiKeyEnvVar));

    public async Task<string> GenerateSqlAsync(string englishQuestion, IReadOnlyList<LoadedFile> tables)
    {
        if (!HasApiKey)
        {
            throw new InvalidOperationException("Groq API key not set. Open Settings to add one.");
        }

        var apiKey = Environment.GetEnvironmentVariable(ApiKeyEnvVar)!;
        var systemPrompt = BuildSystemPrompt(tables);

        var payload = new GroqChatRequest(
            Model,
            new[]
            {
                new GroqChatMessage("system", systemPrompt),
                new GroqChatMessage("user", englishQuestion),
            },
            0.1,
            ResponseFormat: null);

        var json = JsonSerializer.Serialize(payload, DesktopJsonContext.Default.GroqChatRequest);

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

    public async Task<DatasetInsight> ExplainDatasetAsync(LoadedFile table, IReadOnlyList<object?[]> sampleRows)
    {
        if (!HasApiKey)
        {
            throw new InvalidOperationException("Groq API key not set. Open Settings to add one.");
        }

        var apiKey = Environment.GetEnvironmentVariable(ApiKeyEnvVar)!;
        var systemPrompt = BuildInsightSystemPrompt();
        var userPrompt = BuildInsightUserPrompt(table, sampleRows);

        var payload = new GroqChatRequest(
            Model,
            new[]
            {
                new GroqChatMessage("system", systemPrompt),
                new GroqChatMessage("user", userPrompt),
            },
            0.2,
            new GroqResponseFormat("json_object"));

        var json = JsonSerializer.Serialize(payload, DesktopJsonContext.Default.GroqChatRequest);

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

        return ParseInsight(content);
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

    private static string BuildInsightSystemPrompt()
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a data analyst assistant for DuckDB datasets. The user will give you a table schema and a CSV sample of its rows.");
        sb.AppendLine("Respond ONLY with a strict JSON object matching this shape:");
        sb.AppendLine("{");
        sb.AppendLine("  \"summary\": string (1-2 sentence overview of what this dataset is),");
        sb.AppendLine("  \"findings\": string[] (3-5 short bullet observations about patterns, ranges, distributions, or data-quality notes),");
        sb.AppendLine("  \"suggestedQueries\": [ { \"title\": string, \"sql\": string } ] (exactly 3 ready-to-run DuckDB SQL queries against the given table)");
        sb.AppendLine("}");
        sb.AppendLine("The SQL must reference the exact table name provided and use only the listed columns. Do not include markdown, commentary, or code fences—JSON only.");
        return sb.ToString();
    }

    private static string BuildInsightUserPrompt(LoadedFile table, IReadOnlyList<object?[]> sampleRows)
    {
        var sb = new StringBuilder();
        sb.Append("CREATE TABLE ").Append(table.TableName).Append(" (");
        for (var i = 0; i < table.Columns.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            var col = table.Columns[i];
            sb.Append(col.Name).Append(' ').Append(col.Type);
        }
        sb.AppendLine(");");
        sb.Append("Row count: ").Append(table.RowCount).AppendLine();
        sb.AppendLine();
        sb.AppendLine("Sample rows (CSV, up to 20):");

        // Header
        for (var i = 0; i < table.Columns.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(EscapeCsv(table.Columns[i].Name));
        }
        sb.AppendLine();

        var take = Math.Min(20, sampleRows.Count);
        for (var r = 0; r < take; r++)
        {
            var row = sampleRows[r];
            for (var c = 0; c < row.Length; c++)
            {
                if (c > 0) sb.Append(',');
                sb.Append(EscapeCsv(row[c]?.ToString() ?? string.Empty));
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string EscapeCsv(string value)
    {
        if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static DatasetInsight ParseInsight(string content)
    {
        var jsonText = StripFences(content);
        try
        {
            using var doc = JsonDocument.Parse(jsonText);
            var root = doc.RootElement;

            var summary = root.TryGetProperty("summary", out var summaryEl) && summaryEl.ValueKind == JsonValueKind.String
                ? summaryEl.GetString() ?? string.Empty
                : string.Empty;

            var findings = new List<string>();
            if (root.TryGetProperty("findings", out var findingsEl) && findingsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in findingsEl.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        var s = item.GetString();
                        if (!string.IsNullOrWhiteSpace(s)) findings.Add(s);
                    }
                }
            }

            var suggested = new List<SuggestedQuery>();
            if (root.TryGetProperty("suggestedQueries", out var queriesEl) && queriesEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in queriesEl.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object) continue;
                    var title = item.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String
                        ? t.GetString() ?? string.Empty
                        : string.Empty;
                    var sql = item.TryGetProperty("sql", out var s) && s.ValueKind == JsonValueKind.String
                        ? s.GetString() ?? string.Empty
                        : string.Empty;
                    if (!string.IsNullOrWhiteSpace(sql))
                    {
                        suggested.Add(new SuggestedQuery(title, sql));
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(summary) && findings.Count == 0 && suggested.Count == 0)
            {
                throw new InvalidOperationException("Groq insight response did not contain any of summary, findings, or suggestedQueries.");
            }

            return new DatasetInsight(summary, findings, suggested);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Groq insight response was not valid JSON.", ex);
        }
    }
}
