using Content.Client.Stylesheets;
using Content.Shared._Utopia.UserInterface.Components;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._Utopia.UserInterface.Systems;

public sealed class DyeUISystem : EntitySystem
{
    private const string DyedTabsClass = "DyedUiTabs";
    private static readonly string[] BuiSuffixes = { "BoundUserInterface", "Bui" };
    private static readonly HashSet<string> SelfPaintedTypes = new()
    {
        "ButtonListPanel",
        "Slider",
        "HSlider",
        "VSlider"
    };
    private static readonly Dictionary<Type, string> BuiBaseNameCache = new();
    private static readonly Dictionary<Type, string> WindowBaseNameCache = new();


    private readonly record struct ThemeContext(bool InsideButton, bool InsideHeader);
    private readonly record struct UiId(EntityUid Uid, Enum Key);
    private readonly record struct TabSheetKey(Stylesheet Sheet, Color Active, Color Inactive, Color Border, int Thickness);


    [Dependency] private IUserInterfaceManager _uiManager = default!;
    private readonly Dictionary<UiId, BaseWindow> _bindings = new();
    private readonly HashSet<BaseWindow> _claimed = new();
    private readonly HashSet<BaseWindow> _knownWindows = new();
    private readonly List<BaseWindow> _windows = new();
    private readonly HashSet<UiId> _active = new();
    private readonly List<UiId> _stale = new();

    private readonly Dictionary<TabSheetKey, Stylesheet> _tabSheets = new();
    private readonly Dictionary<TabContainer, TabSheetKey> _tabApplied = new();
    private readonly List<TabContainer> _tabStale = new();
    private Stylesheet? _lastGlobalSheet;

    public override void FrameUpdate(float frameTime)
    {
        CollectWindows();
        _active.Clear();

        if (!ReferenceEquals(_lastGlobalSheet, _uiManager.Stylesheet))
        {
            _tabSheets.Clear();
            _lastGlobalSheet = _uiManager.Stylesheet;
        }

        var query = EntityQueryEnumerator<UserInterfaceComponent>();
        while (query.MoveNext(out var uid, out var ui))
        {
            if (ui.ClientOpenInterfaces.Count == 0)
                continue;

            TryComp<DyeUiComponent>(uid, out var theme);

            foreach (var (key, bui) in ui.ClientOpenInterfaces)
            {
                var id = new UiId(uid, key);
                _active.Add(id);

                var window = ResolveWindow(id, bui);
                if (window == null || theme == null)
                    continue;

                var entry = theme.Themes.GetValueOrDefault(key) ?? theme.Default;
                if (entry == null)
                    continue;

                ApplyTheme(window, entry, default);
            }
        }

        ReleaseStaleBindings();
        ReleaseStaleTabs();

        _knownWindows.Clear();
        _knownWindows.UnionWith(_windows);
    }

    private void CollectWindows()
    {
        _windows.Clear();

        try
        {
            foreach (var child in _uiManager.WindowRoot.Children)
            {
                if (child is BaseWindow { Disposed: false } window)
                    _windows.Add(window);
            }
        }
        catch (InvalidOperationException)
        {

        }
    }

    #region BUI

    private BaseWindow? ResolveWindow(UiId id, BoundUserInterface bui)
    {
        if (_bindings.TryGetValue(id, out var bound))
        {
            if (!bound.Disposed && bound.Parent != null)
                return bound;

            _bindings.Remove(id);
            _claimed.Remove(bound);
        }

        var buiBaseName = GetBuiBaseName(bui);
        var found = FindFreeWindow(buiBaseName, onlyNew: true)
                    ?? FindFreeWindow(buiBaseName, onlyNew: false);

        if (found == null)
            return null;

        _bindings[id] = found;
        _claimed.Add(found);
        return found;
    }

    private BaseWindow? FindFreeWindow(string buiBaseName, bool onlyNew)
    {
        foreach (var window in _windows)
        {
            if (_claimed.Contains(window))
                continue;

            if (onlyNew && _knownWindows.Contains(window))
                continue;

            if (NameMatches(buiBaseName, window.GetType()))
                return window;
        }

        return null;
    }

    private void ReleaseStaleBindings()
    {
        if (_bindings.Count == 0)
            return;

        _stale.Clear();
        foreach (var id in _bindings.Keys)
        {
            if (!_active.Contains(id))
                _stale.Add(id);
        }

        foreach (var id in _stale)
        {
            if (_bindings.Remove(id, out var win))
                _claimed.Remove(win);
        }
    }

    private static string GetBuiBaseName(BoundUserInterface bui)
    {
        var type = bui.GetType();
        if (BuiBaseNameCache.TryGetValue(type, out var cachedName))
            return cachedName;

        var name = type.Name;
        foreach (var suffix in BuiSuffixes)
        {
            if (name.Length > suffix.Length && name.EndsWith(suffix, StringComparison.Ordinal))
            {
                name = name.Substring(0, name.Length - suffix.Length);
                break;
            }
        }

        BuiBaseNameCache[type] = name;
        return name;
    }

    private static string GetWindowBaseName(Type windowType)
    {
        if (WindowBaseNameCache.TryGetValue(windowType, out var cachedName))
            return cachedName;

        var name = windowType.Name.Replace("Window", "");
        WindowBaseNameCache[windowType] = name;
        return name;
    }

    private static bool NameMatches(string buiBaseName, Type windowType)
    {
        if (string.IsNullOrEmpty(buiBaseName))
            return false;

        var windowTypeName = windowType.Name;
        var windowBaseName = GetWindowBaseName(windowType);

        if (string.IsNullOrEmpty(windowBaseName))
            return false;

        return windowTypeName.StartsWith(buiBaseName, StringComparison.Ordinal)
            || buiBaseName.StartsWith(windowBaseName, StringComparison.Ordinal);
    }

    #endregion

    #region Getting Elements

    private void ApplyTheme(Control control, DyedUiEntry entry, ThemeContext ctx)
    {
        if (IsIgnored(control, entry))
            return;

        var typeName = control.GetType().Name;

        if (typeName.Contains("Separator"))
        {
            if (entry.SeparatorColor is { } separatorColor && control.Modulate != separatorColor)
                control.Modulate = separatorColor;

            return;
        }

        if (typeName == "StripeBack" && entry.StripeColor is { } stripeColor && control.ModulateSelfOverride != stripeColor)
            control.ModulateSelfOverride = stripeColor;

        switch (control)
        {
            case PanelContainer panel:
                ApplyPanel(panel, entry, ctx);
                break;

            case TabContainer tabs:
                ApplyTabContainer(tabs, entry);
                break;

            case BaseButton button:
                ApplyButton(button, entry);
                break;

            case LineEdit lineEdit:
                ApplyLineEdit(lineEdit, entry);
                break;

            case ProgressBar progressBar:
                ApplyProgressBar(progressBar, entry);
                break;

            case Label label:
                ApplyLabel(label, entry, ctx);
                break;
        }

        var childCtx = ctx with
        {
            InsideButton = ctx.InsideButton || control is BaseButton,
            InsideHeader = ctx.InsideHeader || IsHeader(control)
        };

        try
        {
            foreach (var child in control.Children)
                ApplyTheme(child, entry, childCtx);
        }
        catch (InvalidOperationException)
        {
            // Безопасная обработка изменений структуры дочерних элементов
        }
    }

    private static bool IsIgnored(Control control, DyedUiEntry entry)
    {
        var typeName = control.GetType().Name;
        if (SelfPaintedTypes.Contains(typeName) || typeName.Contains("Slider") || entry.IgnoreTypes.Contains(typeName))
            return true;

        if (!string.IsNullOrEmpty(control.Name) && entry.IgnoreNames.Contains(control.Name))
            return true;

        foreach (var styleClass in entry.IgnoreStyleClasses)
        {
            if (control.HasStyleClass(styleClass))
                return true;
        }

        return false;
    }

    #endregion

    #region Apply

    private static void ApplyPanel(PanelContainer panel, DyedUiEntry entry, ThemeContext ctx)
    {
        if (ctx.InsideButton && !entry.PaintPanelsInsideButtons)
            return;

        if (panel.ModulateSelfOverride != null)
            return;

        var color = IsHeader(panel)
            ? entry.HeaderColor ?? entry.BackgroundColor
            : entry.BackgroundColor;

        if (color is not { } target)
            return;

        SetStyle(panel, target, GetBorder(entry), entry.BorderThickness);
    }

    private static void ApplyButton(BaseButton button, DyedUiEntry entry)
    {
        Color? color = button.DrawMode switch
        {
            BaseButton.DrawModeEnum.Disabled => entry.ButtonDisabledColor ?? entry.ButtonColor,
            BaseButton.DrawModeEnum.Pressed => entry.ButtonPressedColor ?? entry.ButtonColor,
            BaseButton.DrawModeEnum.Hover => entry.ButtonHoverColor ?? entry.ButtonColor,
            _ => entry.ButtonColor
        };

        if (button.HasStyleClass(StyleClass.Positive))
            color = entry.ButtonSelectedColor;

        if (color is not { } target)
        {
            if (GetOverride(button) is StyleBoxFlat flat && IsThemeButtonColor(flat.BackgroundColor, entry))
                SetOverride(button, null);

            return;
        }

        var border = entry.ButtonBorderColor ?? GetBorder(entry);
        SetStyle(button, target, border, entry.BorderThickness);
    }

    private static void ApplyProgressBar(ProgressBar bar, DyedUiEntry entry)
    {
        if (entry.ProgressBarFgColor is { } fg && !IsFlatColor(bar.ForegroundStyleBoxOverride, fg))
            bar.ForegroundStyleBoxOverride = new StyleBoxFlat { BackgroundColor = fg };

        if (entry.ProgressBarBgColor is { } bg && !IsFlatColor(bar.BackgroundStyleBoxOverride, bg))
            bar.BackgroundStyleBoxOverride = new StyleBoxFlat { BackgroundColor = bg };
    }

    private static void ApplyLabel(Label label, DyedUiEntry entry, ThemeContext ctx)
    {
        var color = entry.TextColor;

        if (ctx.InsideButton && entry.ButtonTextColor != null)
            color = entry.ButtonTextColor;

        if ((ctx.InsideHeader || IsHeader(label)) && entry.HeaderTextColor != null)
            color = entry.HeaderTextColor;

        if (color is not { } target)
            return;

        if (label.FontColorOverride == null)
            label.FontColorOverride = target;
    }

    private void ApplyTabContainer(TabContainer tabs, DyedUiEntry entry)
    {
        var panelColor = entry.TabPanelColor ?? entry.BackgroundColor;
        var border = GetBorder(entry);
        var thickness = entry.BorderThickness;

        if (panelColor is { } panel && !IsSame(tabs.PanelStyleBoxOverride, panel, border, thickness))
        {
            tabs.PanelStyleBoxOverride = new StyleBoxFlat
            {
                BackgroundColor = panel,
                BorderColor = border,
                BorderThickness = new Thickness(thickness)
            };
        }

        var activeText = entry.TabTextColor ?? entry.TextColor;
        if (activeText != null && tabs.TabFontColorOverride != activeText)
            tabs.TabFontColorOverride = activeText;

        var inactiveText = entry.TabInactiveTextColor;
        if (inactiveText != null && tabs.TabFontColorInactiveOverride != inactiveText)
            tabs.TabFontColorInactiveOverride = inactiveText;

        ApplyTabHeaders(tabs, entry, panelColor, border, thickness);
    }

    private void ApplyTabHeaders(TabContainer tabs, DyedUiEntry entry, Color? panelColor, Color border, int thickness)
    {
        var active = entry.TabActiveColor ?? panelColor;
        var inactive = entry.TabInactiveColor;

        if (active == null && inactive == null)
            return;

        Color activeColor = active ?? inactive!.Value;
        Color inactiveColor = inactive ?? Darken(activeColor);

        var baseSheet = GetBaseSheet(tabs);
        if (baseSheet == null)
            return;

        var key = new TabSheetKey(baseSheet, activeColor, inactiveColor, border, thickness);
        if (_tabApplied.TryGetValue(tabs, out var applied) && applied == key)
            return;

        if (!_tabSheets.TryGetValue(key, out var sheet))
        {
            var rules = new List<StyleRule>(baseSheet.Rules.Count + 1);
            rules.AddRange(baseSheet.Rules);

            rules.Add(new StyleRule(
                new SelectorElement(typeof(TabContainer), new[] { DyedTabsClass }, null, null),
                new[]
                {
                    new StyleProperty(TabContainer.StylePropertyTabStyleBox, MakeTabBox(activeColor, border, thickness)),
                    new StyleProperty(TabContainer.StylePropertyTabStyleBoxInactive, MakeTabBox(inactiveColor, border, thickness))
                }));

            sheet = new Stylesheet(rules);
            _tabSheets[key] = sheet;
        }

        if (!tabs.HasStyleClass(DyedTabsClass))
            tabs.AddStyleClass(DyedTabsClass);

        tabs.Stylesheet = sheet;
        _tabApplied[tabs] = key;
    }

    private void ApplyLineEdit(LineEdit lineEdit, DyedUiEntry entry)
    {
        if (entry.LineEditColor is { } lineEditColor)
            SetStyle(lineEdit, lineEditColor, GetBorder(entry), entry.BorderThickness);

        var textColor = entry.LineEditTextColor ?? entry.TextColor;
        if (textColor is { } color)
        {
            if (!lineEdit.TryGetStyleProperty("font-color", out Color currentColor) || currentColor != color)
            {
                var baseSheet = GetBaseSheet(lineEdit);
                if (baseSheet == null)
                    return;

                var rules = new List<StyleRule>(baseSheet.Rules.Count + 2);
                rules.AddRange(baseSheet.Rules);
                rules.Add(new StyleRule(
                    new SelectorElement(typeof(LineEdit), null, null, null),
                    new[]
                    {
                        new StyleProperty("font-color", color)
                    }));

                var placeholderColor = new Color(color.R, color.G, color.B, color.A * entry.Alpha);
                rules.Add(new StyleRule(
                    new SelectorElement(typeof(LineEdit), null, null, new[] { LineEdit.StylePseudoClassPlaceholder }),
                    new[]
                    {
                        new StyleProperty("font-color", placeholderColor)
                    }));

                lineEdit.Stylesheet = new Stylesheet(rules);
            }
        }
    }
    #endregion

    #region Helpers

    private static bool IsHeader(Control control)
        => control.HasStyleClass("windowHeader") || control.HasStyleClass("windowTitle");

    private static Color GetBorder(DyedUiEntry entry)
        => entry.BorderColor ?? Color.Transparent;

    private static Color Darken(Color color)
        => new Color(color.R * 0.6f, color.G * 0.6f, color.B * 0.6f, color.A);

    private static bool IsSame(StyleBox? box, Color bg, Color border, int thickness)
        => box is StyleBoxFlat flat && flat.BackgroundColor == bg && flat.BorderColor == border && Math.Abs(flat.BorderThickness.Bottom - thickness) < 0.1f;

    private static bool IsFlatColor(StyleBox? box, Color color)
        => box is StyleBoxFlat flat && flat.BackgroundColor == color;

    private static StyleBox? GetOverride(Control control)
    {
        return control switch
        {
            PanelContainer p => p.PanelOverride,
            Button b => b.StyleBoxOverride,
            ContainerButton cb => cb.StyleBoxOverride,
            LineEdit l => l.StyleBoxOverride,
            _ => null
        };
    }

    private static void SetOverride(Control control, StyleBox? box)
    {
        switch (control)
        {
            case PanelContainer p:
                p.PanelOverride = box;
                break;
            case Button b:
                b.StyleBoxOverride = box;
                break;
            case ContainerButton cb:
                cb.StyleBoxOverride = box;
                break;
            case LineEdit l:
                l.StyleBoxOverride = box;
                break;
        }
    }

    private static bool IsThemeButtonColor(Color color, DyedUiEntry entry)
    {
        return color == entry.ButtonColor
               || color == entry.ButtonHoverColor
               || color == entry.ButtonPressedColor
               || color == entry.ButtonDisabledColor
               || color == entry.ButtonSelectedColor;
    }

    private Stylesheet? GetBaseSheet(Control control)
    {
        for (var parent = control.Parent; parent != null; parent = parent.Parent)
        {
            if (parent.Stylesheet != null)
                return parent.Stylesheet;
        }

        return _uiManager.Stylesheet;
    }

    private static StyleBoxFlat MakeTabBox(Color bg, Color border, int thickness)
    {
        return new StyleBoxFlat
        {
            BackgroundColor = bg,
            BorderColor = border,
            BorderThickness = new Thickness(thickness),
            ContentMarginLeftOverride = 8,
            ContentMarginRightOverride = 8,
            ContentMarginTopOverride = 3,
            ContentMarginBottomOverride = 3
        };
    }

    private void ReleaseStaleTabs()
    {
        if (_tabApplied.Count == 0)
            return;

        _tabStale.Clear();
        foreach (var tabs in _tabApplied.Keys)
        {
            if (tabs.Disposed)
                _tabStale.Add(tabs);
        }

        foreach (var tabs in _tabStale)
            _tabApplied.Remove(tabs);
    }

    private static void SetStyle(Control control, Color bg, Color border, int thickness)
    {
        if (IsSame(GetOverride(control), bg, border, thickness))
            return;

        SetOverride(control, new StyleBoxFlat
        {
            BackgroundColor = bg,
            BorderColor = border,
            BorderThickness = new Thickness(thickness)
        });
    }

    #endregion
}
