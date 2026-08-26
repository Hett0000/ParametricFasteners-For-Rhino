using Eto.Drawing;
using Eto.Forms;
using Rhino;
using Rhino.UI;
using RhinoMM.Plugin.Services;
using RhinoMM.Plugin.Persistence;

namespace RhinoMM.Plugin.UI;

internal sealed class MaintenanceCenterDialog : Dialog
{
    private readonly RhinoDoc _doc;
    private readonly FastenerOperationStatusView _status = new();

    private MaintenanceCenterDialog(RhinoDoc doc)
    {
        _doc = doc;
        Title = "参数化紧固件维护中心";
        ClientSize = new Size(460, 330);
        MinimumSize = new Size(400, 300);
        Resizable = true;
        var refresh = Action("一键修复", "只重建可确定恢复的组件；不会猜测宿主、删除对象或处理损坏数据。");
        var relink = Action("重新绑定…", "列出候选宿主、轴线区间与评分原因，由用户逐项确认。");
        var cleanup = Action("清理残留", "只删除缺少实际控制点的插件残留，不删除普通宿主。");
        var release = Action("解除咬合确认", "清除所选组件固定的咬合宿主，并使用最新几何重新自动识别。");
        var inspect = new Button { Text = "打开装配检查器", Width = 128 };
        var close = new Button { Text = "关闭", Width = FastenerUiTheme.DialogButtonWidth };
        refresh.Button.Click += (_, _) => Run(ComponentMaintenanceService.QuickRefresh);
        cleanup.Button.Click += (_, _) => Run(ComponentMaintenanceService.CleanupResiduals);
        release.Button.Click += (_, _) => Run(ReleaseConfirmedEngagementHosts);
        relink.Button.Click += (_, _) => RelinkWizardDialog.Show(_doc);
        inspect.Click += (_, _) => AssemblyInspectorDialog.Show(_doc);
        close.Click += (_, _) => Close();
        var header = FastenerUiTheme.CreateCard(new DynamicLayout
        {
            Spacing = new Size(0, 2),
            Rows =
            {
                new DynamicRow(FastenerUiTheme.SectionTitle("维护动作")),
                new DynamicRow(FastenerUiTheme.SecondaryLabel("刷新、重绑和清理相互独立；只有确认后的动作才修改模型。"))
            }
        });
        var actions = new DynamicLayout { Spacing = new Size(0, FastenerUiTheme.SpaceSmall) };
        actions.AddRow(refresh.Card);
        actions.AddRow(relink.Card);
        actions.AddRow(cleanup.Card);
        actions.AddRow(release.Card);
        Content = FastenerUiTheme.CreateWindowShell(
            header,
            actions,
            _status,
            FastenerUiTheme.CreateActionBar(inspect, close));
        FastenerUiTheme.ApplySecondary(inspect);
        FastenerUiTheme.ApplySecondary(close);
        FastenerUiTheme.WatchWindow(this);
        AbortButton = close;
    }

    public static void Show(RhinoDoc doc)
    {
        using var dialog = new MaintenanceCenterDialog(doc);
        dialog.ShowModal(RhinoEtoApp.MainWindow);
    }

    private delegate bool MaintenanceAction(RhinoDoc doc, out string message);

    private static bool ReleaseConfirmedEngagementHosts(RhinoDoc doc, out string message)
    {
        var componentIds = doc.Objects.GetSelectedObjects(false, false)
            .Select(obj => obj.Attributes.GetUserString(ComponentRepository.ComponentIdKey))
            .Where(text => Guid.TryParse(text, out _))
            .Select(Guid.Parse)
            .Distinct()
            .ToArray();
        var drafts = componentIds
            .Select(id => ComponentRepository.TryReadComponent(doc, id, out var component)
                ? component
                : null)
            .Where(component => component is not null)
            .Cast<RhinoMM.Core.Domain.FastenerComponentData>()
            .Where(component => component.ConfirmedEngagementHostId != Guid.Empty)
            .Select(component => component with
            {
                ConfirmedEngagementHostId = Guid.Empty,
                AutoRecognizeHosts = true,
                UpdatedAt = DateTimeOffset.UtcNow
            })
            .ToArray();
        if (drafts.Length == 0)
        {
            message = "当前选择中没有固定咬合宿主的组件。";
            return false;
        }
        return ComponentUpdateCoordinator.TryApplyDrafts(doc, drafts, out _, out message);
    }
    private void Run(MaintenanceAction action)
    {
        _status.Set(new FastenerOperationProgress(FastenerOperationState.Preparing, "准备维护组件"));
        var success = action(_doc, out var message);
        _status.Set(new FastenerOperationProgress(
            success ? FastenerOperationState.Success : FastenerOperationState.Failure,
            message));
        RhinoApp.WriteLine(message);
    }

    private static (Button Button, Panel Card) Action(string title, string description)
    {
        var button = new Button { Text = title, Width = 102 };
        FastenerUiTheme.ApplySecondary(button);
        var card = FastenerUiTheme.CreateCard(new TableLayout
        {
            Spacing = new Size(FastenerUiTheme.SpaceMedium, 0),
            Rows =
            {
                new TableRow(button, new TableCell(new Label
                {
                    Text = description,
                    Wrap = WrapMode.Word,
                    VerticalAlignment = VerticalAlignment.Center,
                    ToolTip = description
                }, true))
            }
        }, FastenerUiTheme.SpaceSmall);
        return (button, card);
    }
}

internal sealed class RelinkWizardDialog : Dialog
{
    private sealed record RequestItem(int Index, RelinkRequest Request)
    {
        public override string ToString() => $"{Request.Component.Size} · {Request.Binding.Role} · {Request.Binding.BindingId.ToString("N")[..8]}";
    }

    private readonly RhinoDoc _doc;
    private readonly IReadOnlyList<RelinkRequest> _requests;
    private readonly Dictionary<Guid, Guid> _targets = [];
    private readonly ListBox _requestList = new();
    private readonly DropDown _candidate = new();
    private readonly Label _reason = FastenerUiTheme.SecondaryLabel("选择一个待绑定项。候选不会自动应用。");
    private readonly FastenerOperationStatusView _status = new();
    private int _currentIndex = -1;

    private RelinkWizardDialog(RhinoDoc doc, IReadOnlyList<RelinkRequest> requests)
    {
        _doc = doc;
        _requests = requests;
        Title = "显式重新绑定";
        ClientSize = new Size(680, 430);
        MinimumSize = new Size(520, 340);
        Resizable = true;
        _requestList.DataStore = requests.Select((request, index) => new RequestItem(index, request)).ToArray();
        _requestList.SelectedIndexChanged += (_, _) => LoadRequest();
        _candidate.SelectedIndexChanged += (_, _) => CandidateChanged();
        var apply = new Button { Text = "确认绑定", Width = 96 };
        var cancel = new Button { Text = "取消", Width = FastenerUiTheme.DialogButtonWidth };
        apply.Click += (_, _) => Apply();
        cancel.Click += (_, _) => Close();
        var header = FastenerUiTheme.CreateCard(new DynamicLayout
        {
            Spacing = new Size(0, 2),
            Rows =
            {
                new DynamicRow(FastenerUiTheme.SectionTitle($"待重新绑定 {_requests.Count} 项")),
                new DynamicRow(FastenerUiTheme.SecondaryLabel("先选择绑定项，再核对候选及评分原因；未选择的项保持不变。"))
            }
        });
        var candidateCard = FastenerUiTheme.CreateCard(new DynamicLayout
        {
            Spacing = new Size(0, FastenerUiTheme.SpaceSmall),
            Rows =
            {
                new DynamicRow(FastenerUiTheme.SectionTitle("候选宿主")),
                new DynamicRow(_candidate),
                new DynamicRow(_reason)
            }
        });
        var body = new TableLayout
        {
            Spacing = new Size(FastenerUiTheme.SpaceMedium, 0),
            Rows = { new TableRow(new TableCell(_requestList, true), new TableCell(candidateCard, true)) { ScaleHeight = true } }
        };
        Content = FastenerUiTheme.CreateWindowShell(
            header,
            body,
            _status,
            FastenerUiTheme.CreateActionBar(cancel, apply));
        FastenerUiTheme.ApplyPrimary(apply, true);
        FastenerUiTheme.ApplySecondary(cancel);
        FastenerUiTheme.WatchWindow(this);
        DefaultButton = apply;
        AbortButton = cancel;
        if (_requests.Count > 0) _requestList.SelectedIndex = 0;
    }

    public static void Show(RhinoDoc doc)
    {
        var run = FastenerProgressWindow.Run(
            "重新绑定",
            "正在分析候选宿主",
            operation => ComponentMaintenanceService.BuildRelinkRequests(doc, operation));
        if (run.Cancelled)
        {
            RhinoApp.WriteLine("重新绑定候选分析已取消；模型未修改。");
            return;
        }
        var requests = run.Result;
        if (requests.Count == 0)
        {
            RhinoApp.WriteLine("当前选择或文档中没有待重新绑定的组件。");
            return;
        }
        using var dialog = new RelinkWizardDialog(doc, requests);
        dialog.ShowModal(RhinoEtoApp.MainWindow);
    }

    private void LoadRequest()
    {
        SaveChoice();
        if (_requestList.SelectedValue is not RequestItem item)
            return;
        _currentIndex = item.Index;
        _candidate.Items.Clear();
        _candidate.Items.Add(new ListItem { Key = string.Empty, Text = "不修改" });
        foreach (var candidate in item.Request.Candidates)
            _candidate.Items.Add(new ListItem { Key = candidate.ObjectId.ToString("D"), Text = candidate.Name });
        _candidate.SelectedKey = _targets.TryGetValue(item.Request.Binding.BindingId, out var selected)
            ? selected.ToString("D")
            : string.Empty;
        _candidate.SelectedIndex = Math.Max(0, _candidate.SelectedIndex);
        CandidateChanged();
    }

    private void SaveChoice()
    {
        if (_currentIndex < 0 || _currentIndex >= _requests.Count)
            return;
        var binding = _requests[_currentIndex].Binding.BindingId;
        if (Guid.TryParse(_candidate.SelectedKey, out var target)) _targets[binding] = target;
        else _targets.Remove(binding);
    }

    private void CandidateChanged()
    {
        if (_currentIndex < 0 || _currentIndex >= _requests.Count)
            return;
        var candidate = _requests[_currentIndex].Candidates.FirstOrDefault(item => item.ObjectId.ToString("D") == _candidate.SelectedKey);
        _reason.Text = candidate?.Reason ?? "该绑定保持不变。";
        _reason.ToolTip = _reason.Text;
    }

    private void Apply()
    {
        SaveChoice();
        _status.Set(new FastenerOperationProgress(FastenerOperationState.Preparing, "准备提交重新绑定"));
        if (!ComponentMaintenanceService.CommitRelinks(_doc, _targets, out var message))
        {
            _status.Set(new FastenerOperationProgress(FastenerOperationState.Failure, message));
            RhinoApp.WriteLine(message);
            return;
        }
        _status.Set(new FastenerOperationProgress(FastenerOperationState.Success, message));
        RhinoApp.WriteLine(message);
        Close();
    }
}
