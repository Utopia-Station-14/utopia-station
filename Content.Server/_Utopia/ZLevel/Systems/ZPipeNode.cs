using Content.Server.NodeContainer.Nodes;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Server._Utopia.ZLevels.Pipes.Nodes;

[DataDefinition]
public sealed partial class ZPipeNode : PipeNode
{
    [DataField(required: true)]
    public ZPipeDirection ZDirection;
}
