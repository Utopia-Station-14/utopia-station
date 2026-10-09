using System;
using System.Collections.Generic;
using Content.Shared._Utopia.UserInterface.Components;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Content.Client._Utopia.UserInterface.Systems;

public sealed class UiThemeSystem : EntitySystem
{
    [Dependency] private IUserInterfaceManager _uiManager = default!;
    [Dependency] private IConsoleHost _consoleHost = default!;

    private static readonly string[] BuiSuffixes = { "BoundUserInterface", "Bui" };


    private static readonly HashSet<string> SelfPaintedTypes = new()
    {
        "ButtonListPanel",
        "Slider",
        "HSlider",
        "VSlider"
    };

    private readonly record struct ThemeContext(bool InsideButton, bool InsideHeader);
    private readonly record struct UiId(EntityUid Uid, Enum Key);
    private readonly Dictionary<UiId, BaseWindow> _bindings = new();
    private readonly HashSet<BaseWindow> _claimed = new();
    private readonly HashSet<BaseWindow> _knownWindows = new();
    private readonly List<BaseWindow> _windows = new();
    private readonly HashSet<UiId> _active = new();
    private readonly List<UiId> _stale = new();
    private const string ThemedTabsClass = "UiThemeTabs";

    private readonly record struct TabSheetKey(Stylesheet Sheet, Color Active, Color Inactive, Color Border, int Thickness);
    private readonly Dictionary<TabSheetKey, Stylesheet> _tabSheets = new();
    private readonly Dictionary<TabContainer, TabSheetKey> _tabApplied = new();
    private readonly List<TabContainer> _tabStale = new();
    private Stylesheet? _lastGlobalSheet;

    public override void Initialize()
    {
        base.Initialize();
    }

    public override void Shutdown()
    {
        base.Shutdown();
    }

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

            TryComp<UiThemeComponent>(uid, out var theme);

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

                ApplyThemeRecursively(window, entry, default);
            }
        }

        ReleaseStaleBindings();
        ReleaseStaleTabs();

        _knownWindows.Clear();
        foreach (var window in _windows)
        {
            _knownWindows.Add(window);
        }
    }

    private static bool IsHeader(Control control)
        => control.HasStyleClass("windowHeader") || control.HasStyleClass("windowTitle");

    private static Color GetBorder(UiThemeEntry entry)
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

    private static string HandlerName(Control control)
    {
        return control switch
        {
            PanelContainer => "panel",
            TabContainer => "tabs",
            Button => "button",
            ContainerButton => "container-button",
            LineEdit => "lineedit",
            ProgressBar => "progress",
            _ => "-"
        };
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

            if (NameMatches(buiBaseName, window.GetType().Name))
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
        var name = bui.GetType().Name;
        foreach (var suffix in BuiSuffixes)
        {
            if (name.Length > suffix.Length && name.EndsWith(suffix, StringComparison.Ordinal))
                return name.Substring(0, name.Length - suffix.Length);
        }

        return name;
    }

    private static bool NameMatches(string buiBaseName, string windowTypeName)
    {
        var windowBaseName = windowTypeName.Replace("Window", "");
        if (buiBaseName.Length == 0 || windowBaseName.Length == 0)
            return false;

        return windowTypeName.StartsWith(buiBaseName, StringComparison.Ordinal)
            || buiBaseName.StartsWith(windowBaseName, StringComparison.Ordinal);
    }

    private void ApplyThemeRecursively(Control control, UiThemeEntry entry, ThemeContext ctx)
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

            case Button or ContainerButton:
                ApplyButton((BaseButton) control, entry);
                break;

            case LineEdit lineEdit:
                if (entry.LineEditColor is { } lineEditColor)
                    SetStyle(lineEdit, lineEditColor, GetBorder(entry), entry.BorderThickness);
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
                ApplyThemeRecursively(child, entry, childCtx);
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static bool IsIgnored(Control control, UiThemeEntry entry)
    {
        var typeName = control.GetType().Name;
        if (SelfPaintedTypes.Contains(typeName) || typeName.Contains("Slider") || entry.IgnoreTypes.Contains(typeName))
            return true;

        if (control.Name != null && entry.IgnoreNames.Contains(control.Name))
            return true;

        foreach (var styleClass in entry.IgnoreStyleClasses)
            if (control.HasStyleClass(styleClass))
                return true;

        return false;
    }

    private static void ApplyPanel(PanelContainer panel, UiThemeEntry entry, ThemeContext ctx)
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

    private static void ApplyButton(BaseButton button, UiThemeEntry entry)
    {
        Color? color = button.DrawMode switch
        {
            BaseButton.DrawModeEnum.Disabled => entry.ButtonDisabledColor ?? entry.ButtonColor,
            BaseButton.DrawModeEnum.Pressed => entry.ButtonPressedColor ?? entry.ButtonColor,
            BaseButton.DrawModeEnum.Hover => entry.ButtonHoverColor ?? entry.ButtonColor,
            _ => entry.ButtonColor
        };

        if (button.HasStyleClass(Content.Client.Stylesheets.StyleClass.Positive))
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

    private static bool IsThemeButtonColor(Color color, UiThemeEntry entry)
    {
        return color == entry.ButtonColor
               || color == entry.ButtonHoverColor
               || color == entry.ButtonPressedColor
               || color == entry.ButtonDisabledColor
               || color == entry.ButtonSelectedColor;
    }

    private static void ApplyProgressBar(ProgressBar bar, UiThemeEntry entry)
    {
        if (entry.ProgressBarFgColor is { } fg && !IsFlatColor(bar.ForegroundStyleBoxOverride, fg))
            bar.ForegroundStyleBoxOverride = new StyleBoxFlat { BackgroundColor = fg };

        if (entry.ProgressBarBgColor is { } bg && !IsFlatColor(bar.BackgroundStyleBoxOverride, bg))
            bar.BackgroundStyleBoxOverride = new StyleBoxFlat { BackgroundColor = bg };
    }

    private static void ApplyLabel(Label label, UiThemeEntry entry, ThemeContext ctx)
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

    private void ApplyTabContainer(TabContainer tabs, UiThemeEntry entry)
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

    private void ApplyTabHeaders(TabContainer tabs, UiThemeEntry entry, Color? panelColor, Color border, int thickness)
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
                new SelectorElement(typeof(TabContainer), new[] { ThemedTabsClass }, null, null),
                new[]
                {
                    new StyleProperty(TabContainer.StylePropertyTabStyleBox, MakeTabBox(activeColor, border, thickness)),
                    new StyleProperty(TabContainer.StylePropertyTabStyleBoxInactive, MakeTabBox(inactiveColor, border, thickness))
                }));

            sheet = new Stylesheet(rules);
            _tabSheets[key] = sheet;
        }

        if (!tabs.HasStyleClass(ThemedTabsClass))
            tabs.AddStyleClass(ThemedTabsClass);

        tabs.Stylesheet = sheet;
        _tabApplied[tabs] = key;
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
}
