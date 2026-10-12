using ParquetViewer.Exceptions;
using ParquetViewer.Helpers;
using System.Data;

namespace ParquetViewer.Tests;

[TestClass]
public class PagedRowSourceTests
{
    private const string TEST_FOLDER = "Data/PARTITIONED_PARQUET_FILE_TEST"; //2000 rows spread across 4 files

    public PagedRowSourceTests()
    {
        //Set a consistent date format for all tests
        ParquetEngineSettings.DateDisplayFormat = "yyyy-MM-dd HH:mm:ss";
        ParquetEngineSettings.DateOnlyDisplayFormat = "yyyy-MM-dd";
        ParquetEngineSettings.TimeOnlyDisplayFormat = "HH:mm:ss";
    }

    private static ReadSliceDelegate CreateReadSlice(IParquetEngine engine)
        => async (offset, count, cancellationToken) => (await engine.ReadRowsAsync(engine.Fields, offset, count, null, cancellationToken))(false);

    private static async Task<List<DataTable>> ReadAllPagesAsync(PagedRowSource pageSource)
    {
        var pages = new List<DataTable>();
        for (var pageIndex = 0; ; pageIndex++)
        {
            var page = await pageSource.GetPageAsync(pageIndex, null, default);
            if (page is null)
                break;

            pages.Add(page);
        }
        return pages;
    }

    private static List<string> WithoutRowIndexColumn(IEnumerable<DataTable> tables, string rowIndexColumnName)
        => tables.SelectMany(table =>
        {
            var copy = table.Copy();
            if (copy.Columns.Contains(rowIndexColumnName))
                copy.Columns.Remove(rowIndexColumnName);
            return PagedReadTests.ToComparableRows(copy);
        }).ToList();

    /// <summary>
    /// A filter on the numeric field that matches some, but not all, rows
    /// </summary>
    private static string GetTestFilter(DataTable fullTable) => $"[{fullTable.Columns[3].ColumnName}] > 300";

    [TestMethod]
    public async Task UNFILTERED_PAGES_MATCH_FULL_READ()
    {
        using var engine = await Engine.ParquetNET.ParquetEngine.OpenFileOrFolderAsync(TEST_FOLDER);
        var fullTable = (await engine.ReadRowsAsync(engine.Fields, 0, int.MaxValue))(false);
        var rowIndexColumnName = PagedRowSource.GetRowIndexColumnName(engine.Fields);

        var pageSource = new UnfilteredPageSource(CreateReadSlice(engine), engine.RecordCount, 300, rowIndexColumnName);
        var pages = await ReadAllPagesAsync(pageSource);

        Assert.HasCount(pageSource.KnownPageCount, pages);
        CollectionAssert.AreEqual(PagedReadTests.ToComparableRows(fullTable), WithoutRowIndexColumn(pages, rowIndexColumnName));

        //The row index column holds each row's position in the file
        var rowIndexes = pages.SelectMany(page => page.Rows.Cast<DataRow>().Select(row => (long)row[rowIndexColumnName])).ToList();
        CollectionAssert.AreEqual(Enumerable.Range(0, (int)engine.RecordCount).Select(i => (long)i).ToList(), rowIndexes);

        Assert.IsNull(await pageSource.GetPageAsync(-1, null, default));
    }

    [TestMethod]
    [DataRow(7, 13)]
    [DataRow(100, 1000)]
    [DataRow(1000, 10)]
    public async Task FILTERED_PAGES_MATCH_FULL_FILTER(int pageSize, int scanChunkSize)
    {
        using var engine = await Engine.ParquetNET.ParquetEngine.OpenFileOrFolderAsync(TEST_FOLDER);
        var fullTable = (await engine.ReadRowsAsync(engine.Fields, 0, int.MaxValue))(false);
        var rowFilter = GetTestFilter(fullTable);
        fullTable.DefaultView.RowFilter = rowFilter;
        var expectedRows = PagedReadTests.ToComparableRows(fullTable.DefaultView.ToTable());
        Assert.IsTrue(expectedRows.Count > 0 && expectedRows.Count < engine.RecordCount, "The test filter should match some but not all rows");

        var rowIndexColumnName = PagedRowSource.GetRowIndexColumnName(engine.Fields);
        var pageSource = new FilteredPageSource(CreateReadSlice(engine), engine.RecordCount, pageSize, rowIndexColumnName, rowFilter, scanChunkSize);
        Assert.IsFalse(pageSource.IsComplete);

        var pages = await ReadAllPagesAsync(pageSource);
        CollectionAssert.AreEqual(expectedRows, WithoutRowIndexColumn(pages, rowIndexColumnName));
        Assert.IsTrue(pageSource.IsComplete);
        Assert.AreEqual(expectedRows.Count, pageSource.KnownRowCount);
        Assert.AreEqual(pages.Count, pageSource.KnownPageCount);
        Assert.AreEqual(1, pageSource.ScannedFraction);
        Assert.IsTrue(pages.Take(pages.Count - 1).All(page => page.Rows.Count == pageSize), "Only the last page can be partial");

        //Going back to a page rebuilds it from its checkpoint
        for (var pageIndex = pages.Count - 1; pageIndex >= 0; pageIndex--)
        {
            var rebuiltPage = await pageSource.GetPageAsync(pageIndex, null, default);
            Assert.IsNotNull(rebuiltPage);
            CollectionAssert.AreEqual(PagedReadTests.ToComparableRows(pages[pageIndex]), PagedReadTests.ToComparableRows(rebuiltPage));
        }

        //The row index column holds each match's position in the file
        var expectedRowIndexes = Enumerable.Range(0, fullTable.Rows.Count)
            .Where(i => (double)fullTable.Rows[i][3] > 300)
            .Select(i => (long)i)
            .ToList();
        var rowIndexes = pages.SelectMany(page => page.Rows.Cast<DataRow>().Select(row => (long)row[rowIndexColumnName])).ToList();
        CollectionAssert.AreEqual(expectedRowIndexes, rowIndexes);
    }

    [TestMethod]
    public async Task FILTERED_SOURCE_CAN_JUMP_AHEAD()
    {
        using var engine = await Engine.ParquetNET.ParquetEngine.OpenFileOrFolderAsync(TEST_FOLDER);
        var fullTable = (await engine.ReadRowsAsync(engine.Fields, 0, int.MaxValue))(false);
        var rowFilter = GetTestFilter(fullTable);
        var rowIndexColumnName = PagedRowSource.GetRowIndexColumnName(engine.Fields);

        var sequentialSource = new FilteredPageSource(CreateReadSlice(engine), engine.RecordCount, 10, rowIndexColumnName, rowFilter, 50);
        var expectedPages = await ReadAllPagesAsync(sequentialSource);
        Assert.IsGreaterThan(5, expectedPages.Count);

        //Asking for a page far ahead scans up to it
        var jumpingSource = new FilteredPageSource(CreateReadSlice(engine), engine.RecordCount, 10, rowIndexColumnName, rowFilter, 50);
        var targetPage = await jumpingSource.GetPageAsync(5, null, default);
        Assert.IsNotNull(targetPage);
        Assert.IsFalse(jumpingSource.IsComplete);
        Assert.AreEqual(6, jumpingSource.KnownPageCount);
        CollectionAssert.AreEqual(PagedReadTests.ToComparableRows(expectedPages[5]), PagedReadTests.ToComparableRows(targetPage));

        //Pages beyond the last match don't exist
        Assert.IsNull(await jumpingSource.GetPageAsync(expectedPages.Count + 10, null, default));
        Assert.IsTrue(jumpingSource.IsComplete);
        Assert.AreEqual(expectedPages.Count, jumpingSource.KnownPageCount);
    }

    [TestMethod]
    public async Task FILTER_WITHOUT_MATCHES_RETURNS_NO_PAGES()
    {
        using var engine = await Engine.ParquetNET.ParquetEngine.OpenFileOrFolderAsync(TEST_FOLDER);
        var fullTable = (await engine.ReadRowsAsync(engine.Fields, 0, 1))(false);
        var rowIndexColumnName = PagedRowSource.GetRowIndexColumnName(engine.Fields);

        var pageSource = new FilteredPageSource(CreateReadSlice(engine), engine.RecordCount, 100, rowIndexColumnName,
            $"[{fullTable.Columns[3].ColumnName}] < -1000000", 300);
        Assert.IsNull(await pageSource.GetPageAsync(0, null, default));
        Assert.IsTrue(pageSource.IsComplete);
        Assert.AreEqual(0, pageSource.KnownRowCount);

        //Exports still get one empty chunk so the schema can be written
        var chunks = new List<DataTable>();
        await foreach (var chunk in pageSource.ReadAllChunksAsync(100, default))
            chunks.Add(chunk);

        Assert.HasCount(1, chunks);
        Assert.AreEqual(0, chunks[0].Rows.Count);
        Assert.AreEqual(fullTable.Columns.Count, chunks[0].Columns.Count);
    }

    [TestMethod]
    public async Task INVALID_QUERIES_THROW_INVALID_QUERY_EXCEPTION()
    {
        using var engine = await Engine.ParquetNET.ParquetEngine.OpenFileOrFolderAsync(TEST_FOLDER);
        var fullTable = (await engine.ReadRowsAsync(engine.Fields, 0, 10))(false);
        var rowIndexColumnName = PagedRowSource.GetRowIndexColumnName(engine.Fields);

        Assert.ThrowsExactly<InvalidQueryException>(() => FilteredPageSource.Validate("[this column doesn't exist] = 1", fullTable));
        Assert.ThrowsExactly<InvalidQueryException>(() => FilteredPageSource.Validate("this isn't a valid query", fullTable));
        FilteredPageSource.Validate(GetTestFilter(fullTable), fullTable);

        //Some problems only show up once rows are evaluated
        var pageSource = new FilteredPageSource(CreateReadSlice(engine), engine.RecordCount, 100, rowIndexColumnName,
            $"[{fullTable.Columns[3].ColumnName}] = 'not a number'", 300);
        await Assert.ThrowsExactlyAsync<InvalidQueryException>(() => pageSource.GetPageAsync(0, null, default));
    }

    [TestMethod]
    public async Task CANCELLED_SCAN_LEAVES_SOURCE_USABLE()
    {
        using var engine = await Engine.ParquetNET.ParquetEngine.OpenFileOrFolderAsync(TEST_FOLDER);
        var fullTable = (await engine.ReadRowsAsync(engine.Fields, 0, int.MaxValue))(false);
        var rowFilter = GetTestFilter(fullTable);
        var rowIndexColumnName = PagedRowSource.GetRowIndexColumnName(engine.Fields);

        using var cancellation = new CancellationTokenSource();
        var readCount = 0;
        ReadSliceDelegate cancellingReadSlice = async (offset, count, cancellationToken) =>
        {
            //Cancel part way through the scan
            if (++readCount == 3)
                cancellation.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            return (await engine.ReadRowsAsync(engine.Fields, offset, count, null, cancellationToken))(false);
        };

        var pageSource = new FilteredPageSource(cancellingReadSlice, engine.RecordCount, 1000, rowIndexColumnName, rowFilter, 100);
        await Assert.ThrowsAsync<OperationCanceledException>(() => pageSource.GetPageAsync(0, null, cancellation.Token));
        Assert.AreEqual(0, pageSource.KnownPageCount);

        var pages = await ReadAllPagesAsync(pageSource);
        fullTable.DefaultView.RowFilter = rowFilter;
        CollectionAssert.AreEqual(PagedReadTests.ToComparableRows(fullTable.DefaultView.ToTable()), WithoutRowIndexColumn(pages, rowIndexColumnName));
    }

    [TestMethod]
    public async Task EXPORT_CHUNKS_MATCH_FULL_READ()
    {
        using var engine = await Engine.ParquetNET.ParquetEngine.OpenFileOrFolderAsync(TEST_FOLDER);
        var fullTable = (await engine.ReadRowsAsync(engine.Fields, 0, int.MaxValue))(false);
        var rowIndexColumnName = PagedRowSource.GetRowIndexColumnName(engine.Fields);

        var unfilteredSource = new UnfilteredPageSource(CreateReadSlice(engine), engine.RecordCount, 100, rowIndexColumnName);
        var unfilteredChunks = await unfilteredSource.ReadAllChunksAsync(333, default).ToListAsync();
        Assert.IsTrue(unfilteredChunks.All(chunk => !chunk.Columns.Contains(rowIndexColumnName)));
        Assert.IsTrue(unfilteredChunks.All(chunk => chunk.Rows.Count <= 333));
        CollectionAssert.AreEqual(PagedReadTests.ToComparableRows(fullTable), unfilteredChunks.SelectMany(PagedReadTests.ToComparableRows).ToList());

        var rowFilter = GetTestFilter(fullTable);
        var filteredSource = new FilteredPageSource(CreateReadSlice(engine), engine.RecordCount, 100, rowIndexColumnName, rowFilter, 70);
        var filteredChunks = await filteredSource.ReadAllChunksAsync(50, default).ToListAsync();
        Assert.IsTrue(filteredChunks.All(chunk => !chunk.Columns.Contains(rowIndexColumnName)));
        Assert.IsTrue(filteredChunks.All(chunk => chunk.Rows.Count <= 50));
        fullTable.DefaultView.RowFilter = rowFilter;
        CollectionAssert.AreEqual(PagedReadTests.ToComparableRows(fullTable.DefaultView.ToTable()), filteredChunks.SelectMany(PagedReadTests.ToComparableRows).ToList());
    }

    [TestMethod]
    [DataRow(FileType.CSV)]
    [DataRow(FileType.JSON)]
    [DataRow(FileType.XLS)]
    public async Task CHUNKED_EXPORT_MATCHES_SINGLE_TABLE_EXPORT(FileType fileType)
    {
        using var engine = await Engine.ParquetNET.ParquetEngine.OpenFileOrFolderAsync(TEST_FOLDER);
        var fullTable = (await engine.ReadRowsAsync(engine.Fields, 0, int.MaxValue))(false);
        var rowIndexColumnName = PagedRowSource.GetRowIndexColumnName(engine.Fields);
        var pageSource = new UnfilteredPageSource(CreateReadSlice(engine), engine.RecordCount, 100, rowIndexColumnName);

        Func<IAsyncEnumerable<DataTable>, string, Task> writeFile = fileType switch
        {
            FileType.CSV => (dataChunks, path) => MainForm.WriteDataToCSVFile(dataChunks, path, CancellationToken.None, new Progress<int>()),
            FileType.JSON => (dataChunks, path) => MainForm.WriteDataToJSONFile(dataChunks, path, CancellationToken.None, new Progress<int>()),
            FileType.XLS => (dataChunks, path) => MainForm.WriteDataToExcel93File(dataChunks, path, CancellationToken.None, new Progress<int>()),
            _ => throw new ArgumentOutOfRangeException(nameof(fileType))
        };
        var extension = fileType.GetExtension();

        var singleTablePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}{extension}");
        var chunkedPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}{extension}");
        try
        {
            await writeFile(new[] { fullTable }.ToAsyncEnumerable(), singleTablePath);
            await writeFile(pageSource.ReadAllChunksAsync(3, default), chunkedPath);

            CollectionAssert.AreEqual(File.ReadAllBytes(singleTablePath), File.ReadAllBytes(chunkedPath));
        }
        finally
        {
            File.Delete(singleTablePath);
            File.Delete(chunkedPath);
        }
    }

    [TestMethod]
    public async Task CHUNKED_XLSX_EXPORT_CONTAINS_ALL_ROWS()
    {
        using var engine = await Engine.ParquetNET.ParquetEngine.OpenFileOrFolderAsync(TEST_FOLDER);
        var rowIndexColumnName = PagedRowSource.GetRowIndexColumnName(engine.Fields);
        var pageSource = new UnfilteredPageSource(CreateReadSlice(engine), engine.RecordCount, 100, rowIndexColumnName);
        var fullTable = (await engine.ReadRowsAsync(engine.Fields, 0, int.MaxValue))(false);


        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.xlsx");
        try
        {
            await MainForm.WriteDataToExcel2007File(pageSource.ReadAllChunksAsync(333, default), path, "Sheet1", CancellationToken.None, new Progress<int>());

            using var stream = File.OpenRead(path);
            var rows = MiniExcelLibs.MiniExcel.Query(stream, useHeaderRow: true, excelType: MiniExcelLibs.ExcelType.XLSX)
                .Cast<IDictionary<string, object>>()
                .ToList();

            Assert.HasCount((int)engine.RecordCount, rows);
            CollectionAssert.AreEqual(fullTable.Columns.Cast<DataColumn>().Select(column => column.ColumnName).ToList(), rows[0].Keys.ToList());
            Assert.AreEqual(fullTable.Rows[0][0].ToString(), rows[0][fullTable.Columns[0].ColumnName]?.ToString());
            Assert.AreEqual(fullTable.Rows[1999][8].ToString(), rows[1999][fullTable.Columns[8].ColumnName]?.ToString());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task ROW_LIMIT_EXCEPTION_SURFACES_FROM_XLSX_WRITER()
    {
        using var engine = await Engine.ParquetNET.ParquetEngine.OpenFileOrFolderAsync(TEST_FOLDER);
        var fullTable = (await engine.ReadRowsAsync(engine.Fields, 0, 10))(false);


        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.xlsx");
        try
        {
            var exception = await Assert.ThrowsAsync<Exception>(()
                => MainForm.WriteDataToExcel2007File(ThrowAfterFirstChunk(fullTable), path, "Sheet1", CancellationToken.None, new Progress<int>()));

            //MiniExcel may wrap the exception. MainForm looks through inner exceptions for it.
            var innerException = exception;
            while (innerException is not null and not ExportRowLimitExceededException)
                innerException = innerException.InnerException;
            Assert.IsInstanceOfType<ExportRowLimitExceededException>(innerException);
        }
        finally
        {
            File.Delete(path);
        }

        static async IAsyncEnumerable<DataTable> ThrowAfterFirstChunk(DataTable dataTable)
        {
            await Task.CompletedTask;
            yield return dataTable;
            throw new ExportRowLimitExceededException(FileType.XLSX, 2_000_000, true);
        }
    }

    [TestMethod]
    public async Task CHUNKED_PARQUET_EXPORT_MATCHES_SOURCE()
    {
        using var engine = await Engine.ParquetNET.ParquetEngine.OpenFileOrFolderAsync(TEST_FOLDER);
        var fullTable = (await engine.ReadRowsAsync(engine.Fields, 0, int.MaxValue))(false);
        var rowIndexColumnName = PagedRowSource.GetRowIndexColumnName(engine.Fields);
        var pageSource = new UnfilteredPageSource(CreateReadSlice(engine), engine.RecordCount, 100, rowIndexColumnName);

        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.parquet");
        try
        {
            await engine.WriteDataToParquetFileAsync(pageSource.ReadAllChunksAsync(700, default), path, new Progress<int>(), null, default);

            using var exportedEngine = await Engine.ParquetNET.ParquetEngine.OpenFileOrFolderAsync(path);
            Assert.AreEqual(engine.RecordCount, exportedEngine.RecordCount);
            var exportedTable = (await exportedEngine.ReadRowsAsync(exportedEngine.Fields, 0, int.MaxValue))(false);
            CollectionAssert.AreEqual(PagedReadTests.ToComparableRows(fullTable), PagedReadTests.ToComparableRows(exportedTable));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void ROW_LIMITS_MATCH_EXCEL_FORMATS()
    {
        Assert.AreEqual(65_535, ExportRowLimitExceededException.GetMaxDataRowCount(FileType.XLS));
        Assert.AreEqual(1_048_575, ExportRowLimitExceededException.GetMaxDataRowCount(FileType.XLSX));
        Assert.IsNull(ExportRowLimitExceededException.GetMaxDataRowCount(FileType.CSV));

        ExportRowLimitExceededException.ThrowIfExceeded(FileType.XLS, 65_535, false);
        ExportRowLimitExceededException.ThrowIfExceeded(FileType.CSV, long.MaxValue, false);
        var ex = Assert.ThrowsExactly<ExportRowLimitExceededException>(() => ExportRowLimitExceededException.ThrowIfExceeded(FileType.XLS, 65_536, true));
        Assert.IsTrue(ex.IsLowerBound);
        Assert.AreEqual(65_536, ex.RowCount);
    }

    [TestMethod]
    public void ROW_INDEX_COLUMN_NAME_DOES_NOT_COLLIDE()
    {
        var name = PagedRowSource.GetRowIndexColumnName(["a", "b"]);
        Assert.AreEqual("__ParquetViewer_RowIndex__", name);
        Assert.AreNotEqual(name, PagedRowSource.GetRowIndexColumnName([name.ToUpperInvariant()]), "DataTable column names are case insensitive");
    }
}
