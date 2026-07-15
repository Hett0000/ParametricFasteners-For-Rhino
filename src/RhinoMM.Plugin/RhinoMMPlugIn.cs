using Rhino.PlugIns;
using Rhino.UI;
using RhinoMM.Core.Services;
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
            Panels.RegisterPanel(this, typeof(RhinoMMPanel), "参数化紧固件", null);
            return LoadReturnCode.Success;
        }
        catch (Exception ex)
        {
            errorMessage = $"参数化紧固件初始化失败：{ex}";
            Rhino.RhinoApp.WriteLine(errorMessage);
            return LoadReturnCode.ErrorShowDialog;
        }
    }
}
