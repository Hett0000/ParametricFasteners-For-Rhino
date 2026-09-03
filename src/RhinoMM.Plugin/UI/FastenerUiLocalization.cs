using System.Runtime.CompilerServices;
using Eto.Forms;
using RhinoMM.Core.Services;

namespace RhinoMM.Plugin.UI;

internal static class FastenerUiLocalization
{
    private sealed class TextState
    {
        public string SourceText { get; set; } = string.Empty;
        public string LastText { get; set; } = string.Empty;
        public string SourceToolTip { get; set; } = string.Empty;
        public string LastToolTip { get; set; } = string.Empty;
        public string SourcePlaceholder { get; set; } = string.Empty;
        public string LastPlaceholder { get; set; } = string.Empty;
    }

    private static readonly ConditionalWeakTable<object, TextState> States = new();

    public static void ApplyTree(Control root)
    {
        ApplyObject(root);
        if (root is GridView grid)
            foreach (var column in grid.Columns)
                ApplyColumn(column);
        if (root is DropDown dropDown)
            foreach (var item in dropDown.Items)
                ApplyListItem(item);
        // List boxes commonly contain user templates, notes or custom definitions.
        // Their values are user data and intentionally remain unchanged.
        foreach (var child in root.VisualControls)
            ApplyTree(child);
    }

    private static void ApplyObject(object value)
    {
        var state = States.GetOrCreateValue(value);
        var type = value.GetType();
        // Never rewrite editable values: template names, notes, project groups and custom
        // definition names are data, not interface copy.
        var isNumericEditor = type.Name is "NumericUpDown" or "NumericStepper";
        if (value is not TextBox && value is not TextArea && !isNumericEditor)
            ApplyProperty(value, type.GetProperty("Text"), state, isToolTip: false, isPlaceholder: false);
        ApplyProperty(value, type.GetProperty("Title"), state, isToolTip: false, isPlaceholder: false);
        ApplyProperty(value, type.GetProperty("ToolTip"), state, isToolTip: true, isPlaceholder: false);
        ApplyProperty(value, type.GetProperty("PlaceholderText"), state, isToolTip: false, isPlaceholder: true);
    }

    private static void ApplyColumn(GridColumn column)
    {
        var state = States.GetOrCreateValue(column);
        var current = column.HeaderText ?? string.Empty;
        if (string.IsNullOrEmpty(state.SourceText) || current != state.LastText)
            state.SourceText = current;
        column.HeaderText = FastenerText.Translate(state.SourceText);
        state.LastText = column.HeaderText;
    }

    private static void ApplyListItem(IListItem item)
    {
        var state = States.GetOrCreateValue(item);
        var current = item.Text ?? string.Empty;
        if (string.IsNullOrEmpty(state.SourceText) || current != state.LastText)
            state.SourceText = current;
        item.Text = FastenerText.Translate(state.SourceText);
        state.LastText = item.Text;
    }

    private static void ApplyProperty(
        object target,
        System.Reflection.PropertyInfo? property,
        TextState state,
        bool isToolTip,
        bool isPlaceholder)
    {
        if (property is null || !property.CanRead || !property.CanWrite || property.PropertyType != typeof(string))
            return;
        var current = (string?)property.GetValue(target) ?? string.Empty;
        var source = isToolTip ? state.SourceToolTip : isPlaceholder ? state.SourcePlaceholder : state.SourceText;
        var last = isToolTip ? state.LastToolTip : isPlaceholder ? state.LastPlaceholder : state.LastText;
        if (string.IsNullOrEmpty(source) || current != last)
            source = current;
        var translated = FastenerText.Translate(source);
        property.SetValue(target, translated);
        if (isToolTip)
        {
            state.SourceToolTip = source;
            state.LastToolTip = translated;
        }
        else if (isPlaceholder)
        {
            state.SourcePlaceholder = source;
            state.LastPlaceholder = translated;
        }
        else
        {
            state.SourceText = source;
            state.LastText = translated;
        }
    }
}
