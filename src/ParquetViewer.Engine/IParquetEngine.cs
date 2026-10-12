using System.Data;

namespace ParquetViewer.Engine;

public interface IParquetEngine : IDisposable
{
    List<string> Fields { get; }
    long RecordCount { get; }
    int NumberOfPartitions { get; }
    Dictionary<string, string> CustomMetadata { get; }
    string Path { get; }
    IParquetMetadata Metadata { get; }

    Task<Func<bool, DataTable>> ReadRowsAsync(List<string> selectedFields, long offset, int recordCount,
        IProgress<int>? progress = null, CancellationToken cancellationToken = default);

    /// <param name="dataChunks">The rows to write. At least one chunk is required and all chunks must share the same schema.</param>
    Task WriteDataToParquetFileAsync(IAsyncEnumerable<DataTable> dataChunks, string path, IProgress<int> progress,
        Dictionary<string, string>? customMetadata, CancellationToken cancellationToken);

    IEnumerable<string> GetOpenParquetFilePaths();
}