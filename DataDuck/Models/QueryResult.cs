using System.Collections.Generic;

namespace DataDuck.Models;

public sealed record QueryResult(
    IReadOnlyList<string> ColumnNames,
    IReadOnlyList<object?[]> Rows,
    long ElapsedMs);
