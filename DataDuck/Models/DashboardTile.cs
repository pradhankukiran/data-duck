using System;

namespace DataDuck.Models;

public sealed record DashboardTile(
    Guid Id,
    string Title,
    string Sql,
    DateTimeOffset Created);
