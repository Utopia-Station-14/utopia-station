using System.Linq;
using System.Numerics;
using Content.Shared._Utopia.Research;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Utopia.Research.UI;

public sealed partial class DraggablePanel : LayoutContainer
{
    public DraggablePanel() { }

    protected override void Draw(DrawingHandleScreen handle)
    {
        foreach (var child in Children)
        {
            if (child is not ResearchConsoleItem item)
                continue;

            if (item.Prototype.TechnologyPrerequisites.Count <= 0)
                continue;

            var list = Children.Where(x => x is ResearchConsoleItem second
            && item.Prototype.TechnologyPrerequisites.Contains(second.Prototype.ID));

            var lineColor = GetRefinedConnectionColor(item);

            foreach (var second in list)
            {

                var startCoords = new Vector2(item.PixelPosition.X + item.PixelWidth / 2, item.PixelPosition.Y + item.PixelHeight / 2);
                var endCoords = new Vector2(second.PixelPosition.X + second.PixelWidth / 2, second.PixelPosition.Y + second.PixelHeight / 2);

                if (second.PixelPosition.Y != item.PixelPosition.Y)
                {

                    handle.DrawLine(startCoords, new(endCoords.X, startCoords.Y), lineColor);
                    handle.DrawLine(new(endCoords.X, startCoords.Y), endCoords, lineColor);
                }
                else
                {
                    handle.DrawLine(startCoords, endCoords, lineColor);
                }
            }
        }
    }

    private Color GetRefinedConnectionColor(ResearchConsoleItem dependent)
    {
        return dependent.Availablity switch
        {
            // Researched - bright green
            ResearchAvailablity.Researched => Color.LimeGreen,

            ResearchAvailablity.Available => Color.DarkCyan,

            ResearchAvailablity.Unavailable => Color.Crimson,

            _ => Color.FromHex("#808080")
        };
    }
}
