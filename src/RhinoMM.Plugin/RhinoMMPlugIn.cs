using Rhino.PlugIns;
using Rhino.UI;
using RhinoMM.Core.Services;
using RhinoMM.Plugin.Services;
using RhinoMM.Plugin.UI;

namespace RhinoMM.Plugin;

public sealed class RhinoMMPlugIn : PlugIn
{
    public static RhinoMMPlugIn? Instance { get; private set; }
    public static FastenerCatalog Catalog { get; } = FastenerCatalog.LoadEmbedded();

    public RhinoMMPlugIn() => Instance = this;

    protected override LoadReturnCode OnLoad(ref string errorMessage)
    {
        try
        {
            PlacementPresetService.Load(Settings);
            HeatSetInsertPresetService.Load(Settings);
            GlobalDisplaySettingsService.Load(Settings);
            FastenerTemplateLibraryService.Load(Settings);
            Panels.RegisterPanel(
                this,
                typeof(RhinoMMPanel),
                "参数化紧固件",
                typeof(RhinoMMPlugIn).Assembly,
                PanelIconProvider.PanelResourceName,
                PanelType.System);
            ComponentLifecycleService.Initialize();
            ViewportQuickEditorService.Initialize(Settings);
            LoadToolbar();
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
        ComponentLifecycleService.Shutdown();
        ViewportQuickEditorService.Shutdown();
        base.OnShutdown();
    }

    private void LoadToolbar()
    {
        try
        {
            var rhpPath = GetType().Assembly.Location;
            var toolbarPath = Path.ChangeExtension(rhpPath, ".rui");
            if (!File.Exists(toolbarPath))
                return;

            var loadedToolbar = Rhino.RhinoApp.ToolbarFiles.FindByPath(toolbarPath);
            if (loadedToolbar is not null && !loadedToolbar.Close(false))
            {
                Rhino.RhinoApp.WriteLine("参数化紧固件工具栏正在使用，无法刷新；请重新启动 Rhino。");
                return;
            }
            Rhino.RhinoApp.ToolbarFiles.Open(toolbarPath);
        }
        catch (Exception ex)
        {
            Rhino.RhinoApp.WriteLine($"参数化紧固件工具列未自动加载：{ex.Message}");
        }
    }
}
