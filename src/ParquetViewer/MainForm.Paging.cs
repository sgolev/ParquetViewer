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
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ParquetViewer;

/// <summary>
/// Files that are too large to load at once are shown one page of rows at a time.
/// The user moves between pages with the pager in the status bar.
/// </summary>
public partial class MainForm
{
    private const int PAGE_SIZE = 1_000;

    //Results up to this size are loaded in full (a single page), which also allows sorting. See IsUnderFullLoadLimit()
    private const int FULL_LOAD_ROW_LIMIT = 1_000_000;
    private const long FULL_LOAD_CELL_LIMIT = 5_000_000;

    private const int EXPORT_CHUNK_SIZE = 100_000;

    /// <summary>
    /// Parquet readers and DuckDB connections can't be used concurrently. Every engine read goes through this lock.
    /// </summary>
    private readonly SemaphoreSlim _engineLock = new(1, 1);

    /// <summary>
    /// Incremented every time the rows in the grid are replaced (new file, new fields, query, page change, etc.).
    /// Pending loads check this before touching the grid so stale results get dropped.
    /// </summary>
    private int _viewGeneration;

    /// <summary>
    /// Every read is cancelled through this before the grid's rows are replaced or the engine is disposed
    /// </summary>
    private CancellationTokenSource _loadCts = new();

    private PagedRowSource? _pageSource;
    private List<string>? _loadedFields; //The fields used to load the rows in the grid
    private bool _isFullLoad; //True when the grid holds the entire result. Then there's only one page and sorting is allowed.
    private int _currentPageIndex;

    private bool IsWholeFileQueryActive => _pageSource is FilteredPageSource;

    private string RowIndexSort => $"[{_pageSource?.RowIndexColumnName}] ASC";

    /// <summary>
    /// Results that fit in memory comfortably are loaded in full. A full load holds roughly 50 bytes per cell at its peak
    /// (the engine's intermediate rows plus the DataTable), so 5M cells is around 250MB.
    /// </summary>
    private static bool IsUnderFullLoadLimit(long rowCount, int columnCount)
        => rowCount <= PAGE_SIZE
        || (rowCount <= FULL_LOAD_ROW_LIMIT && rowCount * Math.Max(columnCount, 1) <= FULL_LOAD_CELL_LIMIT);

    private IEnumerable<DataColumn> GetUserColumns()
        => MainDataSource?.Columns.AsEnumerable().Where(column => column.ColumnName != _pageSource?.RowIndexColumnName) ?? [];

    private List<string> GetUserColumnNames() => GetUserColumns().Select(column => column.ColumnName).ToList();

    /// <summary>
    /// Cancels pending loads and invalidates their results
    /// </summary>
    private int StartNewViewGeneration()
    {
        _viewGeneration++;
        _loadCts.Cancel();
        _loadCts.Dispose();
        _loadCts = new CancellationTokenSource();
        return _viewGeneration;
    }

    /// <summary>
    /// Waits for any in-flight engine read to finish and then disposes the open engine and loaded rows
    /// </summary>
    private async Task CloseCurrentFileAsync()
    {
        var engine = _openParquetEngine;
        _openParquetEngine = null;
        ResetLoadedData();

        if (engine is not null)
            await DisposeEngineAsync(engine);

        OpenFileOrFolderPath = null;
    }

    /// <remarks>Cancel pending reads (<see cref="ResetLoadedData"/>) first so nothing is waiting to use the engine</remarks>
    private async Task DisposeEngineAsync(IParquetEngine engine)
    {
        await _engineLock.WaitAsync();
        try
        {
            engine.DisposeSafely();
        }
        finally
        {
            _engineLock.Release();
        }
    }

    private void ResetLoadedData()
    {
        StartNewViewGeneration();
        mainGridView.DisposeAudioCells();
        var oldDataSource = MainDataSource;
        MainDataSource = null;
        oldDataSource?.Dispose();
        _pageSource = null;
        _loadedFields = null;
        _isFullLoad = false;
        _currentPageIndex = 0;
    }

    /// <summary>
    /// Reads rows from the engine while holding the engine lock
    /// </summary>
    private async Task<(DataTable Rows, TimeSpan LoadTime, TimeSpan IndexTime)> ReadSliceAsync(IParquetEngine engine, List<string> fields,
        long offset, int count, LoadingIcon? loadingIcon, CancellationToken cancellationToken)
    {
        await _engineLock.WaitAsync(cancellationToken);
        try
        {
            var stopwatch = Stopwatch.StartNew();
            var intermediateResult = await Task.Run(()
                => engine.ReadRowsAsync(fields, offset, count, loadingIcon, cancellationToken), cancellationToken);

            var loadTime = stopwatch.Elapsed;
            bool showIndexingProgress = false;
            if (loadingIcon is not null && loadTime > TimeSpan.FromSeconds(4))
            {
                //Don't bother showing the indexing step if the data load was really fast because we know
                //indexing will be instantaneous. It looks better this way in my opinion.
                loadingIcon.Reset(Resources.Strings.IndexingDataLabelText);
                showIndexingProgress = true;
            }

            var rows = await Task.Run(() => intermediateResult.Invoke(showIndexingProgress), cancellationToken);
            return (rows, loadTime, stopwatch.Elapsed - loadTime);
        }
        finally
        {
            _engineLock.Release();
        }
    }

    private ReadSliceDelegate CreateReadSlice(IParquetEngine engine, List<string> fields)
        => async (offset, count, cancellationToken) => (await ReadSliceAsync(engine, fields, offset, count, null, cancellationToken)).Rows;

    /// <summary>
    /// Binds a freshly loaded table to the grid
    /// </summary>
    private void ShowNewDataSource(DataTable firstPage, PagedRowSource pageSource, List<string> fields, bool isFullLoad)
    {
        mainGridView.DisposeAudioCells();
        mainGridView.RowIndexColumnName = pageSource.RowIndexColumnName;
        mainGridView.AllowColumnSorting = isFullLoad;
        mainGridView.FitRowHeadersToRowCount(pageSource.RecordCount);

        _pageSource = pageSource;
        _loadedFields = fields;
        _isFullLoad = isFullLoad;
        _currentPageIndex = 0;

        firstPage.DefaultView.Sort = RowIndexSort;
        var oldDataSource = MainDataSource;
        MainDataSource = firstPage;
        oldDataSource?.Dispose();

        UpdatePager();
    }

    /// <summary>
    /// Replaces the rows in the grid with a page. The table (and so the grid's columns) is kept as is.
    /// </summary>
    private void ReplacePage(PagedRowSource pageSource, DataTable? page, int pageIndex)
    {
        var dataTable = MainDataSource!;

        mainGridView.DisposeAudioCells();
        dataTable.DefaultView.RowFilter = null;
        _pageSource = pageSource;
        dataTable.DefaultView.Sort = RowIndexSort; //Undo any sorting done by the user
        dataTable.Clear();

        _currentPageIndex = pageIndex;
        if (page is not null)
        {
            dataTable.BeginLoadData();
            foreach (DataRow row in page.Rows)
            {
                dataTable.LoadDataRow(row.ItemArray, true);
            }
            dataTable.EndLoadData();
            page.Dispose();
        }

        //A query whose matches all fit on one page is the entire result, so it can be sorted
        _isFullLoad = pageSource is FilteredPageSource { IsComplete: true, KnownPageCount: <= 1 };
        mainGridView.AllowColumnSorting = _isFullLoad;

        if (mainGridView.RowCount > 0)
        {
            try
            {
                mainGridView.FirstDisplayedScrollingRowIndex = 0;
            }
            catch (InvalidOperationException) { /*Grid isn't ready to scroll (e.g. it has no height)*/ }
        }

        UpdatePager();
    }

    /// <summary>
    /// Shows the given page of the open file, or of the active query's matches. Pages past the end show the last page instead.
    /// </summary>
    /// <returns>True if the page was shown, false if the load was cancelled or superseded</returns>
    private Task<bool> GoToPageAsync(int pageIndex)
    {
        if (_isFullLoad || _pageSource is null || MainDataSource is null)
            return Task.FromResult(false);

        pageIndex = Math.Max(pageIndex, 0);
        if (_pageSource.IsComplete)
            pageIndex = Math.Min(pageIndex, Math.Max(_pageSource.KnownPageCount - 1, 0));

        return ShowPageAsync(_pageSource, pageIndex);
    }

    /// <summary>
    /// Loads a page of <paramref name="pageSource"/> and shows it in the grid
    /// </summary>
    /// <returns>True if the page was shown, false if the load was cancelled or superseded</returns>
    private async Task<bool> ShowPageAsync(PagedRowSource pageSource, int pageIndex)
    {
        var viewGeneration = StartNewViewGeneration();
        var needsScan = pageSource is FilteredPageSource && pageIndex >= pageSource.KnownPageCount;
        var page = await LoadPageWithLoadingIconAsync(pageSource, pageIndex, viewGeneration,
            needsScan ? Resources.Strings.SearchingForMatchesLabelText : Resources.Strings.LoadingDataLabelText);

        if (page is null && viewGeneration == _viewGeneration && pageIndex >= pageSource.KnownPageCount && pageSource.KnownPageCount > 0)
        {
            //The query has fewer pages than the user asked for. Go to the last one instead.
            pageIndex = pageSource.KnownPageCount - 1;
            page = await LoadPageWithLoadingIconAsync(pageSource, pageIndex, viewGeneration, Resources.Strings.LoadingDataLabelText);
        }

        if (viewGeneration != _viewGeneration)
        {
            page?.Dispose();
            UpdatePager(); //Show the current page number again
            return false;
        }

        ReplacePage(pageSource, page, page is null ? 0 : pageIndex);
        return true;
    }

    /// <returns>The page, or null if it doesn't exist or the load was cancelled (in which case the view generation will have changed)</returns>
    private async Task<DataTable?> LoadPageWithLoadingIconAsync(PagedRowSource pageSource, int pageIndex, int viewGeneration, string loadingMessage)
    {
        using var loadingIcon = ShowLoadingIcon(loadingMessage, pageSource is FilteredPageSource ? 100 : 0);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(loadingIcon.CancellationToken, _loadCts.Token);
        try
        {
            var scanProgress = pageSource is FilteredPageSource ? new LoadingIconPercentageProgress(loadingIcon) : null;
            return await pageSource.GetPageAsync(pageIndex, scanProgress, cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            if (viewGeneration == _viewGeneration)
                StartNewViewGeneration(); //Make sure the caller knows not to use the result
            return null;
        }
    }

    private void ScrollToAndSelectRow(int position)
    {
        if (position < 0 || position >= mainGridView.RowCount)
            return;

        try
        {
            mainGridView.ClearSelection();
            mainGridView.FirstDisplayedScrollingRowIndex = position;
            var firstVisibleColumn = mainGridView.Columns.GetFirstColumn(DataGridViewElementStates.Visible);
            if (firstVisibleColumn is not null)
                mainGridView.CurrentCell = mainGridView[firstVisibleColumn.Index, position];
            mainGridView.Rows[position].Selected = true;
            mainGridView.Focus();
        }
        catch (InvalidOperationException) { /*Grid isn't ready to scroll (e.g. it has no height)*/ }
    }

    /// <returns>The row's 0-based position in the file</returns>
    private long? GetRowIndexValue(int position)
    {
        var dataView = MainDataSource?.DefaultView;
        if (dataView is null || _pageSource is null || position < 0 || position >= dataView.Count)
            return null;

        return dataView[position][_pageSource.RowIndexColumnName] as long?;
    }

    /// <summary>
    /// Runs a query against the entire file. The matches are shown a page at a time.
    /// </summary>
    private async Task RunWholeFileQueryAsync(string rowFilter)
    {
        if (MainDataSource is null || _pageSource is null || _openParquetEngine is null || _loadedFields is null)
            return;

        FilteredPageSource.Validate(rowFilter, MainDataSource);

        var stopwatch = Stopwatch.StartNew();
        var pageSource = new FilteredPageSource(CreateReadSlice(_openParquetEngine, _loadedFields),
            _pageSource.RecordCount, PAGE_SIZE, _pageSource.RowIndexColumnName, rowFilter);
        if (!await ShowPageAsync(pageSource, 0))
            return; //Cancelled or superseded. The rows that were already in the grid stay as they were.

        var queryEvent = new Analytics.ExecuteQueryEvent
        {
            RecordCountTotal = (int)Math.Min(pageSource.RecordCount, int.MaxValue),
            ColumnCount = GetUserColumns().Count(),
            IsValid = true,
            RecordCountFiltered = (int)Math.Min(pageSource.KnownRowCount, int.MaxValue),
            RunTimeMS = stopwatch.ElapsedMilliseconds
        };
        _ = queryEvent.Record(); //Fire and forget
    }

    /// <summary>
    /// Goes back to showing every row of the file, starting from the first page
    /// </summary>
    private async Task ClearWholeFileQueryAsync()
    {
        if (_pageSource is null || _openParquetEngine is null || _loadedFields is null)
            return;

        await ShowPageAsync(new UnfilteredPageSource(CreateReadSlice(_openParquetEngine, _loadedFields),
            _pageSource.RecordCount, PAGE_SIZE, _pageSource.RowIndexColumnName), 0);
    }

    /// <summary>
    /// Shows a row of the file, or the n-th match if a query is active
    /// </summary>
    private async Task JumpToRowAsync(long targetRowIndex)
    {
        if (MainDataSource is null || _pageSource is null)
            return;

        var dataView = MainDataSource.DefaultView;
        var isQueryActive = IsWholeFileQueryActive || !string.IsNullOrEmpty(dataView.RowFilter);
        if (_isFullLoad)
        {
            int position;
            if (isQueryActive)
                position = (int)Math.Min(targetRowIndex, dataView.Count - 1);
            else if (dataView.Sort == RowIndexSort)
                position = dataView.Find(targetRowIndex);
            else
                position = Enumerable.Range(0, dataView.Count).FirstOrDefault(i => GetRowIndexValue(i) == targetRowIndex, -1);

            ScrollToAndSelectRow(position);
            return;
        }

        //The target position within the source: a row of the file or the n-th match of the query
        var pageIndex = (int)(targetRowIndex / PAGE_SIZE);
        if (pageIndex != _currentPageIndex && !await GoToPageAsync(pageIndex))
            return;

        var pageStart = (long)_currentPageIndex * PAGE_SIZE;
        ScrollToAndSelectRow((int)Math.Clamp(targetRowIndex - pageStart, 0, MainDataSource.DefaultView.Count - 1));
    }

    private void UpdatePager()
    {
        if (MainDataSource is null || _pageSource is null)
        {
            pageNumberTextBox.Text = string.Empty;
            pageCountStatusBarLabel.Text = Resources.Strings.PageCountFormat.Format(0, 0);
            pageCountStatusBarLabel.ToolTipText = null;
            pageNumberTextBox.Enabled = previousPageButton.Enabled = nextPageButton.Enabled = false;
            return;
        }

        int pageCount;
        long rowCount;
        bool isExactCount = true;
        if (_isFullLoad)
        {
            pageCount = 1;
            rowCount = MainDataSource.DefaultView.Count;
        }
        else
        {
            pageCount = Math.Max(_pageSource.KnownPageCount, 1);
            rowCount = _pageSource.KnownRowCount;
            isExactCount = _pageSource.IsComplete;
        }

        pageNumberTextBox.Text = (_currentPageIndex + 1).ToString();
        pageCountStatusBarLabel.Text = (isExactCount ? Resources.Strings.PageCountFormat : Resources.Strings.PageCountIncompleteFormat)
            .Format(pageCount, rowCount);
        pageCountStatusBarLabel.ToolTipText = _isFullLoad ? null : Resources.Strings.SortingUnavailableToolTip;

        var hasMultiplePages = !_isFullLoad && (!isExactCount || pageCount > 1);
        pageNumberTextBox.Enabled = hasMultiplePages;
        previousPageButton.Enabled = hasMultiplePages && _currentPageIndex > 0;
        nextPageButton.Enabled = hasMultiplePages && (!isExactCount || _currentPageIndex < pageCount - 1);
    }

    private async void PreviousPageButton_Click(object? sender, EventArgs e) => await GoToPageFromPagerAsync(_currentPageIndex - 1);

    private async void NextPageButton_Click(object? sender, EventArgs e) => await GoToPageFromPagerAsync(_currentPageIndex + 1);

    private void PageNumberTextBox_KeyPress(object? sender, KeyPressEventArgs e)
    {
        if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
        {
            e.Handled = true;
        }
    }

    private async void PageNumberTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.Handled = e.SuppressKeyPress = true; //Avoid the "ding"
            if (int.TryParse(pageNumberTextBox.Text, out var pageNumber) && pageNumber > 0)
            {
                await GoToPageFromPagerAsync(pageNumber - 1);
            }
            else
            {
                UpdatePager(); //Show the current page number again
            }
        }
        else if (e.KeyCode == Keys.Escape)
        {
            e.Handled = e.SuppressKeyPress = true;
            UpdatePager();
            mainGridView.Focus();
        }
    }

    private void PageNumberTextBox_Leave(object? sender, EventArgs e) => UpdatePager(); //Discard anything that wasn't submitted

    private async Task GoToPageFromPagerAsync(int pageIndex)
    {
        try
        {
            if (await GoToPageAsync(pageIndex))
                mainGridView.Focus();
        }
        catch (Exception ex)
        {
            await HandleReadExceptionAsync(ex, (_pageSource as FilteredPageSource)?.RowFilter, pageIndex);
        }
    }

    /// <summary>
    /// Shows a friendly message for the problems we know about. Anything else is rethrown unless the file can be reloaded with DuckDB.
    /// </summary>
    /// <param name="rowFilter">The query to rerun if the file gets reloaded with DuckDB</param>
    /// <param name="pageIndex">The page to show if the file gets reloaded with DuckDB</param>
    private async Task HandleReadExceptionAsync(Exception ex, string? rowFilter, int pageIndex)
    {
        if (ex is InvalidQueryException invalidQueryException)
            ShowInvalidQueryError(invalidQueryException);
        else if (!TryHandleLoadException(ex) && !await TryReloadWithDuckDBAsync(ex, rowFilter, pageIndex))
            ExceptionDispatchInfo.Capture(ex).Throw();
    }

    /// <summary>
    /// The self-contained release also ships the DuckDB engine. If Parquet.NET fails to read the file, we switch to DuckDB
    /// and load the same rows again. Parquet.NET can fail on any page, not just the first one.
    /// </summary>
    /// <returns>True if the file was reloaded with DuckDB</returns>
    /// <exception cref="RowsReadException">DuckDB couldn't read the file either</exception>
    private async Task<bool> TryReloadWithDuckDBAsync(Exception parquetNetException, string? rowFilter, int pageIndex)
    {
#if RELEASE_SELFCONTAINED
        if (_openParquetEngine is not Engine.ParquetNET.ParquetEngine parquetNetEngine || OpenFileOrFolderPath is null)
            return false;

        try
        {
            var duckDbEngine = await Engine.DuckDB.ParquetEngine.OpenFileOrFolderAsync(OpenFileOrFolderPath);
            ResetLoadedData(); //The DuckDB engine returns different value types so start over with a fresh table
            _openParquetEngine = duckDbEngine;
            await DisposeEngineAsync(parquetNetEngine);

            await LoadFileToGridviewImpl(duckDbEngine);
            if (rowFilter is not null)
                await RunWholeFileQueryAsync(rowFilter);
            if (pageIndex > 0)
                await GoToPageAsync(pageIndex);

            return true;
        }
        catch (Exception duckDbException)
        {
            //If DuckDB fails too, bail
            throw new RowsReadException(parquetNetException, duckDbException);
        }
#else
        return await Task.FromResult(false);
#endif
    }

    /// <summary>
    /// Shows a friendly message for the exceptions we know about
    /// </summary>
    /// <returns>True if the exception was handled</returns>
    private static bool TryHandleLoadException(Exception ex)
    {
        switch (ex)
        {
            case AllFilesSkippedException afse:
                HandleAllFilesSkippedException(afse);
                return true;
            case SomeFilesSkippedException sfse:
                HandleSomeFilesSkippedException(sfse);
                return true;
            case FileReadException fre:
                HandleFileReadException(fre);
                return true;
            case MultipleSchemasFoundException msfe:
                HandleMultipleSchemasFoundException(msfe);
                return true;
            case MalformedFieldException mfe:
                HandleMalformedFieldException(mfe);
                return true;
            case DecimalOverflowException doe:
                HandleDecimalOverflowException(doe);
                return true;
            case FileNotFoundException fnfe:
                HandleFileNotFoundException(fnfe);
                return true;
            case OperationCanceledException:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Converts a 0 to 1 fraction into increments for a <see cref="LoadingIcon"/> with a maximum of 100
    /// </summary>
    private sealed class LoadingIconPercentageProgress(LoadingIcon loadingIcon) : IProgress<double>
    {
        private int _reportedPercentage;

        public void Report(double fraction)
        {
            var percentage = (int)Math.Clamp(fraction * 100, 0, 100);
            if (percentage > _reportedPercentage)
            {
                loadingIcon.Report(percentage - _reportedPercentage);
                _reportedPercentage = percentage;
            }
        }
    }
}
