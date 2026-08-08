using Eto.Forms;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.PlugIns;
using Rhino.UI;
using RhinoMM.Plugin.Persistence;
using RhinoMM.Plugin.UI;
using RhinoCommand = Rhino.Commands.Command;

namespace RhinoMM.Plugin.Services;

internal static class ViewportQuickEditorService
{
    private const string AutoShowKey = "QuickEditor.AutoShow";
    private static readonly UITimer SelectionTimer = new() { Interval = 0.25 };
    private static readonly UITimer VisibilityTimer = new() { Interval = 0.25 };
    private static PersistentSettings? _settings;
    private static ViewportQuickEditorWindow? _window;
    private static Guid _dismissedComponentId;
    private static bool _initialized;
    private static int _commandDepth;

    public static bool AutoShowEnabled { get; private set; } = true;

    public static void Initialize(PersistentSettings settings)
    {
        if (_initialized)
            return;
        _settings = settings;
        try
        {
            AutoShowEnabled = settings.GetBool(AutoShowKey, true);
        }
        catch
        {
            AutoShowEnabled = true;
        }
        SelectionTimer.Elapsed += SelectionTimerElapsed;
        VisibilityTimer.Elapsed += VisibilityTimerElapsed;
        RhinoDoc.SelectObjects += SelectionChanged;
        RhinoDoc.DeselectAllObjects += SelectionChanged;
        RhinoDoc.CloseDocument += CloseDocument;
        RhinoCommand.BeginCommand += BeginCommand;
        RhinoCommand.EndCommand += EndCommand;
        _initialized = true;
    }

    public static void Shutdown()
    {
        if (!_initialized)
            return;
        SelectionTimer.Stop();
        SelectionTimer.Elapsed -= SelectionTimerElapsed;
        VisibilityTimer.Stop();
        VisibilityTimer.Elapsed -= VisibilityTimerElapsed;
        RhinoDoc.SelectObjects -= SelectionChanged;
        RhinoDoc.DeselectAllObjects -= SelectionChanged;
        RhinoDoc.CloseDocument -= CloseDocument;
        RhinoCommand.BeginCommand -= BeginCommand;
        RhinoCommand.EndCommand -= EndCommand;
        Hide(false);
        _settings = null;
        _initialized = false;
    }

    public static void SetAutoShowEnabled(bool enabled)
    {
        AutoShowEnabled = enabled;
        try
        {
            _settings?.SetBool(AutoShowKey, enabled);
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"快速编辑器设置未能保存：{ex.Message}");
        }
        if (!enabled)
            Hide(false);
        else
            ScheduleSelectionEvaluation();
    }

    public static bool ShowForCurrentSelection(RhinoDoc? document, bool force)
    {
        var doc = document ?? RhinoDoc.ActiveDoc;
        if (doc is null || (_commandDepth > 0 && !force))
            return false;
        var selected = ComponentRepository.ReadSelectedControlPoints(doc);
        if (selected.Count != 1)
        {
            Hide(false);
            return false;
        }
        var component = selected[0];
        if (!force && (!AutoShowEnabled || component.ComponentId == _dismissedComponentId))
            return false;
        if (force)
            _dismissedComponentId = Guid.Empty;
        if (_window is not null && _window.ComponentId == component.ComponentId)
        {
            _window.LoadComponent(component);
            if (!_window.TryReposition())
            {
                Hide(false);
                return false;
            }
            return true;
        }
        Hide(false);
        _window = new ViewportQuickEditorWindow(doc, component);
        _window.DismissedByUser += WindowDismissedByUser;
        _window.Closed += WindowClosed;
        _window.Show();
        if (!_window.TryReposition())
        {
            Hide(false);
            return false;
        }
        VisibilityTimer.Start();
        RhinoApp.SetFocusToMainWindow();
        return true;
    }

    public static void Hide(bool rememberDismissal)
    {
        SelectionTimer.Stop();
        VisibilityTimer.Stop();
        if (_window is null)
            return;
        if (rememberDismissal)
            _dismissedComponentId = _window.ComponentId;
        var window = _window;
        _window = null;
        window.DismissedByUser -= WindowDismissedByUser;
        window.Closed -= WindowClosed;
        window.CloseProgrammatically();
    }

    public static void SuppressForComponent(Guid componentId)
    {
        _dismissedComponentId = componentId;
        SelectionTimer.Stop();
        Hide(false);
    }

    private static void SelectionChanged(object? sender, RhinoObjectSelectionEventArgs e)
    {
        if (!e.Selected)
            _dismissedComponentId = Guid.Empty;
        ScheduleSelectionEvaluation();
    }

    private static void SelectionChanged(object? sender, RhinoDeselectAllObjectsEventArgs e)
    {
        _dismissedComponentId = Guid.Empty;
        Hide(false);
    }

    private static void ScheduleSelectionEvaluation()
    {
        SelectionTimer.Stop();
        if (_commandDepth == 0 && AutoShowEnabled)
            SelectionTimer.Start();
    }

    private static void SelectionTimerElapsed(object? sender, EventArgs e)
    {
        SelectionTimer.Stop();
        ShowForCurrentSelection(RhinoDoc.ActiveDoc, false);
    }

    private static void VisibilityTimerElapsed(object? sender, EventArgs e)
    {
        if (_window is null || _commandDepth > 0)
            return;
        var doc = RhinoDoc.ActiveDoc;
        var selected = doc is null ? [] : ComponentRepository.ReadSelectedControlPoints(doc);
        if (doc is null
            || _window.DocumentSerialNumber != doc.RuntimeSerialNumber
            || selected.Count != 1
            || selected[0].ComponentId != _window.ComponentId
            || !_window.TryReposition())
            Hide(false);
    }

    private static void BeginCommand(object? sender, CommandEventArgs e)
    {
        _commandDepth++;
        Hide(false);
    }

    private static void EndCommand(object? sender, CommandEventArgs e)
    {
        _commandDepth = Math.Max(0, _commandDepth - 1);
        ScheduleSelectionEvaluation();
    }

    private static void CloseDocument(object? sender, DocumentEventArgs e)
    {
        if (_window is not null && _window.DocumentSerialNumber == e.Document.RuntimeSerialNumber)
            Hide(false);
    }

    private static void WindowDismissedByUser(object? sender, EventArgs e)
    {
        if (_window is not null)
            _dismissedComponentId = _window.ComponentId;
    }

    private static void WindowClosed(object? sender, EventArgs e)
    {
        if (ReferenceEquals(sender, _window))
            _window = null;
    }
}
