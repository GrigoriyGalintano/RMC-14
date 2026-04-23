using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Marines.Roles.Ranks;

[ByRefEvent]
public readonly record struct RankChangedEvent(
    EntityUid User,
    ProtoId<RankPrototype>? OldRank,
    ProtoId<RankPrototype> NewRank
);
