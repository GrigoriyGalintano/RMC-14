using Content.Shared._RMC14.Camera;
using Robust.Server.GameObjects;
using Robust.Shared.Player;

namespace Content.Server._RMC14.Camera;

public sealed class RMCCameraSystem : SharedRMCCameraSystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly ViewSubscriberSystem _viewSubscriber = default!;

    private EntityQuery<ActorComponent> _actorQuery;

    public override void Initialize()
    {
        base.Initialize();

        _actorQuery = GetEntityQuery<ActorComponent>();

        SubscribeLocalEvent<RMCCameraWatcherComponent, PlayerAttachedEvent>(OnWatcherPlayerAttached);
        SubscribeLocalEvent<RMCCameraWatcherComponent, PlayerDetachedEvent>(OnWatcherPlayerDetached);
    }

    private void OnWatcherPlayerAttached(Entity<RMCCameraWatcherComponent> ent, ref PlayerAttachedEvent args)
    {
        if (TryComp(ent.Comp.Computer, out RMCCameraComputerComponent? computer) &&
            _ui.IsUiOpen(ent.Comp.Computer.Value, RMCCameraUiKey.Key, ent.Owner))
        {
            Refresh((ent.Comp.Computer.Value, computer), null);
            return;
        }

        RemoveOverrides(ent);
    }

    private void OnWatcherPlayerDetached(Entity<RMCCameraWatcherComponent> ent, ref PlayerDetachedEvent args)
    {
        foreach (var netOverride in ent.Comp.Overrides)
        {
            if (TryGetEntity(netOverride, out var over))
                _viewSubscriber.RemoveViewSubscriber(over.Value, args.Player);
        }

        ent.Comp.Overrides.Clear();
        Dirty(ent);
    }

    protected override void Refresh(Entity<RMCCameraComputerComponent> ent, EntityUid? old)
    {
        base.Refresh(ent, old);

        for (var i = ent.Comp.Watchers.Count - 1; i >= 0; i--)
        {
            var watcher = ent.Comp.Watchers[i];
            if (TerminatingOrDeleted(watcher) || !_ui.IsUiOpen(ent.Owner, RMCCameraUiKey.Key, watcher))
            {
                ent.Comp.Watchers.RemoveAt(i);
                if (TryComp(watcher, out RMCCameraWatcherComponent? stale) && stale.Computer == ent.Owner)
                    RemoveOverrides((watcher, stale));
                continue;
            }

            if (!_actorQuery.TryComp(watcher, out var actor))
                continue;

            var watcherComp = EnsureComp<RMCCameraWatcherComponent>(watcher);
            RemoveOverrides((watcher, watcherComp, actor));

            if (ent.Comp.CurrentCamera is not { } current)
                continue;

            _viewSubscriber.AddViewSubscriber(current, actor.PlayerSession);

            watcherComp.Overrides.Add(GetNetEntity(current));
            Dirty(watcher, watcherComp);
        }
    }

    protected override void OnWatcherRemoved(Entity<RMCCameraWatcherComponent> watcher)
    {
        base.OnWatcherRemoved(watcher);
        RemoveOverrides(watcher);
    }

    private void RemoveOverrides(Entity<RMCCameraWatcherComponent, ActorComponent?> watcher)
    {
        _actorQuery.Resolve(watcher, ref watcher.Comp2, false);

        foreach (var compOverride in watcher.Comp1.Overrides)
        {
            if (watcher.Comp2 is { } actor && TryGetEntity(compOverride, out var over))
                _viewSubscriber.RemoveViewSubscriber(over.Value, actor.PlayerSession);
        }

        watcher.Comp1.Overrides.Clear();
        Dirty(watcher, watcher.Comp1);
    }
}
