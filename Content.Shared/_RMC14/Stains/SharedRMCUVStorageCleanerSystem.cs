namespace Content.Shared._RMC14.Stains;

public abstract class SharedRMCUVStorageCleanerSystem : EntitySystem
{
    public void SetRunning(Entity<RMCUVStorageCleanerComponent> storage, bool running, TimeSpan finishAt)
    {
        storage.Comp.Running = running;
        storage.Comp.FinishAt = finishAt;
        Dirty(storage);
    }
}
