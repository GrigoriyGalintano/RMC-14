using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared._RMC14.Stains;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Popups;

namespace Content.Shared._RMC14.Chemistry;

public sealed class SharedRMCSinkWaterSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedRMCStainSystem _stains = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RMCSinkWaterComponent, InteractHandEvent>(OnSinkInteractHand);
        SubscribeLocalEvent<RMCSinkWaterComponent, InteractUsingEvent>(OnSinkInteractUsing);
    }

    private void OnSinkInteractHand(Entity<RMCSinkWaterComponent> sink, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        if (!_stains.TryCleanMobStains(args.User, RMCCleanStainFlags.Hands))
            return;

        args.Handled = true;
        var message = Loc.GetString("rmc-sink-wash-hands", ("sink", sink));
        _popup.PopupClient(message, sink, args.User);
    }

    private void OnSinkInteractUsing(Entity<RMCSinkWaterComponent> sink, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (_stains.TryCleanStain(args.Used))
        {
            args.Handled = true;
            var cleanMessage = Loc.GetString("rmc-sink-wash-item", ("item", args.Used), ("sink", sink));
            _popup.PopupClient(cleanMessage, sink, args.User);
            return;
        }

        if (!TryComp<RefillableSolutionComponent>(args.Used, out var refillable))
            return;

        if (!_solution.TryGetRefillableSolution((args.Used, refillable, null), out var targetSolution, out var solution))
            return;

        args.Handled = true;

        var availableSpace = solution.AvailableVolume;
        if (availableSpace <= FixedPoint2.Zero)
        {
            var fullMessage = Loc.GetString("rmc-sink-container-full", ("container", args.Used));
            _popup.PopupClient(fullMessage, sink, args.User);
            return;
        }

        var transferAmount = availableSpace;
        if (TryComp<SolutionTransferComponent>(args.Used, out var transfer))
            transferAmount = FixedPoint2.Min(transfer.TransferAmount, availableSpace);

        var waterSolution = new Solution();
        waterSolution.AddReagent(sink.Comp.Reagent, transferAmount);
        _solution.TryAddSolution(targetSolution.Value, waterSolution);

        var message = Loc.GetString("rmc-sink-fill-container", ("user", args.User), ("container", args.Used), ("sink", sink));
        _popup.PopupPredicted(message, args.User, args.User);
    }
}
