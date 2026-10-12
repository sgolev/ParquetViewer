using Parquet;
using Parquet.Schema;

namespace ParquetViewer.Engine.ParquetNET;

/// <summary>
/// Wraps a <see cref="ParquetRowGroupReader"/> so decoded columns are shared across reads of the same row group.
/// </summary>
/// <remarks>Paging through a row group would otherwise decode the entire column chunk again for every page.</remarks>
internal sealed class CachedRowGroupReader
{
    private readonly ParquetRowGroupReader _groupReader;
    private readonly ParquetReader _fileReader;
    private readonly int _rowGroupIndex;
    private readonly RowGroupColumnCache _cache;

    public long RowCount => _groupReader.RowCount;

    public CachedRowGroupReader(ParquetRowGroupReader groupReader, ParquetReader fileReader, int rowGroupIndex, RowGroupColumnCache cache)
    {
        _groupReader = groupReader;
        _fileReader = fileReader;
        _rowGroupIndex = rowGroupIndex;
        _cache = cache;
    }

    public async Task<CachedColumn> GetColumnAsync(ParquetSchemaElement field, CancellationToken cancellationToken)
    {
        if (field.DataField is not null && _cache.TryGet(_fileReader, _rowGroupIndex, field.DataField, out var cachedColumn))
            return cachedColumn;

        //Throws a friendly exception if the field has no data field
        var dataColumn = await ParquetEngine.ReadColumnAsync(_groupReader, field, cancellationToken);
        cachedColumn = new CachedColumn(dataColumn);
        _cache.Add(_fileReader, _rowGroupIndex, field.DataField!, cachedColumn);
        return cachedColumn;
    }
}

/// <summary>
/// Keeps the decoded columns of the most recently read row groups.
/// </summary>
/// <remarks>Not thread safe. The engine can't be read from concurrently anyway.</remarks>
internal sealed class RowGroupColumnCache
{
    private const int MAX_CACHED_ROW_GROUPS = 2;

    //Most recently used row group first. Columns are keyed by DataField instance (reference equality) because
    //leaf names such as `item`, `element` or `key` repeat across different columns of the same row group.
    private readonly LinkedList<(ParquetReader Reader, int RowGroupIndex, Dictionary<DataField, CachedColumn> Columns)> _rowGroups = new();

    public bool TryGet(ParquetReader reader, int rowGroupIndex, DataField field, out CachedColumn cachedColumn)
    {
        var node = FindRowGroup(reader, rowGroupIndex);
        if (node is not null && node.Value.Columns.TryGetValue(field, out var column))
        {
            MoveToFront(node);
            cachedColumn = column;
            return true;
        }

        cachedColumn = null!;
        return false;
    }

    public void Add(ParquetReader reader, int rowGroupIndex, DataField field, CachedColumn cachedColumn)
    {
        var node = FindRowGroup(reader, rowGroupIndex);
        if (node is null)
        {
            node = _rowGroups.AddFirst((reader, rowGroupIndex, new Dictionary<DataField, CachedColumn>(ReferenceEqualityComparer.Instance)));
            while (_rowGroups.Count > MAX_CACHED_ROW_GROUPS)
            {
                _rowGroups.RemoveLast();
            }
        }
        else
        {
            MoveToFront(node);
        }

        node.Value.Columns[field] = cachedColumn;
    }

    public void Clear() => _rowGroups.Clear();

    private LinkedListNode<(ParquetReader Reader, int RowGroupIndex, Dictionary<DataField, CachedColumn> Columns)>? FindRowGroup(ParquetReader reader, int rowGroupIndex)
    {
        for (var node = _rowGroups.First; node is not null; node = node.Next)
        {
            if (ReferenceEquals(node.Value.Reader, reader) && node.Value.RowGroupIndex == rowGroupIndex)
                return node;
        }
        return null;
    }

    private void MoveToFront(LinkedListNode<(ParquetReader Reader, int RowGroupIndex, Dictionary<DataField, CachedColumn> Columns)> node)
    {
        if (node != _rowGroups.First)
        {
            _rowGroups.Remove(node);
            _rowGroups.AddFirst(node);
        }
    }
}

/// <summary>
/// A decoded column chunk that can be read by row index without walking from the start of the row group.
/// </summary>
internal sealed class CachedColumn
{
    private PaddedColumnView? _paddedView;
    private int[]? _rowStarts;
    private int[]? _mapRowStarts;

    public Parquet.Data.DataColumn DataColumn { get; }

    public CachedColumn(Parquet.Data.DataColumn dataColumn)
    {
        DataColumn = dataColumn;
    }

    /// <summary>
    /// The column's data with empty/null entries padded in. Each column belongs to a single schema element.
    /// </summary>
    public PaddedColumnView GetPaddedView(ParquetSchemaElement field) => _paddedView ??= new PaddedColumnView(DataColumn, field);

    /// <summary>
    /// Level indexes where each row of a repeated column begins (repetition level 0).
    /// </summary>
    public int[] RowStarts
        => _rowStarts ??= BuildRowStarts(DataColumn.RepetitionLevels ?? throw new InvalidOperationException("Column has no repetition levels"));

    /// <summary>
    /// Pair indexes where each map row begins, when this column holds the keys of a map
    /// </summary>
    public int[] GetMapRowStarts(Func<int[]> buildRowStarts) => _mapRowStarts ??= buildRowStarts();

    private static int[] BuildRowStarts(int[] repetitionLevels)
    {
        //Mirrors the row ranges ListValueBuilder used to compute: the first row always starts at 0
        var rowStarts = new List<int>(repetitionLevels.Length) { 0 };
        for (var i = 1; i < repetitionLevels.Length; i++)
        {
            if (repetitionLevels[i] == 0)
                rowStarts.Add(i);
        }
        return [.. rowStarts];
    }
}

/// <summary>
/// Random access over a column's data with empty/null entries padded in so indexes line up with the definition and repetition levels.
/// Some parquet writers don't write null entries into the data array for empty and null lists, which would otherwise throw off our indexes.
/// </summary>
internal sealed class PaddedColumnView
{
    private readonly Array _data;
    private readonly int[]? _levelToDataIndex; //null when no padding is required. Then level index == data index.

    public int Length { get; }

    public PaddedColumnView(Parquet.Data.DataColumn dataColumn, ParquetSchemaElement field)
    {
        _data = dataColumn.Data;
        _levelToDataIndex = BuildLevelToDataIndex(dataColumn, field);
        Length = _levelToDataIndex?.Length ?? _data.Length;
    }

    public object this[int index]
    {
        get
        {
            var dataIndex = _levelToDataIndex is null ? index : _levelToDataIndex[index];
            if (dataIndex < 0)
                return DBNull.Value;

            return _data.GetValue(dataIndex) ?? DBNull.Value;
        }
    }

    private static int[]? BuildLevelToDataIndex(Parquet.Data.DataColumn dataColumn, ParquetSchemaElement field)
    {
        int levelCount = dataColumn.DefinitionLevels?.Length ?? 0;
        if (levelCount <= dataColumn.Data.Length)
            return null;

        var levelToDataIndex = new List<int>(levelCount);
        var index = -1;
        for (var dataIndex = 0; dataIndex < dataColumn.Data.Length; dataIndex++)
        {
            index++;

            while (dataColumn.IsEmpty(index, field) || dataColumn.IsNull(index, field))
            {
                levelToDataIndex.Add(-1);
                index++;
            }

            levelToDataIndex.Add(dataIndex);
        }

        //Need to handle case where last N rows are null/empty
        while (levelCount > index + 1)
        {
            levelToDataIndex.Add(-1);
            index++;
        }

        return [.. levelToDataIndex];
    }
}
