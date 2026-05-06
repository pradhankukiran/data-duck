using System.Threading.Tasks;
using DataDuck.Models;

namespace DataDuck.Services;

public interface IDuckDbService
{
    Task InitAsync();
    Task<LoadedFile> RegisterFileAsync(string fileName, byte[] data);
    Task<QueryResult> QueryAsync(string sql);
}
