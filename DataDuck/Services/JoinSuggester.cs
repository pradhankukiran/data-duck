using System;
using System.Collections.Generic;
using System.Linq;
using DataDuck.Models;

namespace DataDuck.Services;

/// <summary>
/// Heuristic JOIN suggestion engine. Given a list of loaded tables, returns ranked
/// suggestions of likely foreign-key style joins based on column-name similarity and
/// type compatibility.
/// </summary>
public static class JoinSuggester
{
    private const double ConfidenceThreshold = 0.4;
    private const int MaxSuggestions = 10;

    public static IReadOnlyList<JoinSuggestion> Suggest(IReadOnlyList<LoadedFile> files)
    {
        if (files is null || files.Count < 2)
        {
            return Array.Empty<JoinSuggestion>();
        }

        var suggestions = new List<JoinSuggestion>();

        for (var i = 0; i < files.Count; i++)
        {
            for (var j = 0; j < files.Count; j++)
            {
                if (i == j) continue;

                var left = files[i];
                var right = files[j];

                foreach (var lc in left.Columns)
                {
                    foreach (var rc in right.Columns)
                    {
                        if (!TypesCompatible(lc.Type, rc.Type)) continue;

                        var confidence = ScoreColumns(left.TableName, lc.Name, right.TableName, rc.Name);
                        if (confidence < ConfidenceThreshold) continue;

                        suggestions.Add(new JoinSuggestion(
                            left.TableName,
                            lc.Name,
                            right.TableName,
                            rc.Name,
                            Math.Min(1.0, confidence),
                            BuildSql(left.TableName, lc.Name, right.TableName, rc.Name)));
                    }
                }
            }
        }

        return suggestions
            .OrderByDescending(s => s.Confidence)
            .Take(MaxSuggestions)
            .ToList();
    }

    private static double ScoreColumns(string leftTable, string leftCol, string rightTable, string rightCol)
    {
        var l = leftCol.ToLowerInvariant();
        var r = rightCol.ToLowerInvariant();
        var lt = leftTable.ToLowerInvariant();
        var rt = rightTable.ToLowerInvariant();

        double score = 0.0;

        // Exact column-name match (e.g., customer_id ↔ customer_id).
        if (string.Equals(l, r, StringComparison.Ordinal))
        {
            score += 0.7;
        }

        // Foreign-key style: one is "id", the other is "<otherTable>_id" (or singularised).
        if (IsFkRelationship(l, lt, r, rt) || IsFkRelationship(r, rt, l, lt))
        {
            score += 0.6;
        }

        // Substring match of >=4 chars common (only meaningful if not already exact).
        if (!string.Equals(l, r, StringComparison.Ordinal) &&
            HasCommonSubstring(l, r, minLen: 4))
        {
            score += 0.3;
        }

        // Both names contain "id" — small bonus for likely identifier columns.
        if (l.Contains("id", StringComparison.Ordinal) &&
            r.Contains("id", StringComparison.Ordinal))
        {
            score += 0.1;
        }

        return score;
    }

    /// <summary>
    /// True when <paramref name="aCol"/> is the literal string "id" on table
    /// <paramref name="aTable"/>, and <paramref name="bCol"/> is "&lt;aTable&gt;_id"
    /// (or singular form) on the other table.
    /// </summary>
    private static bool IsFkRelationship(string aCol, string aTable, string bCol, string bTable)
    {
        if (!string.Equals(aCol, "id", StringComparison.Ordinal)) return false;

        // Try aTable + "_id" exactly, plus a common singular form (drop trailing 's').
        var expected = aTable + "_id";
        if (string.Equals(bCol, expected, StringComparison.Ordinal)) return true;

        if (aTable.EndsWith("s", StringComparison.Ordinal) && aTable.Length > 1)
        {
            var singular = aTable[..^1] + "_id";
            if (string.Equals(bCol, singular, StringComparison.Ordinal)) return true;
        }

        return false;
    }

    private static bool HasCommonSubstring(string a, string b, int minLen)
    {
        if (a.Length < minLen || b.Length < minLen) return false;

        // Classic O(n*m) longest-common-substring — fine for column-name lengths.
        var max = 0;
        var dp = new int[a.Length + 1, b.Length + 1];
        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                if (a[i - 1] == b[j - 1])
                {
                    dp[i, j] = dp[i - 1, j - 1] + 1;
                    if (dp[i, j] > max) max = dp[i, j];
                }
            }
        }

        return max >= minLen;
    }

    /// <summary>
    /// Returns true if the two declared column types should be considered
    /// compatible for a join. Numeric integer types share a bucket; everything
    /// else must match by a normalized base name.
    /// </summary>
    private static bool TypesCompatible(string a, string b)
    {
        var na = NormalizeType(a);
        var nb = NormalizeType(b);
        return string.Equals(na, nb, StringComparison.Ordinal);
    }

    private static string NormalizeType(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        // Strip parameters like VARCHAR(255), DECIMAL(18,2).
        var paren = raw.IndexOf('(');
        var baseName = (paren > 0 ? raw[..paren] : raw).Trim().ToUpperInvariant();

        return baseName switch
        {
            "TINYINT" or "SMALLINT" or "INTEGER" or "INT" or "BIGINT" or "HUGEINT"
                or "UTINYINT" or "USMALLINT" or "UINTEGER" or "UBIGINT" => "INT_BUCKET",
            "REAL" or "FLOAT" or "DOUBLE" => "FLOAT_BUCKET",
            "VARCHAR" or "CHAR" or "TEXT" or "STRING" => "STR_BUCKET",
            "DATE" => "DATE",
            "TIMESTAMP" or "TIMESTAMPTZ" or "DATETIME" => "TIMESTAMP",
            "BOOLEAN" or "BOOL" => "BOOL",
            "DECIMAL" or "NUMERIC" => "DECIMAL",
            _ => baseName,
        };
    }

    private static string BuildSql(string leftTable, string leftCol, string rightTable, string rightCol)
    {
        return
            $"SELECT a.*, b.*\n" +
            $"FROM \"{leftTable}\" a\n" +
            $"LEFT JOIN \"{rightTable}\" b ON a.\"{leftCol}\" = b.\"{rightCol}\"\n" +
            $"LIMIT 100";
    }
}
