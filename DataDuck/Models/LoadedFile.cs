using System.Collections.Generic;

namespace DataDuck.Models;

public sealed record LoadedFile(
    string Name,
    string TableName,
    long SizeBytes,
    int RowCount,
    IReadOnlyList<ColumnMeta> Columns);

public sealed record ColumnMeta(string Name, string Type);
