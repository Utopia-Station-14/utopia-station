/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Server._Utopia.ZLevels.Nodes;

namespace Content.Server._Utopia.ZLevels.Disposal.Components;

[RegisterComponent]
public sealed partial class ZDisposalPipeComponent : Component
{
    [DataField(required: true)]
    public ZNodeDirection ZDirection;
}
