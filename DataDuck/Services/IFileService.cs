using System.Threading.Tasks;

namespace DataDuck.Services;

public interface IFileService
{
    Task<UploadedFile?> PickAsync();
}

public sealed record UploadedFile(string Name, byte[] Data);
