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
        Panels.RegisterPanel(this, typeof(RhinoMMPanel), "RhinoMM 3D 打印紧固件", null);
        return LoadReturnCode.Success;
    }
}
