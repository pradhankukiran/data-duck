using System.Collections.Generic;

namespace DataDuck.Models;

public sealed record DatasetInsight(
    string Summary,
    IReadOnlyList<string> Findings,
    IReadOnlyList<SuggestedQuery> SuggestedQueries);

public sealed record SuggestedQuery(string Title, string Sql);
