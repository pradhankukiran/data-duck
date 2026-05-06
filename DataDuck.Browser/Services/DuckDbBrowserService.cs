using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DataDuck.Models;
using DataDuck.Services;

namespace DataDuck.Browser.Services;

/// <summary>
/// IDuckDbService implementation that calls into the duckdb-shim.js module via [JSImport].
/// Lazy-initializes — first call to RegisterFile or Query downloads the ~33 MB DuckDB-WASM bundle.
/// </summary>
public partial class DuckDbBrowserService : IDuckDbService
{
    private bool _initialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public async Task InitAsync()
    {
        if (_initialized) return;
        await _initLock.WaitAsync();
        try
        {
            if (_initialized) return;
            await JSHost.ImportAsync("duckdb-shim", "./duckdb-shim.js");
            await InitDuckDB("/duckdb");
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<LoadedFile> RegisterFileAsync(string fileName, byte[] data)
    {
        await InitAsync();
        var json = await RegisterFile(fileName, data);
        var meta = JsonSerializer.Deserialize<RegisterResp>(json, JsonOptions)
                   ?? throw new InvalidOperationException("Empty response from registerFile");

        return new LoadedFile(
            fileName,
            meta.TableName,
            data.LongLength,
            meta.RowCount,
            meta.Columns.Select(c => new ColumnMeta(c.Name, c.Type)).ToArray());
    }

    public async Task<QueryResult> QueryAsync(string sql)
    {
        await InitAsync();
        var sw = Stopwatch.StartNew();
        var json = await Query(sql);
        sw.Stop();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
        {
            return new QueryResult(Array.Empty<string>(), Array.Empty<object?[]>(), sw.ElapsedMilliseconds);
        }

        var firstRow = root[0];
        var columnNames = firstRow.EnumerateObject().Select(p => p.Name).ToArray();

        var rows = root.EnumerateArray().Select(row =>
            columnNames.Select(col =>
            {
                if (!row.TryGetProperty(col, out var val)) return (object?)null;
                return val.ValueKind switch
                {
                    JsonValueKind.Null => null,
                    JsonValueKind.True => true,
                    JsonValueKind.False => (object)false,
                    JsonValueKind.Number => val.TryGetInt64(out var l) ? (object)l : val.GetDouble(),
                    JsonValueKind.String => val.GetString(),
                    _ => val.ToString()
                };
            }).ToArray()
        ).ToArray();

        return new QueryResult(columnNames, rows, sw.ElapsedMilliseconds);
    }

    [JSImport("initDuckDB", "duckdb-shim")]
    internal static partial Task InitDuckDB(string baseUrl);

    [JSImport("registerFile", "duckdb-shim")]
    internal static partial Task<string> RegisterFile(string name, byte[] data);

    [JSImport("query", "duckdb-shim")]
    internal static partial Task<string> Query(string sql);

    private sealed record RegisterResp(string TableName, int RowCount, ColInfo[] Columns);
    private sealed record ColInfo(string Name, string Type);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };
}
