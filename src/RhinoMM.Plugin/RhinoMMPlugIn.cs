using Rhino.PlugIns;
using Rhino.UI;
using System.Runtime.InteropServices;
using RhinoMM.Core.Services;
using RhinoMM.Plugin.Services;
using RhinoMM.Plugin.UI;

namespace RhinoMM.Plugin;

public sealed class RhinoMMPlugIn : PlugIn
{
    private static readonly Guid ToolbarFileId = new("5c120a44-494a-4973-a495-e8c4218b0c22");
    private static bool _toolbarReconcilePending;

    public static RhinoMMPlugIn? Instance { get; private set; }
    public static FastenerCatalog Catalog { get; } = FastenerCatalog.LoadEmbedded();
    public override PlugInLoadTime LoadTime => PlugInLoadTime.AtStartup;

    internal static string AssemblyPath => typeof(RhinoMMPlugIn).Assembly.Location;
    internal static string ExpectedToolbarPath => Path.ChangeExtension(AssemblyPath, ".rui");
    internal static string? ActiveToolbarPath { get; private set; }
    internal static string ToolbarHealth { get; private set; } = "等待 Rhino 初始化工具栏";

    public RhinoMMPlugIn() => Instance = this;

    protected override LoadReturnCode OnLoad(ref string errorMessage)
    {
        try
        {
            PlacementPresetService.Load(Settings);
            HeatSetInsertPresetService.Load(Settings);
            GlobalDisplaySettingsService.Load(Settings);
            FastenerTemplateLibraryService.Load(Settings);
            AssemblyInspectionSettingsService.Load(Settings);
            UserFastenerLibraryService.Load(Settings);
            OutputCenterSettingsService.Load(Settings);
            Panels.RegisterPanel(
                this,
                typeof(RhinoMMPanel),
                "参数化紧固件",
                typeof(RhinoMMPlugIn).Assembly,
                PanelIconProvider.PanelResourceName,
                PanelType.System);
            Panels.RegisterPanel(
                this,
                typeof(ComponentNavigatorPanel),
                "紧固件导航器",
                typeof(RhinoMMPlugIn).Assembly,
                PanelIconProvider.PanelResourceName,
                PanelType.System);
            ComponentLifecycleService.Initialize();
            FastenerDocumentIndexService.Initialize();
            ComponentDocumentHealthService.Initialize();
            ViewportQuickEditorService.Initialize(Settings);
            ScheduleToolbarReconcile();
            var commandCount = UI.FastenerVersionInfo.CommandTypeCount;
            Rhino.RhinoApp.WriteLine(
                $"参数化紧固件 v{UI.FastenerVersionInfo.PluginVersion} 已加载；" +
                $"Rhino {Rhino.RhinoApp.Version}；运行时：{RuntimeInformation.FrameworkDescription}；" +
                $"安装方式：{UI.FastenerVersionInfo.InstallationType}；程序集：{AssemblyPath}；" +
                $"加载模式：{LoadTime}；发现命令：{commandCount}。");
            return LoadReturnCode.Success;
        }
        catch (Exception ex)
        {
            errorMessage = $"参数化紧固件初始化失败：{ex}";
            Rhino.RhinoApp.WriteLine(errorMessage);
            return LoadReturnCode.ErrorShowDialog;
        }
    }

    protected override void OnShutdown()
    {
        Rhino.RhinoApp.Idle -= OnFirstIdle;
        _toolbarReconcilePending = false;
        ComponentLifecycleService.Shutdown();
        ComponentDocumentHealthService.Shutdown();
        FastenerDocumentIndexService.Shutdown();
        ViewportQuickEditorService.Shutdown();
        base.OnShutdown();
    }

    private static void ScheduleToolbarReconcile()
    {
        if (_toolbarReconcilePending)
            return;
        _toolbarReconcilePending = true;
        Rhino.RhinoApp.Idle += OnFirstIdle;
    }

    private static void OnFirstIdle(object? sender, EventArgs e)
    {
        Rhino.RhinoApp.Idle -= OnFirstIdle;
        _toolbarReconcilePending = false;
        ReconcileToolbar();
    }

    internal static void ReconcileToolbar()
    {
        try
        {
            var toolbarPath = ExpectedToolbarPath;
            if (!File.Exists(toolbarPath))
            {
                ActiveToolbarPath = null;
                ToolbarHealth = $"缺少工具栏文件：{toolbarPath}";
                Rhino.RhinoApp.WriteLine($"参数化紧固件工具栏未加载：{ToolbarHealth}");
                return;
            }

            var current = Rhino.RhinoApp.ToolbarFiles.FindByPath(toolbarPath);
            var stale = new List<ToolbarFile>();
            for (var i = 0; i < Rhino.RhinoApp.ToolbarFiles.Count; i++)
            {
                var candidate = Rhino.RhinoApp.ToolbarFiles[i];
                if (candidate is null || candidate.Id != ToolbarFileId)
                    continue;
                if (PathsEqual(candidate.Path, toolbarPath))
                    current ??= candidate;
                else
                    stale.Add(candidate);
            }

            foreach (var toolbar in stale)
            {
                if (!toolbar.Close(false))
                {
                    ActiveToolbarPath = toolbar.Path;
                    ToolbarHealth = $"旧工具栏正在使用，无法切换：{toolbar.Path}";
                    Rhino.RhinoApp.WriteLine($"参数化紧固件工具栏自修复未完成：{ToolbarHealth}");
                    return;
                }
                Rhino.RhinoApp.WriteLine($"参数化紧固件已关闭旧路径工具栏：{toolbar.Path}");
            }

            if (current is null)
            {
                Rhino.RhinoApp.ToolbarFiles.Open(toolbarPath);
                current = Rhino.RhinoApp.ToolbarFiles.FindByPath(toolbarPath);
            }

            ActiveToolbarPath = current?.Path ?? toolbarPath;
            ToolbarHealth = current is null ? "Rhino 未确认工具栏已经打开" : "安装正常";
            Rhino.RhinoApp.WriteLine($"参数化紧固件工具栏：{ActiveToolbarPath}；{ToolbarHealth}。");
        }
        catch (Exception ex)
        {
            ActiveToolbarPath = null;
            ToolbarHealth = $"工具栏自修复失败：{ex.Message}";
            Rhino.RhinoApp.WriteLine($"参数化紧固件工具栏未自动加载：{ex.Message}");
        }
    }

    private static bool PathsEqual(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return false;
        try
        {
            return string.Equals(
                Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }
}
