using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using System.Numerics;

namespace Content.Client._Utopia.Genetics.DnaScannerConsole.UI;

public sealed partial class UniqueEnzymeButton : Button
{
    private readonly EntityPrototypeView _iconView;
    public float IconScale { get; set; } = 1f;

    private const string DiscoveredIconPrototype = "DnaDiscoveredIcon";
    private const string UndiscoveredIconPrototype = "DnaUndiscoveredIcon";
    private const string ExtraIconPrototype = "DnaExtraIcon";

    public UniqueEnzymeButton()
    {
        RobustXamlLoader.Load(this);

        _iconView = new EntityPrototypeView
        {
            SetSize = new Vector2(70, 40),
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center
        };

        AddChild(_iconView);
        _iconView.SetPrototype(DiscoveredIconPrototype);
        _iconView.Scale = new Vector2(IconScale, IconScale);
    }

    public void UpdateIcon(bool isDiscovered, bool isBase)
    {
        string prototype;

        if (!isDiscovered)
        {
            prototype = UndiscoveredIconPrototype;
        }

        else if (isBase)
        {
            prototype = DiscoveredIconPrototype;
        }

        else
        {
            prototype = ExtraIconPrototype;
        }

        _iconView.SetPrototype(prototype);
    }
}
