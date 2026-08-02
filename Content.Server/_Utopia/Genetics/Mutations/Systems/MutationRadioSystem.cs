using Content.Server._Utopia.Genetics.Mutations.Components;
using Content.Shared.Radio.Components;

namespace Content.Server._Utopia.Genetics.Mutations.Systems;

public sealed class MutationRadioSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MutationRadioComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<MutationRadioComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnInit(Entity<MutationRadioComponent> ent, ref ComponentInit args)
    {
        var mob = ent.Owner;

        var activeRadio = EnsureComp<ActiveRadioComponent>(mob);
        foreach (var channel in ent.Comp.RadioChannels)
        {
            if (activeRadio.Channels.Add(channel))
            {
                ent.Comp.ActiveAddedChannels.Add(channel);
            }
        }

        EnsureComp<IntrinsicRadioReceiverComponent>(mob);

        var intrinsicRadioTransmitter = EnsureComp<IntrinsicRadioTransmitterComponent>(mob);
        foreach (var channel in ent.Comp.RadioChannels)
        {
            if (intrinsicRadioTransmitter.Channels.Add(channel))
            {
                ent.Comp.TransmitterAddedChannels.Add(channel);
            }
        }
    }

    private void OnShutdown(Entity<MutationRadioComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp<ActiveRadioComponent>(ent.Owner, out var activeRadio))
        {
            foreach (var channel in ent.Comp.ActiveAddedChannels)
            {
                activeRadio.Channels.Remove(channel);
            }

            ent.Comp.ActiveAddedChannels.Clear();

            if (activeRadio.Channels.Count == 0)
            {
                RemCompDeferred<ActiveRadioComponent>(ent.Owner);
            }
        }

        if (TryComp<IntrinsicRadioTransmitterComponent>(ent.Owner, out var transmitter))
        {
            foreach (var channel in ent.Comp.TransmitterAddedChannels)
            {
                transmitter.Channels.Remove(channel);
            }

            ent.Comp.TransmitterAddedChannels.Clear();

            if (transmitter.Channels.Count == 0)
            {
                RemCompDeferred<IntrinsicRadioTransmitterComponent>(ent.Owner);
            }
        }

        if (!HasComp<ActiveRadioComponent>(ent.Owner))
        {
            RemCompDeferred<IntrinsicRadioReceiverComponent>(ent.Owner);
        }
    }
}
