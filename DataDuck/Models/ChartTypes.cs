namespace DataDuck.Models;

public enum ChartKind
{
    None,
    BigNumber,
    BarHorizontal,
    Line,
    Pie,
}

public sealed class BarPoint
{
    public string Label { get; init; } = string.Empty;
    public double Value { get; init; }
    public double Max { get; init; }
    public string Display { get; init; } = string.Empty;
}

public sealed class LinePoint
{
    public double X { get; init; }            // numeric or epoch-millis for dates
    public double Y { get; init; }
    public string XLabel { get; init; } = ""; // formatted string for axis ticks
    public string YDisplay { get; init; } = "";
}

public sealed class PieSlice
{
    public string Label { get; init; } = "";
    public double Value { get; init; }
    public double Percent { get; init; }      // 0..100
    public string Display { get; init; } = "";
    public string ColorHex { get; init; } = "";  // 8 rotating accent shades
    public double StartAngleDeg { get; init; }
    public double SweepAngleDeg { get; init; }
}
