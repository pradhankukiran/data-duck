using System.Collections.Generic;
using System.Threading.Tasks;
using DataDuck.Models;

namespace DataDuck.Services;

public interface IProfilingService
{
    Task<IReadOnlyList<ColumnProfile>> ProfileAsync(LoadedFile file);
}
