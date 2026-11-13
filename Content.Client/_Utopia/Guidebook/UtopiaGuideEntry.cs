using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Utopia.Guidebook;

public abstract class UtopiaGuideEntry : BoxContainer
{
    private const float BodyIndent = 10f;

    private static readonly Color HeaderColor = Color.FromHex("#2B2B33");
    private static readonly Color AccentColor = Color.FromHex("#9A8547");
    private static readonly Color NoteColor = Color.FromHex("#8E8E92");

    private readonly PanelContainer _header;
    private readonly BoxContainer _row;

    protected UtopiaGuideEntry()
    {
        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;
        Margin = new Thickness(0, 4, 0, 12);

        _row = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            VerticalAlignment = VAlignment.Center,
        };

        _header = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = HeaderColor,
                BorderColor = AccentColor,
                BorderThickness = new Thickness(2, 0, 0, 0),
                ContentMarginLeftOverride = 6,
                ContentMarginRightOverride = 6,
                ContentMarginTopOverride = 3,
                ContentMarginBottomOverride = 3,
            },
            HorizontalExpand = true,
            Children = { _row },
        };

        AddChild(_header);
    }

    protected void AddIcon(Texture texture, string? tooltip = null)
    {
        _row.AddChild(new TextureRect
        {
            Texture = texture,
            TextureScale = Vector2.One,
            Stretch = TextureRect.StretchMode.KeepCentered,
            VerticalAlignment = VAlignment.Center,
            Margin = new Thickness(0, 0, 2, 0),
            ToolTip = tooltip,
        });
    }

    protected void AddTitle(string title, string? note = null)
    {
        if (string.IsNullOrEmpty(title))
            return;

        var display = CapitalizeFirst(title);

        _row.AddChild(new Label
        {
            Text = display,
            StyleClasses = { "LabelKeyText" },
            Margin = new Thickness(6, 0, 0, 0),
            VerticalAlignment = VAlignment.Center,
        });

        if (string.IsNullOrWhiteSpace(note))
            return;

        _row.AddChild(new Label
        {
            Text = note,
            FontColorOverride = NoteColor,
            Margin = new Thickness(6, 0, 0, 0),
            VerticalAlignment = VAlignment.Center,
        });
    }

    private static string CapitalizeFirst(string s)
    {
        if (string.IsNullOrEmpty(s))
            return s;

        var chars = s.ToCharArray();
        chars[0] = char.ToUpperInvariant(chars[0]);
        return new string(chars);
    }

    protected override void ChildAdded(Control newChild)
    {
        base.ChildAdded(newChild);

        if (newChild == _header)
            return;

        newChild.Margin = new Thickness(BodyIndent, 2, 0, 0);
        newChild.HorizontalExpand = true;
    }
}
