using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace ParquetViewer.Controls;

public class DelayedOnChangedTextBox : TextBox
{
    private Timer? _delayedTextChangedTimer;

    public event EventHandler? DelayedTextChanged;

    public DelayedOnChangedTextBox()
        : base()
    {
        DelayedTextChangedTimeout = 1 * 1000;
    }

    public DelayedOnChangedTextBox(int secondsDelay)
        : base()
    {
        DelayedTextChangedTimeout = secondsDelay * 1000;
    }

    protected override void Dispose(bool disposing)
    {
        if (_delayedTextChangedTimer is not null)
        {
            _delayedTextChangedTimer.Stop();
            if (disposing)
                _delayedTextChangedTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public int DelayedTextChangedTimeout { get; set; }

    protected virtual void OnDelayedTextChanged(EventArgs e)
    {
        DelayedTextChanged?.Invoke(this, e);
    }

    protected override void OnTextChanged(EventArgs e)
    {
        InitializeDelayedTextChangedEvent();
        base.OnTextChanged(e);
    }

    private void InitializeDelayedTextChangedEvent()
    {
        _delayedTextChangedTimer?.Stop();

        if (_delayedTextChangedTimer is null || _delayedTextChangedTimer.Interval != DelayedTextChangedTimeout)
        {
            _delayedTextChangedTimer = new Timer();
            _delayedTextChangedTimer.Tick += new EventHandler(HandleDelayedTextChangedTimerTick);
            _delayedTextChangedTimer.Interval = DelayedTextChangedTimeout;
        }

        _delayedTextChangedTimer.Start();
    }

    private void HandleDelayedTextChangedTimerTick(object? sender, EventArgs e)
    {
        var timer = sender as Timer;
        timer?.Stop();

        OnDelayedTextChanged(EventArgs.Empty);
    }
}