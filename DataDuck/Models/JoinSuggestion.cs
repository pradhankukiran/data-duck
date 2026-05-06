namespace DataDuck.Models;

public sealed record JoinSuggestion(
    string LeftTable,
    string LeftColumn,
    string RightTable,
    string RightColumn,
    double Confidence,
    string GeneratedSql);
