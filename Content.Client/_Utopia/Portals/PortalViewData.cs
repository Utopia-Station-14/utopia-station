using System.Numerics;
using Robust.Client.Graphics;

namespace Content.Client._Utopia.Portals;

public readonly record struct PortalViewData(IClydeViewport? Viewport, Vector2[] Cone);
