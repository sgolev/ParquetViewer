using ParquetViewer.Helpers;
using System;

namespace ParquetViewer.Exceptions;

/// <summary>
/// Thrown when an export would write more rows than the target file format supports.
/// </summary>
public class ExportRowLimitExceededException : Exception
{
    //Both limits include the header row
    private const long XLS_MAX_ROW_COUNT = 65_536;
    private const long XLSX_MAX_ROW_COUNT = 1_048_576;

    public FileType FileType { get; }

    /// <summary>
    /// Maximum number of data rows (excluding the header row) the file format supports
    /// </summary>
    public long MaxDataRowCount { get; }

    public long RowCount { get; }

    /// <summary>
    /// True when <see cref="RowCount"/> is only the number of rows found so far, not the final count
    /// </summary>
    public bool IsLowerBound { get; }

    public ExportRowLimitExceededException(FileType fileType, long rowCount, bool isLowerBound)
        : base($"{fileType} exports support at most {GetMaxDataRowCount(fileType)} rows")
    {
        FileType = fileType;
        MaxDataRowCount = GetMaxDataRowCount(fileType) ?? long.MaxValue;
        RowCount = rowCount;
        IsLowerBound = isLowerBound;
    }

    /// <returns>The maximum number of data rows (excluding the header row) the file type supports, or null if there is no limit</returns>
    public static long? GetMaxDataRowCount(FileType fileType) => fileType switch
    {
        FileType.XLS => XLS_MAX_ROW_COUNT - 1,
        FileType.XLSX => XLSX_MAX_ROW_COUNT - 1,
        _ => null
    };

    public static void ThrowIfExceeded(FileType fileType, long rowCount, bool isLowerBound)
    {
        if (rowCount > GetMaxDataRowCount(fileType))
            throw new ExportRowLimitExceededException(fileType, rowCount, isLowerBound);
    }
}
