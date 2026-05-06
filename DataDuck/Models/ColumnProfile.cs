using System.Collections.Generic;

namespace DataDuck.Models;

/// <summary>
/// Per-column profile produced by <see cref="Services.IProfilingService"/>.
/// Numeric-only fields (<see cref="Mean"/>, <see cref="StdDev"/>) are null for non-numeric columns.
/// </summary>
public sealed record ColumnProfile(
    string ColumnName,
    string DataType,
    long DistinctCount,
    long NullCount,
    long TotalCount,
    string? MinValue,
    string? MaxValue,
    double? Mean,
    double? StdDev,
    IReadOnlyList<TopValue> TopValues);

public sealed record TopValue(string Value, long Count);
