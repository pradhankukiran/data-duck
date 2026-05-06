using System;
using System.Threading.Tasks;
using DataDuck.Models;
using DataDuck.Services;

namespace DataDuck.Desktop.Services;

/// <summary>
/// Desktop-only stub. DuckDB-WASM runs in the browser only.
/// On desktop, file drop still adds an entry but querying throws a friendly error
/// pointing the dev to the Browser head.
/// </summary>
public sealed class NotSupportedDuckDbService : IDuckDbService
{
    public Task InitAsync() => Task.CompletedTask;

    public Task<LoadedFile> RegisterFileAsync(string fileName, byte[] data) =>
        Task.FromResult(new LoadedFile(
            fileName,
            ToTableName(fileName),
            data.LongLength,
            RowCount: 0,
            Columns: Array.Empty<ColumnMeta>()));

    public Task<QueryResult> QueryAsync(string sql) =>
        throw new NotSupportedException(
            "DuckDB-WASM runs in the browser only. Run `dotnet run --project DataDuck.Browser` " +
            "and open the printed URL to execute SQL.");

    private static string ToTableName(string fileName)
    {
        var dot = fileName.LastIndexOf('.');
        var stem = dot > 0 ? fileName[..dot] : fileName;
        var chars = stem.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
            if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_')
                chars[i] = '_';
        var name = new string(chars);
        if (name.Length == 0 || char.IsDigit(name[0]))
            name = "t_" + name;
        return name;
    }
}
