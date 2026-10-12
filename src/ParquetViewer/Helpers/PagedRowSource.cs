using ParquetViewer.Exceptions;
using System;
using System.Collections.Generic;
using System.Data;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace ParquetViewer.Helpers;

/// <summary>
/// Reads <paramref name="count"/> rows starting at row <paramref name="offset"/> of the open file(s).
/// </summary>
public delegate Task<DataTable> ReadSliceDelegate(long offset, int count, CancellationToken cancellationToken);

/// <summary>
/// Serves the rows of the open file(s) one page at a time so large files don't have to be loaded all at once.
/// </summary>
/// <remarks>Not thread safe. Only request one page at a time.</remarks>
public abstract class PagedRowSource
{
    private const string ROW_INDEX_COLUMN_BASE_NAME = "__ParquetViewer_RowIndex__";

    protected ReadSliceDelegate ReadSlice { get; }

    /// <summary>
    /// Total number of rows in the open file(s)
    /// </summary>
    public long RecordCount { get; }

    public int PageSize { get; }

    /// <summary>
    /// Name of the hidden column added to every page that holds each row's position in the file(s)
    /// </summary>
    public string RowIndexColumnName { get; }

    /// <summary>
    /// True once the total number of rows this source serves is known
    /// </summary>
    public abstract bool IsComplete { get; }

    /// <summary>
    /// Number of rows this source is known to serve. This is the final count once <see cref="IsComplete"/> is true.
    /// </summary>
    public abstract long KnownRowCount { get; }

    /// <summary>
    /// Number of pages this source is known to serve
    /// </summary>
    public abstract int KnownPageCount { get; }

    protected PagedRowSource(ReadSliceDelegate readSlice, long recordCount, int pageSize, string rowIndexColumnName)
    {
        ArgumentNullException.ThrowIfNull(readSlice);
        ArgumentOutOfRangeException.ThrowIfNegative(recordCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);
        ArgumentException.ThrowIfNullOrWhiteSpace(rowIndexColumnName);

        ReadSlice = readSlice;
        RecordCount = recordCount;
        PageSize = pageSize;
        RowIndexColumnName = rowIndexColumnName;
    }

    /// <returns>The rows of the page including the <see cref="RowIndexColumnName"/> column, or null if the page doesn't exist</returns>
    public abstract Task<DataTable?> GetPageAsync(int pageIndex, IProgress<double>? scanProgress, CancellationToken cancellationToken);

    /// <summary>
    /// Reads every row this source serves in chunks of up to <paramref name="chunkSize"/> rows. Used for exports.
    /// At least one (possibly empty) chunk is always returned so callers can read the schema.
    /// </summary>
    /// <remarks>The chunks don't include the <see cref="RowIndexColumnName"/> column</remarks>
    public abstract IAsyncEnumerable<DataTable> ReadAllChunksAsync(int chunkSize, CancellationToken cancellationToken);

    /// <summary>
    /// Picks a name for the row index column that doesn't collide with any field names
    /// </summary>
    public static string GetRowIndexColumnName(IEnumerable<string> fieldNames)
    {
        //DataTable column names are case insensitive
        var existingNames = new HashSet<string>(fieldNames, StringComparer.OrdinalIgnoreCase);
        var name = ROW_INDEX_COLUMN_BASE_NAME;
        while (existingNames.Contains(name))
        {
            name += "_";
        }
        return name;
    }

    /// <summary>
    /// Adds the row index column to <paramref name="dataTable"/>, numbering its rows from <paramref name="startIndex"/>
    /// </summary>
    public static void AddRowIndexColumn(DataTable dataTable, string rowIndexColumnName, long startIndex)
    {
        var column = dataTable.Columns.Add(rowIndexColumnName, typeof(long));
        var rowIndex = startIndex;
        foreach (DataRow row in dataTable.Rows)
        {
            row[column] = rowIndex++;
        }
    }
}

/// <summary>
/// Pages through every row of the open file(s)
/// </summary>
public sealed class UnfilteredPageSource : PagedRowSource
{
    public override bool IsComplete => true;

    public override long KnownRowCount => RecordCount;

    public override int KnownPageCount => (int)Math.Ceiling(RecordCount / (double)PageSize);

    public UnfilteredPageSource(ReadSliceDelegate readSlice, long recordCount, int pageSize, string rowIndexColumnName)
        : base(readSlice, recordCount, pageSize, rowIndexColumnName)
    {
    }

    public override async Task<DataTable?> GetPageAsync(int pageIndex, IProgress<double>? scanProgress, CancellationToken cancellationToken)
    {
        long offset = (long)pageIndex * PageSize;
        if (pageIndex < 0 || offset >= RecordCount)
            return null;

        var page = await ReadSlice(offset, PageSize, cancellationToken);
        AddRowIndexColumn(page, RowIndexColumnName, offset);
        return page;
    }

    public override async IAsyncEnumerable<DataTable> ReadAllChunksAsync(int chunkSize, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkSize);

        long offset = 0;
        do
        {
            yield return await ReadSlice(offset, chunkSize, cancellationToken);
            offset += chunkSize;
        } while (offset < RecordCount);
    }
}

/// <summary>
/// Pages through the rows of the open file(s) that match a <see cref="DataView.RowFilter"/> expression.
/// The file is scanned incrementally: only as far as needed to fill the pages that have been requested.
/// </summary>
public sealed class FilteredPageSource : PagedRowSource
{
    public const int DEFAULT_SCAN_CHUNK_SIZE = 10_000;

    /// <summary>
    /// Where a page's first match is: the start of the chunk it's in and the number of earlier matches in that chunk to skip.
    /// </summary>
    private readonly record struct Checkpoint(long ChunkStart, int MatchesToSkip);

    private readonly int _scanChunkSize;

    //Checkpoint for the start of every page found so far. The last entry is where the next (unscanned) page starts.
    private readonly List<Checkpoint> _pageCheckpoints = [new Checkpoint(0, 0)];
    private long _matchCount;
    private long _scanPosition;
    private bool _isComplete;

    public string RowFilter { get; }

    public override bool IsComplete => _isComplete;

    public override long KnownRowCount => _matchCount;

    public override int KnownPageCount => _pageCheckpoints.Count - 1;

    /// <summary>
    /// Fraction of the file that has been scanned for matches (0 to 1)
    /// </summary>
    public double ScannedFraction => _isComplete || RecordCount == 0 ? 1 : Math.Min(1, _scanPosition / (double)RecordCount);

    public FilteredPageSource(ReadSliceDelegate readSlice, long recordCount, int pageSize, string rowIndexColumnName,
        string rowFilter, int scanChunkSize = DEFAULT_SCAN_CHUNK_SIZE)
        : base(readSlice, recordCount, pageSize, rowIndexColumnName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rowFilter);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(scanChunkSize);

        RowFilter = rowFilter;
        _scanChunkSize = scanChunkSize;
    }

    /// <summary>
    /// Checks the filter is syntactically valid and only references existing columns
    /// </summary>
    /// <exception cref="InvalidQueryException"></exception>
    public static void Validate(string rowFilter, DataTable schema)
    {
        using var emptyTable = schema.Clone();
        try
        {
            emptyTable.DefaultView.RowFilter = rowFilter;
        }
        catch (Exception ex)
        {
            throw new InvalidQueryException(ex);
        }
    }

    public override async Task<DataTable?> GetPageAsync(int pageIndex, IProgress<double>? scanProgress, CancellationToken cancellationToken)
    {
        if (pageIndex < 0)
            return null;

        //Scan forward until the page is found
        while (pageIndex >= KnownPageCount)
        {
            if (_isComplete)
                return null;

            var frontierPageIndex = KnownPageCount;
            var (page, nextCheckpoint, reachedEnd) = await BuildPageAsync(_pageCheckpoints[frontierPageIndex], scanProgress, cancellationToken);

            //Only update our state once the page is complete so a cancellation doesn't leave us in a bad state
            if (page.Rows.Count > 0)
            {
                _pageCheckpoints.Add(nextCheckpoint);
                _matchCount += page.Rows.Count;
            }
            _isComplete = reachedEnd;
            scanProgress?.Report(ScannedFraction);

            if (frontierPageIndex == pageIndex)
            {
                if (page.Rows.Count > 0)
                    return page;

                page.Dispose();
                return null;
            }

            page.Dispose();
        }

        //We've seen this page before. Rebuild it from its checkpoint.
        var (rebuiltPage, _, _) = await BuildPageAsync(_pageCheckpoints[pageIndex], null, cancellationToken);
        return rebuiltPage;
    }

    private async Task<(DataTable Page, Checkpoint NextCheckpoint, bool ReachedEnd)> BuildPageAsync(
        Checkpoint checkpoint, IProgress<double>? scanProgress, CancellationToken cancellationToken)
    {
        DataTable? page = null;
        var chunkStart = checkpoint.ChunkStart;
        var matchesToSkip = checkpoint.MatchesToSkip;
        while (true)
        {
            var (chunk, matches) = await ScanChunkAsync(chunkStart, cancellationToken);
            using (chunk)
            {
                page ??= chunk.Clone();
                var takeCount = Math.Clamp(matches.Length - matchesToSkip, 0, PageSize - page.Rows.Count);
                for (var i = matchesToSkip; i < matchesToSkip + takeCount; i++)
                {
                    page.LoadDataRow(matches[i].ItemArray, true);
                }

                var isLastChunk = chunk.Rows.Count < _scanChunkSize || chunkStart + _scanChunkSize >= RecordCount;
                _scanPosition = Math.Max(_scanPosition, chunkStart + chunk.Rows.Count);
                scanProgress?.Report(ScannedFraction);

                var consumedMatchCount = matchesToSkip + takeCount;
                if (page.Rows.Count == PageSize)
                {
                    var hasMoreMatchesInChunk = consumedMatchCount < matches.Length;
                    var nextCheckpoint = hasMoreMatchesInChunk
                        ? new Checkpoint(chunkStart, consumedMatchCount)
                        : new Checkpoint(chunkStart + _scanChunkSize, 0);
                    return (page, nextCheckpoint, isLastChunk && !hasMoreMatchesInChunk);
                }

                if (isLastChunk)
                    return (page, new Checkpoint(chunkStart + _scanChunkSize, 0), true);
            }

            matchesToSkip = 0;
            chunkStart += _scanChunkSize;
        }
    }

    public override async IAsyncEnumerable<DataTable> ReadAllChunksAsync(int chunkSize, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkSize);

        DataTable? buffer = null;
        var hasYielded = false;
        long offset = 0;
        do
        {
            var (chunk, matches) = await ScanChunkAsync(offset, cancellationToken);
            using (chunk)
            {
                if (buffer is null)
                {
                    //Exports don't include the row index column
                    buffer = chunk.Clone();
                    buffer.Columns.Remove(RowIndexColumnName);
                }

                foreach (var match in matches)
                {
                    buffer.LoadDataRow(match.ItemArray[..^1], true); //The row index column is the last one
                    if (buffer.Rows.Count >= chunkSize)
                    {
                        var fullBuffer = buffer;
                        buffer = fullBuffer.Clone();
                        yield return fullBuffer;
                        hasYielded = true;
                    }
                }
            }

            offset += _scanChunkSize;
        } while (offset < RecordCount);

        if (buffer is not null && (buffer.Rows.Count > 0 || !hasYielded))
            yield return buffer;
    }

    /// <summary>
    /// Reads a chunk of the file and finds its matches, in file order
    /// </summary>
    private async Task<(DataTable Chunk, DataRow[] Matches)> ScanChunkAsync(long chunkStart, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var chunk = await ReadSlice(chunkStart, _scanChunkSize, cancellationToken);

        //Filtering can take a while so keep it off the UI thread
        var matches = await Task.Run(() =>
        {
            AddRowIndexColumn(chunk, RowIndexColumnName, chunkStart);
            try
            {
                //Select() doesn't preserve the file's row order on its own (it can use an index on the filtered column) so sort by the row index
                return chunk.Select(RowFilter, $"[{RowIndexColumnName}] ASC");
            }
            catch (Exception ex)
            {
                throw new InvalidQueryException(ex);
            }
        }, cancellationToken);

        return (chunk, matches);
    }
}
