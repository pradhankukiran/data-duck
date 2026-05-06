using System;

namespace DataDuck.Models;

public sealed record SavedQuery(
    string Sql,
    DateTimeOffset When,
    long ElapsedMs);
