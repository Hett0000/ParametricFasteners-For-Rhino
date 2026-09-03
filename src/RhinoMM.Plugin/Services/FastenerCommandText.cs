using Rhino;
using RhinoMM.Core.Services;

namespace RhinoMM.Plugin.Services;

internal static class FastenerCommandText
{
    public static void WriteLine(string message) => RhinoApp.WriteLine(FastenerText.Translate(message));
    public static string Translate(string message) => FastenerText.Translate(message);
}
