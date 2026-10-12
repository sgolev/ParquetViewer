using ParquetViewer.Controls;
using ParquetViewer.Helpers;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace ParquetViewer;

public partial class GoToRowForm : FormBase
{
    /// <summary>
    /// The 0-based row (or match) index the user wants to jump to
    /// </summary>
    /// <remarks>The user enters 1-based row numbers, the same as the ones shown in the row headers</remarks>
    public long SelectedRowIndex => (long)rowIndexNumericUpDown.Value - 1;

    public GoToRowForm()
    {
        InitializeComponent();
        Text = Resources.Strings.GoToRowFormTitle;
        goButton.Text = Resources.Strings.GoToRowButtonText;
        cancelButton.Text = Resources.Strings.CancelButtonText;
    }

    /// <param name="maxRowIndex">The largest 0-based row (or match) index the user can jump to</param>
    /// <param name="initialRowIndex">The 0-based row (or match) index to start with</param>
    /// <param name="isMatchNumber">True if the user is picking the n-th match of a filter query instead of a row in the file</param>
    /// <param name="knownMatchCount">The number of matches found so far, if the query hasn't scanned the whole file yet</param>
    public GoToRowForm(long maxRowIndex, long initialRowIndex, bool isMatchNumber, long? knownMatchCount = null) : this()
    {
        var maxRowNumber = Math.Max(maxRowIndex, 0) + 1;
        var maxRowNumberText = knownMatchCount is null ? maxRowNumber.ToString("N0") : $"{knownMatchCount:N0}+";
        rowIndexLabel.Text = (isMatchNumber ? Resources.Strings.GoToMatchLabelTextFormat : Resources.Strings.GoToRowLabelTextFormat).Format(maxRowNumberText);
        rowIndexNumericUpDown.Minimum = 1;
        rowIndexNumericUpDown.Maximum = maxRowNumber;
        rowIndexNumericUpDown.Value = Math.Clamp(initialRowIndex + 1, 1, maxRowNumber);
    }

    private void GoToRowForm_Shown(object? sender, EventArgs e)
    {
        rowIndexNumericUpDown.Focus();
        rowIndexNumericUpDown.Select(0, rowIndexNumericUpDown.Text.Length);
    }

    public override void SetTheme(Theme theme)
    {
        if (DesignMode)
        {
            return;
        }

        base.SetTheme(theme);
        rowIndexNumericUpDown.BackColor = theme.CellBackgroundColor;
        rowIndexNumericUpDown.ForeColor = theme.TextColor;
        goButton.ForeColor = Color.Black;
        cancelButton.ForeColor = Color.Black;
    }
}
