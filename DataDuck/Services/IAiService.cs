using System.Collections.Generic;
using System.Threading.Tasks;
using DataDuck.Models;

namespace DataDuck.Services;

public interface IAiService
{
    bool HasApiKey { get; }
    Task<string> GenerateSqlAsync(string englishQuestion, IReadOnlyList<LoadedFile> tables);
}
