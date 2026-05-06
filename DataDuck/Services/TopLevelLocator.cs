using Avalonia.Controls;

namespace DataDuck.Services;

/// <summary>
/// Static accessor for the current TopLevel (set by MainView when attached).
/// Lets services like file pickers reach the storage provider without
/// taking a Visual reference through their constructors.
/// </summary>
public static class TopLevelLocator
{
    public static TopLevel? Current { get; set; }
}
