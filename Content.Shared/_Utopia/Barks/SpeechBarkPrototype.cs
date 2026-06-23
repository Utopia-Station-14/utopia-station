using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Audio;

namespace Content.Shared._Utopia.SpeechBarks;

[Prototype]
public sealed partial class SpeechBarkPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public bool RoundStart = true;

    public string LocName => Loc.GetString("bark-" + ID + "-name");

    [DataField]
    public string Category = "standard";

    [DataField(required: true)]
    public SoundSpecifier Sound { get; private set; } = default!;
}

[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class BarkData
{
    [DataField]
    public ProtoId<SpeechBarkPrototype> Proto = "Human1";

    [DataField]
    public SoundSpecifier? Sound = null;

    [DataField]
    public float MinVar = 0.1f;

    [DataField]
    public float MaxVar = 0.5f;

    [DataField]
    public float Pitch = 1f;

    public BarkData WithProto(string proto)
    {
        var data = Copy();
        data.Proto = proto;
        return data;
    }

    public BarkData WithPitch(float pitch)
    {
        var data = Copy();
        data.Pitch = pitch;
        return data;
    }

    public BarkData WithMinVar(float var)
    {
        var data = Copy();
        data.MinVar = var;
        return data;
    }

    public BarkData WithMaxVar(float var)
    {
        var data = Copy();
        data.MaxVar = var;
        return data;
    }

    public BarkData(ProtoId<SpeechBarkPrototype> proto, float pitch, float minVar, float maxVar)
    {
        Proto = proto;
        Pitch = pitch;
        MinVar = minVar;
        MaxVar = maxVar;
    }

    public BarkData Copy()
    {
        return new BarkData()
        {
            Proto = Proto,
            Sound = Sound,
            Pitch = Pitch,
            MinVar = MinVar,
            MaxVar = MaxVar
        };
    }

    public bool MemberwiseEquals(BarkData other)
    {
        if (Proto != other.Proto)
            return false;

        if (Sound != other.Sound)
            return false;

        if (Pitch != other.Pitch)
            return false;

        if (MinVar != other.MinVar)
            return false;

        if (MaxVar != other.MaxVar)
            return false;

        return true;
    }
}
