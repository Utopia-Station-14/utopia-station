using Robust.Client.ResourceManagement;
using Robust.Shared.Utility;

namespace Content.Client._Utopia;

public sealed partial class ErrorFallbackSystem : EntitySystem
{
    [Dependency] private IResourceCache _resourceCache = default!;

    private const string UtopiaErrorPath = "/Textures/_Utopia/error.rsi";
    private static readonly ResPath FallbackPath = new("/Textures/error.rsi");

    public override void Initialize()
    {
        base.Initialize();

        try
        {
            var utopiaError = _resourceCache.GetResource<RSIResource>(UtopiaErrorPath, useFallback: false);
            _resourceCache.CacheResource(FallbackPath, utopiaError);
        }
        catch
        {
            Log.Warning("Failed to cache new error RSI at {PATH}, using default fallback", FallbackPath);
        }
    }
}
