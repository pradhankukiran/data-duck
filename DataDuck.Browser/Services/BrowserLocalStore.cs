using System.Runtime.InteropServices.JavaScript;
using DataDuck.Services;

namespace DataDuck.Browser.Services;

public partial class BrowserLocalStore : ILocalStore
{
    public string? Get(string key) => GetItemImpl(key);
    public void Set(string key, string value) => SetItemImpl(key, value);
    public void Remove(string key) => RemoveItemImpl(key);

    [JSImport("globalThis.localStorage.getItem")]
    internal static partial string? GetItemImpl(string key);

    [JSImport("globalThis.localStorage.setItem")]
    internal static partial void SetItemImpl(string key, string value);

    [JSImport("globalThis.localStorage.removeItem")]
    internal static partial void RemoveItemImpl(string key);
}
