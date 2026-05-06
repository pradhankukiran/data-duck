using System;
using System.Globalization;
using System.Text;
using DataDuck.Models;

namespace DataDuck.Services;

/// <summary>
/// Pure-string serializers for <see cref="QueryResult"/> instances. The caller is
/// responsible for actually pushing the result to the clipboard / disk — this class
/// has no IO and no UI dependencies.
/// </summary>
public static class ResultExporter
{
    private const int MarkdownRowCap = 1000;

    /// <summary>RFC 4180-ish CSV. Quotes fields that contain comma, quote, or newline.</summary>
    public static string ToCsv(QueryResult result)
    {
        if (result is null) return string.Empty;

        var sb = new StringBuilder();
        for (var i = 0; i < result.ColumnNames.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(CsvEscape(result.ColumnNames[i]));
        }
        sb.Append('\n');

        foreach (var row in result.Rows)
        {
            for (var i = 0; i < row.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(CsvEscape(FormatValue(row[i])));
            }
            sb.Append('\n');
        }

        return sb.ToString();
    }

    /// <summary>
    /// Tab-separated values. Escapes tabs / newlines inside cells so the row
    /// shape survives a paste into Excel / Google Sheets.
    /// </summary>
    public static string ToTsv(QueryResult result)
    {
        if (result is null) return string.Empty;

        var sb = new StringBuilder();
        for (var i = 0; i < result.ColumnNames.Count; i++)
        {
            if (i > 0) sb.Append('\t');
            sb.Append(TsvEscape(result.ColumnNames[i]));
        }
        sb.Append('\n');

        foreach (var row in result.Rows)
        {
            for (var i = 0; i < row.Length; i++)
            {
                if (i > 0) sb.Append('\t');
                sb.Append(TsvEscape(FormatValue(row[i])));
            }
            sb.Append('\n');
        }

        return sb.ToString();
    }

    /// <summary>GitHub-flavored Markdown table, capped at the first 1000 rows.</summary>
    public static string ToMarkdown(QueryResult result)
    {
        if (result is null) return string.Empty;

        var sb = new StringBuilder();

        sb.Append('|');
        foreach (var col in result.ColumnNames)
        {
            sb.Append(' ').Append(MarkdownEscape(col)).Append(" |");
        }
        sb.Append('\n');

        sb.Append('|');
        for (var i = 0; i < result.ColumnNames.Count; i++)
        {
            sb.Append("---|");
        }
        sb.Append('\n');

        var rowsTaken = 0;
        foreach (var row in result.Rows)
        {
            if (rowsTaken >= MarkdownRowCap) break;
            sb.Append('|');
            for (var i = 0; i < row.Length; i++)
            {
                sb.Append(' ').Append(MarkdownEscape(FormatValue(row[i]))).Append(" |");
            }
            sb.Append('\n');
            rowsTaken++;
        }

        var remaining = result.Rows.Count - rowsTaken;
        if (remaining > 0)
        {
            sb.Append("… (").Append(remaining).Append(" more rows)\n");
        }

        return sb.ToString();
    }

    private static string FormatValue(object? value)
    {
        return value switch
        {
            null => string.Empty,
            string s => s,
            bool b => b ? "true" : "false",
            DateTimeOffset dto => dto.ToString("o", CultureInfo.InvariantCulture),
            DateTime dt => dt.ToString("o", CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
        };
    }

    private static string CsvEscape(string value)
    {
        var needsQuote = value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0;
        if (!needsQuote) return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static string TsvEscape(string value)
    {
        // No quoting in TSV — collapse structural whitespace to spaces.
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\t", " ", StringComparison.Ordinal)
            .Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
    }

    private static string MarkdownEscape(string value)
    {
        // Pipes break table layout; newlines collapse the row.
        return value
            .Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
    }
}
