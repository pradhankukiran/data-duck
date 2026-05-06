using System.IO;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;

namespace DataDuck.Services;

/// <summary>
/// IFileService implementation backed by Avalonia's IStorageProvider.
/// Works the same on Browser and Desktop heads — System.IO.File doesn't.
/// </summary>
public sealed class StorageProviderFileService : IFileService
{
    private static readonly FilePickerFileType[] DataFileTypes =
    {
        new("Data files")
        {
            Patterns = new[] { "*.csv", "*.parquet", "*.json", "*.jsonl", "*.ndjson" },
            MimeTypes = new[] { "text/csv", "application/json", "application/x-ndjson" },
        },
        FilePickerFileTypes.All,
    };

    public async Task<UploadedFile?> PickAsync()
    {
        var top = TopLevelLocator.Current;
        if (top is null) return null;

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open data file",
            AllowMultiple = false,
            FileTypeFilter = DataFileTypes,
        });

        if (files.Count == 0) return null;

        var file = files[0];
        return await ReadAsync(file);
    }

    public static async Task<UploadedFile> ReadAsync(IStorageFile file)
    {
        await using var stream = await file.OpenReadAsync();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory);
        return new UploadedFile(file.Name, memory.ToArray());
    }
}
