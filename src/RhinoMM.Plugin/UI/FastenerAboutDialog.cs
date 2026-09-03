using System.Reflection;
using System.Runtime.InteropServices;
using Eto.Drawing;
using Eto.Forms;
using Rhino;
using Rhino.UI;
using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;
using RhinoMM.Plugin;
using RhinoMM.Plugin.Services;

namespace RhinoMM.Plugin.UI;

internal static class FastenerVersionInfo
{
    public static int CommandTypeCount
    {
        get
        {
            try
            {
                return Assembly.GetExecutingAssembly().GetTypes()
                    .Count(type => !type.IsAbstract && typeof(Rhino.Commands.Command).IsAssignableFrom(type));
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Count(type => type is not null
                    && !type.IsAbstract
                    && typeof(Rhino.Commands.Command).IsAssignableFrom(type));
            }
        }
    }

    public static string PluginVersion
    {
        get
        {
            var assembly = Assembly.GetExecutingAssembly();
            var informational = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;
            if (!string.IsNullOrWhiteSpace(informational))
                return informational.Split('+')[0];
            return assembly.GetName().Version?.ToString(3) ?? "未知";
        }
    }

    public static string InstallationType
    {
        get
        {
            var normalized = RhinoMMPlugIn.AssemblyPath.Replace('/', '\\');
            return normalized.Contains("\\McNeel\\Rhinoceros\\packages\\", StringComparison.OrdinalIgnoreCase)
                ? "Yak"
                : "手动/开发";
        }
    }

    public static bool IsHealthy =>
        File.Exists(RhinoMMPlugIn.AssemblyPath)
        && File.Exists(RhinoMMPlugIn.ExpectedToolbarPath)
        && string.Equals(RhinoMMPlugIn.ToolbarHealth, "安装正常", StringComparison.Ordinal);

    public static string Details => FastenerText.Translate(string.Join(Environment.NewLine,
    [
        $"参数化紧固件 v{PluginVersion}",
        $"Rhino {RhinoApp.Version}",
        $"组件 schema v{FastenerComponentData.CurrentSchemaVersion}",
        $"模板 schema v{FastenerTemplateData.CurrentSchemaVersion}",
        $"用户库 schema v{UserFastenerLibraryDocument.CurrentSchemaVersion}",
        $"安装方式：{InstallationType}",
        $"RHP：{RhinoMMPlugIn.AssemblyPath}",
        $"工具栏：{RhinoMMPlugIn.ActiveToolbarPath ?? RhinoMMPlugIn.ExpectedToolbarPath}",
        $"状态：{(IsHealthy ? "安装正常" : "建议使用当前版本离线安装包重新安装")}",
        $"语言模式：{FastenerLocalizationService.ModeLabel()}",
        $"实际语言：{FastenerText.Language}",
        $"Rhino 语言 ID：{FastenerLocalizationService.RhinoLanguageId}",
        $"翻译资源版本：{FastenerText.ResourceVersion}"
    ]));

    public static string InstallationDiagnostics => FastenerText.Translate(string.Join(Environment.NewLine,
    [
        $"参数化紧固件 v{PluginVersion}",
        $"Rhino {RhinoApp.Version}",
        $"运行时：{RuntimeInformation.FrameworkDescription}",
        $"组件 schema v{FastenerComponentData.CurrentSchemaVersion}",
        $"模板 schema v{FastenerTemplateData.CurrentSchemaVersion}",
        $"用户库 schema v{UserFastenerLibraryDocument.CurrentSchemaVersion}",
        $"安装方式：{InstallationType}",
        $"RHP：{SanitizePath(RhinoMMPlugIn.AssemblyPath)}",
        $"工具栏：{SanitizePath(RhinoMMPlugIn.ActiveToolbarPath ?? RhinoMMPlugIn.ExpectedToolbarPath)}",
        $"工具栏状态：{RhinoMMPlugIn.ToolbarHealth}",
        $"发现命令：{CommandTypeCount}",
        $"安装状态：{(IsHealthy ? "安装正常" : "建议使用当前版本离线安装包重新安装")}",
        $"语言模式：{FastenerLocalizationService.ModeLabel()}",
        $"实际语言：{FastenerText.Language}",
        $"Rhino 语言 ID：{FastenerLocalizationService.RhinoLanguageId}",
        $"翻译资源版本：{FastenerText.ResourceVersion}"
    ]));

    private static string SanitizePath(string path)
    {
        foreach (var item in new[]
        {
            (Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "%LOCALAPPDATA%"),
            (Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "%APPDATA%"),
            (Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "%USERPROFILE%")
        })
        {
            if (!string.IsNullOrWhiteSpace(item.Item1)
                && path.StartsWith(item.Item1, StringComparison.OrdinalIgnoreCase))
                return item.Item2 + path[item.Item1.Length..];
        }
        return path;
    }
}

internal sealed class FastenerAboutDialog : Dialog
{
    private readonly Label _status = FastenerUiTheme.SecondaryLabel();

    private FastenerAboutDialog()
    {
        Title = "关于参数化紧固件";
        ClientSize = new Size(560, 360);
        Resizable = false;

        var title = FastenerUiTheme.PrimaryLabel($"参数化紧固件 v{FastenerVersionInfo.PluginVersion}");
        title.Font = new Font(SystemFont.Bold, FastenerUiTheme.TitleFontSize);
        var details = new TextArea
        {
            Text = FastenerVersionInfo.Details,
            ReadOnly = true,
            Wrap = true
        };
        var copy = new Button { Text = "复制安装诊断" };
        var close = new Button { Text = "关闭" };
        FastenerUiTheme.ApplySecondary(copy);
        FastenerUiTheme.ApplyPrimary(close, true);
        copy.Click += (_, _) =>
        {
            Clipboard.Instance.Text = FastenerVersionInfo.InstallationDiagnostics;
            _status.Text = "安装诊断已复制。";
            FastenerUiTheme.SetRole(_status, FastenerThemeRole.StatusSuccess);
        };
        close.Click += (_, _) => Close();
        DefaultButton = close;
        AbortButton = close;

        var summary = new DynamicLayout { Spacing = new Size(0, FastenerUiTheme.SpaceSmall) };
        summary.AddRow(title);
        summary.AddRow(FastenerUiTheme.SecondaryLabel("Rhino 8 参数化紧固件与打印孔工作流"));
        var body = FastenerUiTheme.CreateCard(new DynamicLayout
        {
            Padding = new Padding(FastenerUiTheme.SpaceSmall),
            Spacing = new Size(0, FastenerUiTheme.SpaceSmall),
            Rows = { new DynamicRow(details) }
        });
        Content = FastenerUiTheme.CreateWindowShell(
            summary,
            body,
            _status,
            FastenerUiTheme.CreateActionBar(copy, close));
        FastenerUiTheme.WatchWindow(this);
    }

    public static void Show()
    {
        using var dialog = new FastenerAboutDialog();
        dialog.ShowModal(RhinoEtoApp.MainWindow);
    }
}
