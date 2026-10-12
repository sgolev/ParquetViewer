using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace ParquetViewer.Helpers;

/// <summary>
/// Exposes a stream of <see cref="DataTable"/> chunks as a single forward-only data reader,
/// so writers that accept an <see cref="IDataReader"/> can export data without loading all of it at once.
/// </summary>
/// <remarks>All chunks must share the same schema as the first one</remarks>
public sealed class ChunkedDataReader : DbDataReader
{
    private readonly IAsyncEnumerator<DataTable> _chunks;
    private readonly DataTable _schema;
    private readonly bool _hasRows;
    private DataTable? _currentChunk;
    private int _currentRowIndex = -1;
    private bool _isClosed;

    private ChunkedDataReader(IAsyncEnumerator<DataTable> chunks, DataTable firstChunk)
    {
        _chunks = chunks;
        _currentChunk = firstChunk;
        _schema = firstChunk.Clone();
        _hasRows = firstChunk.Rows.Count > 0; //Sources only return an empty first chunk when there are no rows at all
    }

    /// <summary>
    /// Reads the first chunk so the schema is known before any rows are requested
    /// </summary>
    public static async Task<ChunkedDataReader> CreateAsync(IAsyncEnumerable<DataTable> chunks, CancellationToken cancellationToken)
    {
        var enumerator = chunks.GetAsyncEnumerator(cancellationToken);
        if (!await enumerator.MoveNextAsync())
        {
            await enumerator.DisposeAsync();
            throw new InvalidOperationException("At least one chunk is required to read the schema");
        }

        return new ChunkedDataReader(enumerator, enumerator.Current);
    }

    public override bool Read()
    {
        while (_currentChunk is not null)
        {
            if (++_currentRowIndex < _currentChunk.Rows.Count)
                return true;

            _currentChunk.Dispose();
            _currentChunk = null;
            _currentRowIndex = -1;

            //Callers of IDataReader.Read() are synchronous. We're always on a thread pool thread here so blocking is fine.
            if (_chunks.MoveNextAsync().AsTask().GetAwaiter().GetResult())
                _currentChunk = _chunks.Current;
        }

        return false;
    }

    private DataRow CurrentRow => _currentChunk is not null && _currentRowIndex >= 0 && _currentRowIndex < _currentChunk.Rows.Count
        ? _currentChunk.Rows[_currentRowIndex]
        : throw new InvalidOperationException("No current row");

    public override object GetValue(int ordinal) => CurrentRow[ordinal];

    public override int GetValues(object[] values)
    {
        var itemArray = CurrentRow.ItemArray;
        var count = Math.Min(values.Length, itemArray.Length);
        Array.Copy(itemArray, values, count);
        return count;
    }

    public override object this[int ordinal] => GetValue(ordinal);
    public override object this[string name] => GetValue(GetOrdinal(name));
    public override int FieldCount => _schema.Columns.Count;
    public override int Depth => 0;
    public override bool HasRows => _hasRows;
    public override bool IsClosed => _isClosed;
    public override int RecordsAffected => -1;
    public override string GetName(int ordinal) => _schema.Columns[ordinal].ColumnName;
    public override int GetOrdinal(string name) => _schema.Columns.IndexOf(name);
    public override Type GetFieldType(int ordinal) => _schema.Columns[ordinal].DataType;
    public override string GetDataTypeName(int ordinal) => GetFieldType(ordinal).Name;
    public override bool IsDBNull(int ordinal) => GetValue(ordinal) == DBNull.Value;
    public override bool GetBoolean(int ordinal) => (bool)GetValue(ordinal);
    public override byte GetByte(int ordinal) => (byte)GetValue(ordinal);
    public override char GetChar(int ordinal) => (char)GetValue(ordinal);
    public override DateTime GetDateTime(int ordinal) => (DateTime)GetValue(ordinal);
    public override decimal GetDecimal(int ordinal) => (decimal)GetValue(ordinal);
    public override double GetDouble(int ordinal) => (double)GetValue(ordinal);
    public override float GetFloat(int ordinal) => (float)GetValue(ordinal);
    public override Guid GetGuid(int ordinal) => (Guid)GetValue(ordinal);
    public override short GetInt16(int ordinal) => (short)GetValue(ordinal);
    public override int GetInt32(int ordinal) => (int)GetValue(ordinal);
    public override long GetInt64(int ordinal) => (long)GetValue(ordinal);
    public override string GetString(int ordinal) => (string)GetValue(ordinal);
    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();
    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();
    public override bool NextResult() => false;
    public override IEnumerator GetEnumerator() => new DbEnumerator(this);
    public override DataTable GetSchemaTable()
    {
        using var schemaReader = _schema.CreateDataReader();
        return schemaReader.GetSchemaTable();
    }

    public override void Close()
    {
        if (_isClosed)
            return;

        _isClosed = true;
        _currentChunk?.Dispose();
        _currentChunk = null;
        _chunks.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Close();
            _schema.Dispose();
        }
        base.Dispose(disposing);
    }
}
