namespace DataDuck.Services;

public interface ILocalStore
{
    string? Get(string key);
    void Set(string key, string value);
    void Remove(string key);
}
