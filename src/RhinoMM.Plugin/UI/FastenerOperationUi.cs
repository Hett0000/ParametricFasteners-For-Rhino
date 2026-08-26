using Eto.Drawing;
using Eto.Forms;
using Rhino;
using Rhino.UI;

namespace RhinoMM.Plugin.UI;

internal enum FastenerOperationState
{
    Idle,
    Preparing,
    Running,
    Success,
    Warning,
    Failure,
    Cancelled
}

internal sealed record FastenerOperationProgress(
    FastenerOperationState State,
    string Phase,
    int Completed = 0,
    int Total = 0,
    string Detail = "")
{
    public double Fraction => Total <= 0 ? 0 : Math.Clamp((double)Completed / Total, 0, 1);
}

internal sealed class FastenerOperationStatusView : Panel
{
    private readonly Label _phase = FastenerUiTheme.SecondaryLabel();
    private readonly Label _count = FastenerUiTheme.SecondaryLabel();
    private readonly ProgressBar _progress = new() { MinValue = 0, MaxValue = 1000, Height = 4 };
    private readonly Button _cancel = new() { Text = "取消", Width = 68 };
    private readonly UITimer _successTimer = new() { Interval = 1.2 };

    public event EventHandler? CancelRequested;
    public FastenerOperationState State { get; private set; }

    public FastenerOperationStatusView()
    {
        _cancel.Click += (_, _) => CancelRequested?.Invoke(this, EventArgs.Empty);
        _successTimer.Elapsed += (_, _) =>
        {
            _successTimer.Stop();
            if (State == FastenerOperationState.Success)
                Set(new FastenerOperationProgress(FastenerOperationState.Idle, string.Empty));
        };
        Content = new TableLayout
        {
            Spacing = new Size(FastenerUiTheme.SpaceSmall, 2),
            Rows =
            {
                new TableRow(_phase, new TableCell(null, true), _count, _cancel),
                _progress
            }
        };
        Set(new FastenerOperationProgress(FastenerOperationState.Idle, string.Empty));
    }

    public void Set(FastenerOperationProgress value)
    {
        State = value.State;
        _phase.Text = string.IsNullOrWhiteSpace(value.Detail) ? value.Phase : $"{value.Phase} · {value.Detail}";
        _count.Text = value.Total > 0 ? $"{value.Completed}/{value.Total}" : string.Empty;
        _progress.Value = (int)Math.Round(value.Fraction * 1000);
        var running = value.State is FastenerOperationState.Preparing or FastenerOperationState.Running;
        _progress.Visible = running;
        _cancel.Visible = running;
        Visible = value.State != FastenerOperationState.Idle || !string.IsNullOrWhiteSpace(value.Phase);
        var role = value.State switch
        {
            FastenerOperationState.Success => FastenerThemeRole.StatusSuccess,
            FastenerOperationState.Warning => FastenerThemeRole.StatusWarning,
            FastenerOperationState.Failure => FastenerThemeRole.StatusError,
            FastenerOperationState.Cancelled => FastenerThemeRole.SecondaryText,
            _ => FastenerThemeRole.StatusInfo
        };
        FastenerUiTheme.SetRole(_phase, role);
        FastenerUiTheme.SetRole(_count, role);
        if (value.State == FastenerOperationState.Success)
        {
            _successTimer.Stop();
            _successTimer.Start();
        }
        Invalidate();
    }
}

internal sealed class FastenerUiOperationContext
{
    private readonly Action<FastenerOperationProgress>? _report;
    public bool IsCancellationRequested { get; private set; }

    public FastenerUiOperationContext(Action<FastenerOperationProgress>? report = null) => _report = report;

    public void Cancel() => IsCancellationRequested = true;

    public bool Yield(string phase, int completed, int total, string detail = "")
    {
        _report?.Invoke(new FastenerOperationProgress(
            FastenerOperationState.Running, phase, completed, total, detail));
        RhinoApp.Wait();
        return !IsCancellationRequested;
    }
}

internal sealed class FastenerProgressWindow : Form
{
    private readonly FastenerOperationStatusView _status = new();
    private readonly FastenerUiOperationContext _context;

    private FastenerProgressWindow(string title, string phase)
    {
        Title = title;
        ClientSize = new Size(420, 116);
        MinimumSize = new Size(340, 116);
        Resizable = false;
        ShowInTaskbar = false;
        Maximizable = false;
        Minimizable = false;
        _context = new FastenerUiOperationContext(progress =>
        {
            if (!IsDisposed)
                _status.Set(progress);
        });
        _status.CancelRequested += (_, _) => _context.Cancel();
        Content = FastenerUiTheme.CreateWindowShell(
            FastenerUiTheme.CreateCard(FastenerUiTheme.SectionTitle(title), FastenerUiTheme.SpaceSmall),
            new Panel(),
            _status,
            new Panel(),
            FastenerUiTheme.SpaceMedium);
        FastenerUiTheme.WatchWindow(this);
        _status.Set(new FastenerOperationProgress(FastenerOperationState.Preparing, phase));
    }

    public static (T Result, bool Cancelled) Run<T>(
        string title,
        string phase,
        Func<FastenerUiOperationContext, T> action)
    {
        using var window = new FastenerProgressWindow(title, phase);
        window.Owner = RhinoEtoApp.MainWindow;
        window.Show();
        RhinoApp.Wait();
        try
        {
            var result = action(window._context);
            return (result, window._context.IsCancellationRequested);
        }
        finally
        {
            window.Close();
        }
    }
}
