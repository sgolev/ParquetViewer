using MiniExcelLibs;
using ParquetViewer.Analytics;
using ParquetViewer.Engine;
using ParquetViewer.Engine.Exceptions;
using ParquetViewer.Exceptions;
using ParquetViewer.Helpers;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ParquetViewer;

public partial class MainForm
{
    public LoadingIcon ShowLoadingIcon(string message, long loadingBarMax = 0)
    {
        var loadingIcon = new LoadingIcon(this, message, loadingBarMax);
        loadingIcon.OnShow += (sender, e) =>
        {
            mainTableLayoutPanel.Enabled = false;
            mainMenuStrip.Enabled = false;
            mainStatusStrip.Enabled = false;
        };
        loadingIcon.OnHide += (sender, e) =>
        {
            mainTableLayoutPanel.Enabled = true;
            mainMenuStrip.Enabled = true;
            mainStatusStrip.Enabled = true;
        };

        loadingIcon.Show();
        return loadingIcon;
    }

    //TODO: Should we export floats and binary data with custom formatting if activated?
    //E.g. float -> Decimal format, Binary -> Size format, etc.
    //We can't use the gridview formattedValue directly as we're changing the type sometimes plus enumerating the dgv is really slow due to row unsharing.
    private async void ExportResults(FileType defaultFileType, string? filePathWithExtension = null)
    {
        string? filePath = null;
        LoadingIcon? loadingIcon = null;
        FileType? rerunType = null;
        filePathWithExtension = string.IsNullOrWhiteSpace(filePathWithExtension) ? null : filePathWithExtension;
        try
        {
            var (exportRowCount, isExactRowCount) = GetExportRowCount();
            if (MainDataSource is not null && _pageSource is not null && (exportRowCount > 0 || !isExactRowCount))
            {
                exportFileDialog.Title = isExactRowCount
                    ? Resources.Strings.RecordsToBeExportedTitleFormat.Format(exportRowCount)
                    : Resources.Strings.MatchingRecordsToBeExportedTitle;
                exportFileDialog.Filter = "CSV file (*.csv)|*.csv|JSON file (*.json)|*.json|Excel '93 file (*.xls)|*.xls|Excel '07 file (*.xlsx)|*.xlsx";
                exportFileDialog.FilterIndex = (int)defaultFileType + 1;

                if (_openParquetEngine?.Metadata.SchemaTree?.Children.All(s => s.IsPrimitive) == true
                    && _openParquetEngine is Engine.ParquetNET.ParquetEngine)
                {
                    exportFileDialog.Filter += "|Parquet file (*.parquet)|*.parquet";
                }

                if (filePathWithExtension is not null || exportFileDialog.ShowDialog() == DialogResult.OK)
                {
                    filePath = filePathWithExtension ?? exportFileDialog.FileName;
                    CleanupFile(filePath); //Delete any existing file (user already confirmed any overwrite)

                    var fileExtension = Path.GetExtension(filePath);
                    FileType? selectedFileType = UtilityMethods.ExtensionToFileType(fileExtension) ?? throw new ArgumentOutOfRangeException(fileExtension);

                    //Don't bother reading anything if we already know the file format can't hold this many rows
                    ExportRowLimitExceededException.ThrowIfExceeded(selectedFileType.Value, exportRowCount, !isExactRowCount);

                    var stopWatch = Stopwatch.StartNew();
                    var columnCount = GetUserColumns().Count();
                    loadingIcon = ShowLoadingIcon(Resources.Strings.ExportingDataLabelText, isExactRowCount ? exportRowCount * columnCount : 0);
                    using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(loadingIcon.CancellationToken, _loadCts.Token);

                    var rowCounter = new ExportRowCounter();
                    var dataChunks = CountExportedRows(GetExportChunks(cancellation.Token), selectedFileType.Value, rowCounter, cancellation.Token);
                    await ExportResultsImpl(dataChunks, columnCount, selectedFileType.Value, _openParquetEngine,
                        filePath, loadingIcon, OpenFileOrFolderPath, cancellation.Token);
                    cancellation.Token.ThrowIfCancellationRequested(); //Some writers stop early instead of throwing

                    long fileSizeInBytes = new FileInfo(filePath).Length;

                    FileExportEvent.FireAndForget(
                        selectedFileType.Value,
                        fileSizeInBytes,
                        (int)Math.Min(rowCounter.Count, int.MaxValue),
                        columnCount,
                        stopWatch.ElapsedMilliseconds);

                    MessageBox.Show(this,
                        Resources.Strings.ExportSuccessfulMessageFormat.Format(Math.Round((fileSizeInBytes / 1024.0) / 1024.0, 2)),
                        Resources.Strings.ExportSuccessfulTitle,
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }
        catch (Exception ex) when (FindInnerException<ExportRowLimitExceededException>(ex) is { } rowLimitException)
        {
            //Some writers (e.g. MiniExcel) wrap exceptions thrown while reading the data
            CleanupFile(filePath);
            ShowExportRowLimitExceededError(rowLimitException);
        }
        catch (OperationCanceledException)
        {
            CleanupFile(filePath);
            MessageBox.Show(Resources.Strings.ExportCancelledMessage, Resources.Strings.ExportCancelledTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (IOException ex)
        {
            CleanupFile(filePath);
            ShowError(ex.Message, Resources.Errors.ExportFailedErrorTitle);
        }
        catch (XlsCellLengthException ex)
        {
            CleanupFile(filePath);

            if (MessageBox.Show(this,
                Resources.Strings.SwitchFromXlsToXlsxMessageFormat.Format(ex.MaxLength, ex.FileType.GetExtension(), FileType.XLSX.GetExtension()),
                Resources.Strings.SwitchFromXlsToXlsxMessageTitle,
                MessageBoxButtons.OKCancel, MessageBoxIcon.Exclamation) == DialogResult.OK)
            {
                rerunType = FileType.XLSX;
            }
        }
        catch (Exception)
        {
            CleanupFile(filePath);
            throw;
        }
        finally
        {
            loadingIcon?.Dispose();
        }

        if (rerunType is not null)
        {
            ExportResults(default, filePath is not null ? Path.ChangeExtension(filePath, rerunType.Value.GetExtension()) : filePath);
        }

        static void CleanupFile(string? filePath)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(filePath))
                    File.Delete(filePath);
            }
            catch (Exception) { /*Swallow*/ }
        }
    }

    private sealed class ExportRowCounter
    {
        public long Count { get; set; }
    }

    /// <summary>
    /// The number of rows an export will write
    /// </summary>
    /// <returns>The row count, and whether it's exact or only the number of query matches found so far</returns>
    private (long RowCount, bool IsExact) GetExportRowCount()
    {
        if (MainDataSource is null || _pageSource is null)
            return (0, true);

        if (_isFullLoad)
            return (MainDataSource.DefaultView.Count, true);

        return (_pageSource.KnownRowCount, _pageSource.IsComplete);
    }

    /// <summary>
    /// Every row the user can page through: the entire file, or all matches of the active query
    /// </summary>
    private IAsyncEnumerable<DataTable> GetExportChunks(CancellationToken cancellationToken)
    {
        //Everything is in memory already. Exporting the view also keeps the user's sort order.
        if (_isFullLoad)
            return ReadViewInChunks(MainDataSource!.DefaultView, GetUserColumns().ToList()).ToAsyncEnumerable();

        return _pageSource!.ReadAllChunksAsync(EXPORT_CHUNK_SIZE, cancellationToken);
    }

    /// <summary>
    /// Copies the rows of <paramref name="dataView"/> a chunk at a time so the whole view isn't duplicated in memory
    /// </summary>
    private static IEnumerable<DataTable> ReadViewInChunks(DataView dataView, List<DataColumn> columns)
    {
        using var schema = new DataTable();
        foreach (var column in columns)
        {
            schema.Columns.Add(column.ColumnName, column.DataType);
        }

        var chunk = schema.Clone();
        for (var i = 0; i < dataView.Count; i++)
        {
            var row = dataView[i].Row;
            chunk.LoadDataRow(columns.Select(column => row[column]).ToArray(), true);
            if (chunk.Rows.Count == EXPORT_CHUNK_SIZE)
            {
                yield return chunk;
                chunk = schema.Clone();
            }
        }

        if (chunk.Rows.Count > 0 || dataView.Count == 0) //Always return at least one chunk so the schema can be written
            yield return chunk;
    }

    /// <summary>
    /// Counts the rows passing through and stops the export as soon as the file format's row limit is exceeded
    /// </summary>
    private static async IAsyncEnumerable<DataTable> CountExportedRows(IAsyncEnumerable<DataTable> dataChunks, FileType fileType,
        ExportRowCounter rowCounter, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var dataChunk in dataChunks.WithCancellation(cancellationToken))
        {
            rowCounter.Count += dataChunk.Rows.Count;
            ExportRowLimitExceededException.ThrowIfExceeded(fileType, rowCounter.Count, true);
            yield return dataChunk;
        }
    }

    private static T? FindInnerException<T>(Exception? ex) where T : Exception
    {
        while (ex is not null)
        {
            if (ex is T match)
                return match;

            ex = ex.InnerException;
        }
        return null;
    }

    private void ShowExportRowLimitExceededError(ExportRowLimitExceededException ex)
        => MessageBox.Show(this,
            Resources.Errors.ExportRowLimitExceededErrorMessageFormat.Format(
                ex.FileType.GetExtension(),
                ex.MaxDataRowCount,
                ex.IsLowerBound ? $"{ex.RowCount:N0}+" : $"{ex.RowCount:N0}"),
            Resources.Errors.ExportRowLimitExceededErrorTitle,
            MessageBoxButtons.OK, MessageBoxIcon.Warning);

    private static Task ExportResultsImpl(IAsyncEnumerable<DataTable> dataChunks, int columnCount, FileType selectedFileType, IParquetEngine? engine,
        string filePath, IProgress<int> progress, string? sourceFileOrFolderPath, CancellationToken cancellationToken)
    {
        if (selectedFileType == FileType.CSV)
        {
            return WriteDataToCSVFile(dataChunks, filePath, cancellationToken, progress);
        }
        else if (selectedFileType == FileType.XLS)
        {
            const int MAX_XLS_COLUMN_COUNT = 256; //.xls format has a hard limit on 256 columns
            if (columnCount > MAX_XLS_COLUMN_COUNT)
            {
                MessageBox.Show(
                    Resources.Errors.TooManyColumnsXlsErrorMessageFormat.Format(MAX_XLS_COLUMN_COUNT, columnCount),
                    Resources.Errors.TooManyColumnsErrorTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);

                return Task.CompletedTask;
            }

            return WriteDataToExcel93File(dataChunks, filePath, cancellationToken, progress);
        }
        else if (selectedFileType == FileType.XLSX)
        {
            const int MAX_XLSX_COLUMN_COUNT = 16384; //.xlsx format has a hard limit on 16384 columns
            if (columnCount > MAX_XLSX_COLUMN_COUNT)
            {
                MessageBox.Show(
                    Resources.Errors.TooManyColumnsXlsxErrorMessageFormat.Format(MAX_XLSX_COLUMN_COUNT, columnCount),
                    Resources.Errors.TooManyColumnsErrorTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);

                return Task.CompletedTask;
            }

            var sheetName = Path.GetFileNameWithoutExtension(sourceFileOrFolderPath) ?? "Sheet1";
            return WriteDataToExcel2007File(dataChunks, filePath, sheetName, cancellationToken, progress);
        }
        else if (selectedFileType == FileType.JSON)
        {
            return WriteDataToJSONFile(dataChunks, filePath, cancellationToken, progress);
        }
        else if (selectedFileType == FileType.PARQUET)
        {
            ArgumentNullException.ThrowIfNull(engine);
            var engineTypeName = engine is Engine.ParquetNET.ParquetEngine ? "ParquetNET" : "DuckDB";
            return WriteDataToParquetFile(engine, dataChunks, filePath, progress, engineTypeName, cancellationToken);
        }
        else
        {
            throw new Exception(Resources.Errors.UnsupportedExportTypeFormat.Format(selectedFileType.ToString()));
        }
    }

    //The writers run on a background thread as reading the data chunks can take a while

    internal static Task WriteDataToExcel2007File(IAsyncEnumerable<DataTable> dataChunks, string path, string sheetName, CancellationToken cancellationToken, IProgress<int> progress)
        => Task.Run(async () =>
            {
                const int MAX_XLSX_SHEET_NAME_LENGTH = 31;

                //sanitize sheet name
                sheetName = Regex.Replace(sheetName, "[^a-zA-Z0-9 _\\-()]", string.Empty).Left(MAX_XLSX_SHEET_NAME_LENGTH);

                using var fs = new FileStream(path, FileMode.OpenOrCreate);
                using var dataReader = await ChunkedDataReader.CreateAsync(dataChunks, cancellationToken);
                await fs.SaveAsAsync(dataReader, printHeader: true, sheetName, ExcelType.XLSX, configuration: null, progress, cancellationToken);
            }, cancellationToken);

    internal static Task WriteDataToCSVFile(IAsyncEnumerable<DataTable> dataChunks, string path, CancellationToken cancellationToken, IProgress<int> progress)
        => Task.Run(async () =>
            {
                using var writer = new StreamWriter(path, false, Encoding.UTF8);

                var rowBuilder = new StringBuilder();
                var isHeaderWritten = false;
                string dateFormat = AppSettings.DateTimeDisplayFormat.GetDateFormat();
                string dateOnlyFormat = AppSettings.DateTimeDisplayFormat.GetDateOnlyFormat();
                string timeOnlyFormat = AppSettings.DateTimeDisplayFormat.GetTimeOnlyFormat();
                await foreach (var dataTable in dataChunks.WithCancellation(cancellationToken))
                {
                    if (!isHeaderWritten)
                    {
                        bool isFirst = true;
                        foreach (DataColumn column in dataTable.Columns)
                        {
                            if (!isFirst)
                            {
                                rowBuilder.Append(',');
                            }
                            else
                            {
                                isFirst = false;
                            }

                            rowBuilder.Append(
                                column.ColumnName
                                    .Replace("\r", string.Empty)
                                    .Replace("\n", string.Empty)
                                    .Replace(",", string.Empty));
                        }
                        writer.WriteLine(rowBuilder.ToString());
                        isHeaderWritten = true;
                    }

                    foreach (DataRow row in dataTable.Rows)
                    {
                        rowBuilder.Clear();

                        var isFirst = true;
                        foreach (object? value in row.ItemArray)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                break;
                            }

                            if (!isFirst)
                            {
                                rowBuilder.Append(',');
                            }
                            else
                            {
                                isFirst = false;
                            }

                            if (value is DateTime dt)
                            {
                                rowBuilder.Append(UtilityMethods.CleanCSVValue(dt.ToString(dateFormat)));
                            }
                            else if (value is DateOnly dateOnly)
                            {
                                rowBuilder.Append(UtilityMethods.CleanCSVValue(dateOnly.ToString(dateOnlyFormat)));
                            }
                            else if (value is TimeOnly timeOnly)
                            {
                                rowBuilder.Append(UtilityMethods.CleanCSVValue(timeOnly.ToString(timeOnlyFormat)));
                            }
                            else
                            {
                                var stringValue = value!.ToString()!; //we never have `null` only `DBNull.Value`
                                rowBuilder.Append(UtilityMethods.CleanCSVValue(stringValue));
                            }

                            progress.Report(1);
                        }

                        writer.WriteLine(rowBuilder.ToString());
                    }
                }
            }, cancellationToken);

    internal static Task WriteDataToExcel93File(IAsyncEnumerable<DataTable> dataChunks, string path, CancellationToken cancellationToken, IProgress<int> progress)
        => Task.Run(async () =>
            {
                string dateFormat = AppSettings.DateTimeDisplayFormat.GetDateFormat();
                string dateOnlyFormat = AppSettings.DateTimeDisplayFormat.GetDateOnlyFormat();
                string timeOnlyFormat = AppSettings.DateTimeDisplayFormat.GetTimeOnlyFormat();
                using var fs = new FileStream(path, FileMode.OpenOrCreate);
                var excelWriter = new ExcelWriter(fs);
                excelWriter.BeginWrite();

                var isHeaderWritten = false;
                var rowIndex = 0; //Row 0 is the header row
                await foreach (var dataTable in dataChunks.WithCancellation(cancellationToken))
                {
                    //Write headers
                    if (!isHeaderWritten)
                    {
                        for (int i = 0; i < dataTable.Columns.Count; i++)
                        {
                            excelWriter.WriteCell(0, i, dataTable.Columns[i].ColumnName);
                        }
                        isHeaderWritten = true;
                    }

                    //Write data
                    foreach (DataRow row in dataTable.Rows)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }

                        rowIndex++;
                        for (int j = 0; j < dataTable.Columns.Count; j++)
                        {
                            var value = row[j];
                            if (value == DBNull.Value)
                            {
                                excelWriter.WriteCell(rowIndex, j); //empty cell
                            }
                            else if (IsIntCastSafe(value))
                            {
                                excelWriter.WriteCell(rowIndex, j, Convert.ToInt32(value));
                            }
                            else if (IsDoubleCastSafe(value))
                            {
                                excelWriter.WriteCell(rowIndex, j, Convert.ToDouble(value));
                            }
                            else if (value is DateTime dt)
                            {
                                excelWriter.WriteCell(rowIndex, j, dt.ToString(dateFormat));
                            }
                            else if (value is DateOnly dateOnly)
                            {
                                excelWriter.WriteCell(rowIndex, j, dateOnly.ToString(dateOnlyFormat));
                            }
                            else if (value is TimeOnly timeOnly)
                            {
                                excelWriter.WriteCell(rowIndex, j, timeOnly.ToString(timeOnlyFormat));
                            }
                            else
                            {
                                var stringValue = value.ToString();

                                //BUG: for some reason strings longer than 255 characters appear empty.
                                const int maxSupportedCellLength = 255;
                                if (stringValue!.Length > maxSupportedCellLength)
                                {
                                    throw new XlsCellLengthException(maxSupportedCellLength);
                                }

                                excelWriter.WriteCell(rowIndex, j, stringValue);
                            }
                            progress.Report(1);
                        }
                    }
                }

                bool IsIntCastSafe(object value) => value.GetType() == typeof(int)
                    || (value.GetType() == typeof(uint) && (uint)value < int.MaxValue)
                    || value.GetType() == typeof(sbyte)
                    || value.GetType() == typeof(byte);

                bool IsDoubleCastSafe(object value) => value.GetType() == typeof(double)
                    || value.GetType() == typeof(decimal)
                    || value.GetType() == typeof(float)
                    || value.GetType() == typeof(long)
                    || value.GetType() == typeof(uint);

                excelWriter.EndWrite();
            }, cancellationToken);

    internal static Task WriteDataToJSONFile(IAsyncEnumerable<DataTable> dataChunks, string path, CancellationToken cancellationToken, IProgress<int> progress)
        => Task.Run(async () =>
            {
                using var fs = new FileStream(path, FileMode.OpenOrCreate);
                using var jsonWriter = new Engine.Utf8JsonWriterWithRunningLength(fs);

                jsonWriter.WriteStartArray();
                await foreach (var dataTable in dataChunks.WithCancellation(cancellationToken))
                {
                    foreach (DataRow row in dataTable.Rows)
                    {
                        jsonWriter.WriteStartObject();
                        for (var i = 0; i < row.ItemArray.Length; i++)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                break;
                            }

                            var columnName = dataTable.Columns[i].ColumnName;
                            jsonWriter.WritePropertyName(columnName);

                            object? value = row.ItemArray[i];
                            Engine.Helpers.WriteValue(jsonWriter, value!, false);
                            progress.Report(1);
                        }
                        jsonWriter.WriteEndObject();
                    }
                }
                jsonWriter.WriteEndArray();
            }, cancellationToken);

    private static Task WriteDataToParquetFile(IParquetEngine engine, IAsyncEnumerable<DataTable> dataChunks, string path,
        IProgress<int> progress, string engineName, CancellationToken cancellationToken)
        => Task.Run(async () =>
            {
                var customMetadata = new Dictionary<string, string>
                        {
                            {
"ParquetViewer", @"
{
    ""CreatedWith"": ""ParquetViewer"",
    ""Version"": """ + Env.AssemblyVersion.ToString() + @""",
    ""Website"": ""https://github.com/mukunku/ParquetViewer"",
    ""CreationDate"": """ + DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ") + @""",
    ""Engine"": """ + engineName + @"""
}"
                            }
                        };
                await engine.WriteDataToParquetFileAsync(dataChunks, path, progress, customMetadata, cancellationToken);
            }, cancellationToken);

    private static void HandleAllFilesSkippedException(AllFilesSkippedException ex)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Resources.Errors.NoValidParquetFilesFoundErrorMessage);
        foreach (var skippedFile in ex.SkippedFiles)
        {
            sb.AppendLine($"-{skippedFile.FileName}");
        }
        ShowError(sb.ToString());
    }

    private static void HandleSomeFilesSkippedException(SomeFilesSkippedException ex)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Resources.Errors.SomeInvalidParquetFilesFoundErrorMessage);
        foreach (var skippedFile in ex.SkippedFiles)
        {
            sb.AppendLine($"-{skippedFile.FileName}");
        }
        ShowError(sb.ToString());
    }

    private static void HandleFileReadException(FileReadException ex)
    {
        ShowError(Resources.Errors.UnexpectedFileReadErrorMessageFormat.Format(ex));
    }

    private static void HandleFileNotFoundException(FileNotFoundException ex)
    {
        ShowError(ex.Message);
    }

    private static void HandleMultipleSchemasFoundException(MultipleSchemasFoundException ex)
    {
        var sb = new StringBuilder();
        sb.Append(Resources.Errors.MultipleSchemasDetectedErrorMessage);
        sb.AppendLine(" ");

        var schemaIndex = 1;
        const int topCount = 5;
        const int maxSchemasLimit = 10; //prevent a giant textbox from appearing
        foreach (var schema in ex.Schemas)
        {
            sb.AppendLine(Resources.Errors.MultipleSchemasDetectedEntriesErrorMessageFormat.Format(schemaIndex++));
            for (var i = 0; i < topCount; i++)
            {
                if (i == schema.Count)
                    break;

                sb.AppendLine($"  {schema.ElementAt(i)}");
            }

            if (schemaIndex > maxSchemasLimit)
            {
                sb.AppendLine("...");
                break;
            }
        }
        ShowError(sb.ToString());
    }

    private static void HandleMalformedFieldException(MalformedFieldException ex)
    {
        ShowError(Resources.Errors.MalformedFieldErrorMessageFormat.Format(ex.Message));
    }

    private static void HandleDecimalOverflowException(DecimalOverflowException ex)
        => ShowError(
               (ex.HasDetailedInfo ? Resources.Errors.DecimalValueTooLargeErrorMessageFormat
                    : Resources.Errors.DecimalValueUnknownSizeTooLargeErrorMessageFormat)
               .Format(
                   ex.FieldName,
                   ex.Precision,
                   ex.Scale,
                   DecimalOverflowException.MAX_DECIMAL_PRECISION,
                   DecimalOverflowException.MAX_DECIMAL_SCALE),
               Resources.Errors.DecimalValueTooLargeErrorTitle);

    private static void ShowError(string message, string? title = null)
        => MessageBox.Show(message, title ?? Resources.Errors.GenericErrorMessage, MessageBoxButtons.OK, MessageBoxIcon.Error);
}