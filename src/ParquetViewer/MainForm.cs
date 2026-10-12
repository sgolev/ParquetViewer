using ParquetViewer.Analytics;
using ParquetViewer.Controls;
using ParquetViewer.Engine;
using ParquetViewer.Engine.Exceptions;
using ParquetViewer.Helpers;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ParquetViewer;

public partial class MainForm : FormBase
{
    private readonly string _defaultFormTitle;

    #region Members
    private readonly string? _fileToLoadOnLaunch;
    private string? _openFileOrFolderPath;
    /// <remarks>Setting this only resets the UI. Use <see cref="CloseCurrentFileAsync"/> to close the open file.</remarks>
    private string? OpenFileOrFolderPath
    {
        get => _openFileOrFolderPath;
        set
        {
            _openFileOrFolderPath = value;
            SelectedFields = null;
            changeFieldsMenuStripButton.Enabled = false;
            goToRowToolStripMenuItem.Enabled = false;
            getSQLCreateTableScriptToolStripMenuItem.Enabled = false;
            saveAsToolStripMenuItem.Enabled = false;
            metadataViewerToolStripMenuItem.Enabled = false;
            UpdatePager();
            searchFilterTextBox.PlaceholderText = "WHERE ";
            mainGridView.ClearQuickPeekForms();
            mainGridView.ClearColumnFormatOverrides();
            ResetGetSQLCreateTableScriptToolStripMenuItemToolTipText();

            if (string.IsNullOrWhiteSpace(_openFileOrFolderPath))
            {
                Text = _defaultFormTitle;
            }
            else
            {
                if (File.Exists(_openFileOrFolderPath))
                    Text = Resources.Strings.MainWindowOpenFileTitleFormat.Format(_openFileOrFolderPath);
                else
                    Text = Resources.Strings.MainWindowOpenFolderTitleFormat.Format(_openFileOrFolderPath);

                changeFieldsMenuStripButton.Enabled = true;
                goToRowToolStripMenuItem.Enabled = true;
                saveAsToolStripMenuItem.Enabled = true;
                getSQLCreateTableScriptToolStripMenuItem.Enabled = true;
                metadataViewerToolStripMenuItem.Enabled = true;
            }
        }
    }

    private List<string>? _selectedFields = null;
    private List<string>? SelectedFields
    {
        get => _selectedFields;
        set
        {
            _selectedFields = value?.ToList();

            //Check for duplicate fields (We don't support case sensitive field names unfortunately)
            var duplicateFields = _selectedFields?.GroupBy(f => f.ToUpperInvariant()).Where(g => g.Count() > 1).SelectMany(g => g).ToList();
            if (duplicateFields?.Count > 0)
            {
                _selectedFields = _selectedFields!.Where(f => !duplicateFields.Any(df => df.Equals(f, StringComparison.InvariantCultureIgnoreCase))).ToList();

                MessageBox.Show($"The following duplicate fields could not be loaded: {string.Join(',', duplicateFields)}. " +
                        $"{Environment.NewLine}{Environment.NewLine}Case sensitive field names are not currently supported.",
                        "Duplicate fields detected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            if (value?.Count > 0)
            {
                LoadFileToGridview();
            }
        }
    }

    private bool IsAnyFileOpen
        => !string.IsNullOrWhiteSpace(OpenFileOrFolderPath)
            && _openParquetEngine is not null;

    private DataTable? _mainDataSource;
    private DataTable? MainDataSource
    {
        get => _mainDataSource;
        set
        {
            _mainDataSource = value;
            mainGridView.DataSource = _mainDataSource;

            if (_mainDataSource is not null)
            {
                SetSampleQueryAsPlaceHolder();
            }
        }
    }

#pragma warning disable CA1859 // Use concrete types when possible for improved performance
    private IParquetEngine? _openParquetEngine;
#pragma warning restore CA1859 // Self-Contained executable will assign DuckDB engines to this

    private (DateTime LastWriteTimeUtc, long Length)? _originalModifiedInfo;
    #endregion

    public MainForm()
    {
        InitializeComponent();
        _defaultFormTitle = Text;
        MainDataSource = new DataTable();
        OpenFileOrFolderPath = null;

        //Have to set these here because it gets deleted from the .Designer.cs file for some reason
        metadataViewerToolStripMenuItem.Image = Resources.Icons.text_file_icon_16x16.ToBitmap();
        iSO8601ToolStripMenuItem.ToolTipText = ExtensionMethods.ISO_8601_DATE_TIME_FORMAT;
    }

    public MainForm(string? fileToOpenPath) : this()
    {
        if (fileToOpenPath is not null)
        {
            //The code below will be executed after the default constructor => this()
            _fileToLoadOnLaunch = fileToOpenPath;
        }
    }

    private async void MainForm_Load(object sender, EventArgs e)
    {
        //Open existing file on first load. Usually this means user double-clicked a parquet file with this utility as the default program.
        if (!string.IsNullOrWhiteSpace(_fileToLoadOnLaunch))
        {
            await OpenNewFileOrFolder(_fileToLoadOnLaunch);
        }

        //Check necessary toolstrip menu items
        RefreshDateFormatMenuItemSelection();
        darkModeToolStripMenuItem.Checked = AppSettings.DarkMode;
        RefreshExperimentalFeatureToolStrips();
        SetLanguageCheckmark();

        //Get user's consent to gather analytics; and update the toolstrip menu item accordingly
        Program.GetUserConsentToGatherAnalytics();
        shareAnonymousUsageDataToolStripMenuItem.Checked = AppSettings.AnalyticsDataGatheringConsent;

        //Ask the user if they want to enable dark mode (only if their system is in dark mode)
        Program.AskUserIfTheyWantToSwitchToDarkMode();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        //Stop any pending page loads or query scans
        _loadCts.Cancel();
        base.OnFormClosing(e);
    }

    private async Task<List<string>?> OpenFieldSelectionDialog(bool forceOpenDialog)
    {
        if (string.IsNullOrWhiteSpace(OpenFileOrFolderPath))
        {
            return null;
        }

        if (_openParquetEngine is null)
        {
            try
            {
                _openParquetEngine = await Engine.ParquetNET.ParquetEngine.OpenFileOrFolderAsync(OpenFileOrFolderPath);
            }
            catch (Exception ex)
            {
                if (_openParquetEngine is null)
                {
                    //cancel the file open
                    await CloseCurrentFileAsync();
                }

                if (ex is AllFilesSkippedException afse)
                {
                    HandleAllFilesSkippedException(afse);
                }
                else if (ex is SomeFilesSkippedException sfse)
                {
                    HandleSomeFilesSkippedException(sfse);
                }
                else if (ex is Engine.Exceptions.FileReadException fre)
                {
                    MainForm.HandleFileReadException(fre);
                }
                else if (ex is MultipleSchemasFoundException msfe)
                {
                    HandleMultipleSchemasFoundException(msfe);
                }
                else if (ex is FileNotFoundException fnfe)
                {
                    HandleFileNotFoundException(fnfe);
                }
                else if (ex is not OperationCanceledException)
                {
                    throw;
                }

                return null;
            }
        }

        List<string>? fields = null;
        try
        {
            fields = _openParquetEngine.Fields;
        }
        catch (ArgumentException ex) when (ex.Message.StartsWith("at least one field is required"))
        { /*Swallow: This exception is thrown from Parquet.Net when the schema has no fields*/ }
        catch (Exception ex)
        {
            throw new Parquet.ParquetException(Resources.Errors.ParquetSchemaReadErrorMessage, ex);
        }

        if (fields?.Count > 0)
        {
            if (AppSettings.AlwaysSelectAllFields && !forceOpenDialog)
            {
                return fields;
            }
            else
            {
                using var fieldSelectionForm = new FieldsToLoadForm(fields, GetUserColumnNames());
                if (fieldSelectionForm.ShowDialog(this) == DialogResult.OK && fieldSelectionForm.NewSelectedFields?.Count > 0)
                {
                    return fieldSelectionForm.NewSelectedFields;
                }
                else
                {
                    return null;
                }
            }
        }
        else
        {
            ShowError(Resources.Errors.NoFieldsFoundErrorMessage, Resources.Errors.NoFieldsFoundErrorTitle);
            return null;
        }
    }

    private async void LoadFileToGridview()
    {
        if (_openParquetEngine is null)
            return;

        try
        {
            await LoadFileToGridviewImpl(_openParquetEngine);
        }
        catch (Exception ex)
        {
            if (!await TryReloadWithDuckDBAsync(ex, null, 0))
                throw;
        }

        _originalModifiedInfo = null;
    }

    private async Task LoadFileToGridviewImpl(IParquetEngine engine)
    {
        var stopwatch = Stopwatch.StartNew();
        var loadTime = TimeSpan.Zero;
        var indexTime = TimeSpan.Zero;
        LoadingIcon? loadingIcon = null;
        var wasSuccessful = false;
        var loadedRowCount = 0;
        try
        {
            if (!IsAnyFileOpen)
                return;

            if (SelectedFields is null || SelectedFields.Count == 0)
                return;

            if (!File.Exists(OpenFileOrFolderPath) && !Directory.Exists(OpenFileOrFolderPath))
            {
                ShowError(Resources.Errors.OpenFileNoLongerExistsErrorMessageFormat.Format(OpenFileOrFolderPath + Environment.NewLine));
                return;
            }

            var fields = SelectedFields.ToList();
            var viewGeneration = StartNewViewGeneration();
            var isFullLoad = IsUnderFullLoadLimit(engine.RecordCount, fields.Count);
            var rowsToRead = isFullLoad ? (int)Math.Max(engine.RecordCount, 1) : PAGE_SIZE;

            long cellCount = fields.Count * Math.Min(rowsToRead, engine.RecordCount);
            loadingIcon = ShowLoadingIcon(Resources.Strings.LoadingDataLabelText, cellCount);
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(loadingIcon.CancellationToken, _loadCts.Token);

            (var firstPage, loadTime, indexTime) = await ReadSliceAsync(engine, fields, 0, rowsToRead, loadingIcon, cancellation.Token);
            if (viewGeneration != _viewGeneration)
            {
                firstPage.Dispose();
                return; //Something else was loaded in the meantime
            }

            var rowIndexColumnName = PagedRowSource.GetRowIndexColumnName(fields);
            PagedRowSource.AddRowIndexColumn(firstPage, rowIndexColumnName, 0);
            var pageSource = new UnfilteredPageSource(CreateReadSlice(engine, fields), engine.RecordCount, PAGE_SIZE, rowIndexColumnName);
            ShowNewDataSource(firstPage, pageSource, fields, isFullLoad);

            loadedRowCount = firstPage.Rows.Count;
            wasSuccessful = true;
        }
        catch (Exception ex) when (TryHandleLoadException(ex))
        {
        }
        finally
        {
            stopwatch.Stop();

            var totalTime = stopwatch.Elapsed;
            var renderTime = totalTime - loadTime - indexTime;

            //Little secret performance counter
            pageStatusBarLabel.ToolTipText = $"Total time: {totalTime:mm\\:ss\\.ff}" + Environment.NewLine +
            $"    Load time: {loadTime:mm\\:ss\\.ff}" + Environment.NewLine +
            $"    Index time: {indexTime:mm\\:ss\\.ff}" + Environment.NewLine +
            $"    Render time: {renderTime:mm\\:ss\\.ff}" + Environment.NewLine +
            $"Engine: {(engine is Engine.ParquetNET.ParquetEngine ? "ParquetNET" : "DuckDB")}";

            loadingIcon?.Dispose();

            if (wasSuccessful)
            {
                var engineType = _openParquetEngine is Engine.ParquetNET.ParquetEngine
                    ? FileOpenEvent.ParquetEngineTypeId.ParquetNET
                    : FileOpenEvent.ParquetEngineTypeId.DuckDB;

                var userColumns = GetUserColumns().ToList();
                FileOpenEvent.FireAndForget(
                    Directory.Exists(OpenFileOrFolderPath),
                    engine.NumberOfPartitions,
                    engine.RecordCount,
                    engine.Metadata.RowGroups.Count,
                    engine.Fields.Count,
                    userColumns.Select(column => column.DataType.Name).Distinct().Order().ToArray(),
                    0,
                    loadedRowCount,
                    userColumns.Count,
                    (long)totalTime.TotalMilliseconds,
                    (long)loadTime.TotalMilliseconds,
                    (long)indexTime.TotalMilliseconds,
                    (long)renderTime.TotalMilliseconds,
                    engineType);
            }
        }
    }

    private async Task OpenNewFileOrFolder(string fileOrFolderPath)
    {
        await CloseCurrentFileAsync();
        OpenFileOrFolderPath = fileOrFolderPath;

        var fieldList = await OpenFieldSelectionDialog(false);
        if (fieldList is not null)
        {
            SelectedFields = fieldList; //triggers a file load
            AppSettings.OpenedFileCount++;
            Program.AskUserForFileExtensionAssociation();
        }
    }

    /// <summary>
    /// Checks <see cref="AppSettings.DateTimeDisplayFormat"/> and checks/unchecks
    /// the appropriate date format options located in the menu bar.
    /// </summary>
    private void RefreshDateFormatMenuItemSelection()
    {
        defaultToolStripMenuItem.Checked = false;
        iSO8601ToolStripMenuItem.Checked = false;
        customDateFormatToolStripMenuItem.Checked = false;

        switch (AppSettings.DateTimeDisplayFormat)
        {
            case DateFormat.Default:
                defaultToolStripMenuItem.Checked = true;
                break;
            case DateFormat.ISO8601:
                iSO8601ToolStripMenuItem.Checked = true;
                break;
            case DateFormat.Custom:
                customDateFormatToolStripMenuItem.Checked = true;
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// Provides the user with a sample query in <see cref="searchFilterTextBox"/>
    /// using the first primitive field available in the dataset. If none are found,
    /// the placeholder won't contain a sample. Only the "WHERE ".
    /// </summary>
    private void SetSampleQueryAsPlaceHolder()
    {
        searchFilterTextBox.PlaceholderText = "WHERE ";

        if (MainDataSource is null || MainDataSource.Rows.Count == 0)
            return;

        var simpleColumn = GetUserColumns().FirstOrDefault(c => c.DataType.IsSimple());
        if (simpleColumn is null)
            return;

        //find a value we can use as a sample
        object sampleSimpleValue = DBNull.Value;
        int counter = 1000;
        foreach (DataRow row in MainDataSource.Rows)
        {
            sampleSimpleValue = row[simpleColumn];
            if (counter <= 0 || (sampleSimpleValue != DBNull.Value))
            {
                break;
            }
            counter--;
        }

        if (sampleSimpleValue == DBNull.Value)
            return;

        string placeholder = ParquetGridView.GenerateFilterQuery(simpleColumn.ColumnName, simpleColumn.DataType, sampleSimpleValue);
        if (placeholder.Length < 100) //Only set the placeholder query if it's reasonably short
            searchFilterTextBox.PlaceholderText = $"WHERE {placeholder}";
    }

    private void SetLanguageCheckmark()
    {
        if (AppSettings.UserSelectedCulture is not null)
        {
            languageToolStripMenuItem.DropDownItems.OfType<ToolStripMenuItem>().ToList().ForEach(languageToolStripItem =>
            {
                languageToolStripItem.Checked = languageToolStripItem.Tag?.ToString() == AppSettings.UserSelectedCulture.ToString();
            });
        }
        else
        {
            //We default to English
            englishToolStripMenuItem.Checked = true;
        }
    }
}
