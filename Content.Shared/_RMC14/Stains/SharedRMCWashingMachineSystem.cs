namespace Content.Shared._RMC14.Stains;

public abstract class SharedRMCWashingMachineSystem : EntitySystem
{
    public void SetRunning(Entity<RMCWashingMachineComponent> machine, bool running, TimeSpan finishAt)
    {
        machine.Comp.Running = running;
        machine.Comp.FinishAt = finishAt;
        Dirty(machine);
    }
}
