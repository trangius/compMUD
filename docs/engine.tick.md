# The tick

Nothing happens in the engine between ticks. The world is frozen until
the frontend calls [`World.Tick()`](../Engine/World.cs#L67); time in this world is discrete, one
`Tick()` call at a time. Inside a single call, two passes run in order,
then [`tickCount`](../Engine/World.cs#L44)`++`.

## Pass 1 — one action per entity

Every entity with a [`Behaviors`](../Engine/Behaviors/Behavior.cs#L28) component gets a turn when its scheduler
reports it as due. Among willing behaviors (those whose [`WouldAct`](../Engine/Behaviors/Behavior.cs#L17)
returns true), the one with the highest [`Priority`](../Engine/Behaviors/Behavior.cs#L13) runs its [`Act`](../Engine/Behaviors/Behavior.cs#L24). That
`Act` returns a cost, and the scheduler pushes the entity's `NextActTick`
forward by `period × cost`.

Some state components constrain which of an entity's behaviors can
run. [`Grappled`](../Engine/Behaviors/Grapple.cs#L8), for example, limits the entity to behaviors marked
[`ICanActWhenGrappled`](../Engine/Behaviors/Grapple.cs#L45) — today only [`EscapeGrappleBehavior`](../Engine/Behaviors/Grapple.cs#L53). The
dispatcher drops `Grappled` automatically at the start of the entity's
turn if the attacker has died or moved away, so a stale pin can't keep
the victim out of its own turn. Future states (sleep, stun, paralysis)
can plug into the same pattern with their own marker interfaces.

## Pass 2 — every effect, every tick

For every entity with an [`Effects`](../Engine/Effects/Effect.cs#L16) component, every effect applies in
list order. No competition. An effect that destroys its host breaks out
of the effect loop early.

Effects run **wall-clock** — every global tick, every entity, regardless
of scheduler period. A slow creature drains the same amount of energy
per global tick as a fast one.

## Edge cases

- **Destroying an entity mid-tick is fine.** A behavior or effect can
  destroy its host or any other entity. The dispatcher's
  [`EntityExists(id)`](../Engine/World.cs#L301) checks skip anyone already gone.
- **Creating an entity mid-tick is fine.** Breeding and raid-spawning
  both do this. The new entity is alive immediately but isn't in the
  current iteration's snapshot — it starts acting on the next tick.

## Why two passes

One action per entity per turn is enforced by the loop shape, not by
convention. Effects in a separate pass guarantee that passive processes
(drain, decay, poison) fire regardless of what the entity chose. A
creature can't escape starvation by choosing not to eat — Pass 2 runs
the drain anyway.
