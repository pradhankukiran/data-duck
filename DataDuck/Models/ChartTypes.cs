namespace DataDuck.Models;

public enum ChartKind
{
    None,
    BigNumber,
    BarHorizontal,
}

public sealed class BarPoint
{
    public string Label { get; init; } = string.Empty;
    public double Value { get; init; }
    public double Max { get; init; }
    public string Display { get; init; } = string.Empty;
}
