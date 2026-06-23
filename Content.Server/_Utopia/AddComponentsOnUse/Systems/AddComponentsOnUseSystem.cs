using Content.Shared.Interaction.Events;

namespace Content.Server._Utopia.AddComponentsOnUse;

public sealed class AddComponentsOnUseSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AddComponentsOnUseComponent, UseInHandEvent>(OnUsed);
    }

    private void OnUsed(EntityUid uid, AddComponentsOnUseComponent component, UseInHandEvent args)
    {
        try
        {
            EntityManager.AddComponents(args.User, component.Components, removeExisting: false);
        }
        catch (Exception ex)
        {
            Log.Debug($"Failed to add components to {ToPrettyString(uid)}: {ex.Message}");
        }

        if (component.DeleteOnUse)
        {
            QueueDel(uid);
        }
    }
}
