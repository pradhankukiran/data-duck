using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using DataDuck.Models;

namespace DataDuck.Services;

/// <summary>
/// Platform-agnostic column profiler. Issues SQL through <see cref="IDuckDbService"/>
/// so it works wherever a real DuckDB backend is registered (browser head).
/// On platforms where <c>QueryAsync</c> is not supported (e.g. desktop stub), the
/// underlying <see cref="NotSupportedException"/> propagates and should be handled
/// at the call site.
/// </summary>
public sealed class ProfilingService : IProfilingService
{
    private readonly IDuckDbService _duckDb;

    public ProfilingService(IDuckDbService duckDb)
    {
        _duckDb = duckDb;
    }

    public async Task<IReadOnlyList<ColumnProfile>> ProfileAsync(LoadedFile file)
    {
        if (file is null) throw new ArgumentNullException(nameof(file));

        var profiles = new List<ColumnProfile>(file.Columns.Count);
        var quotedTable = QuoteIdent(file.TableName);

        foreach (var column in file.Columns)
        {
            var profile = await ProfileColumnAsync(quotedTable, column).ConfigureAwait(false);
            profiles.Add(profile);
        }

        return profiles;
    }

    private async Task<ColumnProfile> ProfileColumnAsync(string quotedTable, ColumnMeta column)
    {
        var quotedCol = QuoteIdent(column.Name);
        var isNumeric = IsNumericType(column.Type);

        long total = 0, nonNull = 0, distinct = 0;
        string? minValue = null;
        string? maxValue = null;
        double? mean = null;
        double? stdDev = null;

        // Single combined stats query. AVG/STDDEV are only included for numeric columns;
        // for non-numerics we still compute count/min/max via VARCHAR cast.
        var statsSql = isNumeric
            ? $"SELECT " +
              $"COUNT(*) AS total, " +
              $"COUNT({quotedCol}) AS non_null, " +
              $"COUNT(DISTINCT {quotedCol}) AS distinct_n, " +
              $"CAST(MIN({quotedCol}) AS VARCHAR) AS min_v, " +
              $"CAST(MAX({quotedCol}) AS VARCHAR) AS max_v, " +
              $"TRY_CAST(AVG({quotedCol}) AS DOUBLE) AS mean, " +
              $"TRY_CAST(STDDEV({quotedCol}) AS DOUBLE) AS sd " +
              $"FROM {quotedTable}"
            : $"SELECT " +
              $"COUNT(*) AS total, " +
              $"COUNT({quotedCol}) AS non_null, " +
              $"COUNT(DISTINCT {quotedCol}) AS distinct_n, " +
              $"CAST(MIN({quotedCol}) AS VARCHAR) AS min_v, " +
              $"CAST(MAX({quotedCol}) AS VARCHAR) AS max_v " +
              $"FROM {quotedTable}";

        try
        {
            var result = await _duckDb.QueryAsync(statsSql).ConfigureAwait(false);
            if (result.Rows.Count > 0)
            {
                var row = result.Rows[0];
                total = ToLong(row, 0);
                nonNull = ToLong(row, 1);
                distinct = ToLong(row, 2);
                minValue = ToStringOrNull(row, 3);
                maxValue = ToStringOrNull(row, 4);
                if (isNumeric)
                {
                    mean = ToDoubleOrNull(row, 5);
                    stdDev = ToDoubleOrNull(row, 6);
                }
            }
        }
        catch (NotSupportedException)
        {
            // Platform doesn't support SQL execution — bubble up so the VM can show an error.
            throw;
        }
        catch
        {
            // If the combined query fails (e.g. AVG on a complex/struct col we mis-flagged
            // as numeric), fall back to a conservative count-only query so the user still
            // gets distinct/null counts.
            try
            {
                var fallbackSql =
                    $"SELECT COUNT(*) AS total, COUNT({quotedCol}) AS non_null, " +
                    $"COUNT(DISTINCT {quotedCol}) AS distinct_n FROM {quotedTable}";
                var result = await _duckDb.QueryAsync(fallbackSql).ConfigureAwait(false);
                if (result.Rows.Count > 0)
                {
                    var row = result.Rows[0];
                    total = ToLong(row, 0);
                    nonNull = ToLong(row, 1);
                    distinct = ToLong(row, 2);
                }
            }
            catch
            {
                // Truly unprofileable column — return zeros below.
            }

            mean = null;
            stdDev = null;
        }

        var topValues = await TopValuesAsync(quotedTable, quotedCol).ConfigureAwait(false);
        var nullCount = total - nonNull;
        if (nullCount < 0) nullCount = 0;

        return new ColumnProfile(
            ColumnName: column.Name,
            DataType: column.Type,
            DistinctCount: distinct,
            NullCount: nullCount,
            TotalCount: total,
            MinValue: minValue,
            MaxValue: maxValue,
            Mean: mean,
            StdDev: stdDev,
            TopValues: topValues);
    }

    private async Task<IReadOnlyList<TopValue>> TopValuesAsync(string quotedTable, string quotedCol)
    {
        var sql =
            $"SELECT CAST({quotedCol} AS VARCHAR) AS v, COUNT(*) AS c FROM {quotedTable} " +
            $"WHERE {quotedCol} IS NOT NULL " +
            $"GROUP BY {quotedCol} ORDER BY c DESC LIMIT 5";

        try
        {
            var result = await _duckDb.QueryAsync(sql).ConfigureAwait(false);
            var list = new List<TopValue>(result.Rows.Count);
            foreach (var row in result.Rows)
            {
                var value = ToStringOrNull(row, 0) ?? string.Empty;
                var count = ToLong(row, 1);
                list.Add(new TopValue(value, count));
            }
            return list;
        }
        catch (NotSupportedException)
        {
            throw;
        }
        catch
        {
            return Array.Empty<TopValue>();
        }
    }

    /// <summary>
    /// Best-effort numeric-type detector based on the DuckDB type string.
    /// Used to decide whether to attempt AVG/STDDEV. Wrong guesses are caught and tolerated.
    /// </summary>
    private static bool IsNumericType(string type)
    {
        if (string.IsNullOrWhiteSpace(type)) return false;
        var t = type.ToUpperInvariant();
        if (t.StartsWith("DECIMAL", StringComparison.Ordinal)) return true;
        if (t.StartsWith("NUMERIC", StringComparison.Ordinal)) return true;
        return t switch
        {
            "TINYINT" or "SMALLINT" or "INTEGER" or "INT" or "BIGINT" or "HUGEINT" => true,
            "UTINYINT" or "USMALLINT" or "UINTEGER" or "UBIGINT" or "UHUGEINT" => true,
            "FLOAT" or "REAL" or "DOUBLE" => true,
            _ => false,
        };
    }

    private static string QuoteIdent(string ident)
    {
        // DuckDB convention: double-quote identifiers, escape embedded quotes by doubling.
        return "\"" + ident.Replace("\"", "\"\"") + "\"";
    }

    private static long ToLong(object?[] row, int index)
    {
        if (index >= row.Length) return 0;
        var v = row[index];
        return v switch
        {
            null => 0,
            long l => l,
            int i => i,
            double d => (long)d,
            _ => long.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture),
                NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0,
        };
    }

    private static double? ToDoubleOrNull(object?[] row, int index)
    {
        if (index >= row.Length) return null;
        var v = row[index];
        return v switch
        {
            null => null,
            double d => double.IsNaN(d) ? null : d,
            long l => (double)l,
            int i => (double)i,
            _ => double.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture),
                NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : (double?)null,
        };
    }

    private static string? ToStringOrNull(object?[] row, int index)
    {
        if (index >= row.Length) return null;
        var v = row[index];
        if (v is null) return null;
        return v is string s ? s : Convert.ToString(v, CultureInfo.InvariantCulture);
    }
}
