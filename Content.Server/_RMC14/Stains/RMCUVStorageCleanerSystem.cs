using Content.Server.Storage.Components;
using Content.Shared._RMC14.Stains;
using Content.Shared.Storage.Components;
using Robust.Shared.Timing;

namespace Content.Server._RMC14.Stains;

public sealed class RMCUVStorageCleanerSystem : SharedRMCUVStorageCleanerSystem
{
    [Dependency] private readonly RMCStainSystem _stain = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<RMCUVStorageCleanerComponent, StorageAfterCloseEvent>(OnStorageAfterClose);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<RMCUVStorageCleanerComponent, EntityStorageComponent>();
        while (query.MoveNext(out var uid, out var cleaner, out var storage))
        {
            if (!cleaner.Running || cleaner.FinishAt > now)
                continue;

            var containedEntities = new List<EntityUid>(storage.Contents.ContainedEntities);
            foreach (var contained in containedEntities)
            {
                _stain.CleanEntityStainsAndForensics(contained);
            }

            SetRunning((uid, cleaner), false, TimeSpan.Zero);
        }
    }

    private void OnStorageAfterClose(Entity<RMCUVStorageCleanerComponent> ent, ref StorageAfterCloseEvent args)
    {
        if (ent.Comp.Running ||
            !TryComp<EntityStorageComponent>(ent, out var storage) ||
            storage.Contents.ContainedEntities.Count <= 0)
        {
            return;
        }

        SetRunning(ent, true, _timing.CurTime + ent.Comp.CycleTime);
    }
}
