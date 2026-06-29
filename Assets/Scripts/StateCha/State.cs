using UnityEngine;

// Stand should not promote itself without an explicit movement signal.

// -------- Move --------
public sealed class Move_Stand : IState<CharacteContext>
{
    public void Enter(CharacteContext ctx) { }
    public void Exit(CharacteContext ctx) { }

    public void Tick(CharacteContext ctx, float dt)
    {
        if (!ctx.stateHub.CanMove()) return;

        if (ctx.DashSystem != null && ctx.DashSystem.IsDashing)
        {
            ctx.stateHub.MoveSM.TryChange(MoveStateId.Dash);
            return;
        }

        if (ctx.ShouldBeInMoveState())
        {
            ctx.stateHub.MoveSM.TryChange(MoveStateId.Moveing);
            return;
        }
    }
}

public sealed class Move_Moving : IState<CharacteContext>
{
    public void Enter(CharacteContext ctx) { }
    public void Exit(CharacteContext ctx) { }

    public void Tick(CharacteContext ctx, float dt)
    {
        if (!ctx.stateHub.CanMove()) return;

        if (ctx.DashSystem != null && ctx.DashSystem.IsDashing)
        {
            ctx.stateHub.MoveSM.TryChange(MoveStateId.Dash);
            return;
        }

        if (!ctx.ShouldBeInMoveState())
        {
            ctx.stateHub.MoveSM.TryChange(MoveStateId.Stand);
            return;
        }
    }
}

public sealed class Move_Dash : IState<CharacteContext>
{
    public void Enter(CharacteContext ctx)
    {
    }

    public void Exit(CharacteContext ctx) { }

    public void Tick(CharacteContext ctx, float dt)
    {
        if (ctx.DashSystem == null || !ctx.DashSystem.IsDashing)
        {
            ctx.stateHub.MoveSM.TryChange(
                ctx.ShouldBeInMoveState() ? MoveStateId.Moveing : MoveStateId.Stand
            );
        }
    }
}

public sealed class Move_Knockback : IState<CharacteContext>
{
    public void Enter(CharacteContext ctx) { }
    public void Exit(CharacteContext ctx) { }

    public void Tick(CharacteContext ctx, float dt)
    {
    }
}

public sealed class Move_Stunned : IState<CharacteContext>
{
    public void Enter(CharacteContext ctx) { }
    public void Exit(CharacteContext ctx) { }

    public void Tick(CharacteContext ctx, float dt)
    {
    }
}

// -------- Weapon --------
public sealed class Weapon_Melee : IState<CharacteContext>
{
    public void Enter(CharacteContext ctx)
    {
    }

    public void Exit(CharacteContext ctx)
    {
    }

    public void Tick(CharacteContext ctx, float dt)
    {
    }
}

public sealed class Weapon_Ready : IState<CharacteContext>
{
    public void Enter(CharacteContext ctx) { }
    public void Exit(CharacteContext ctx) { }

    public void Tick(CharacteContext ctx, float dt)
    {
    }
}

public sealed class Weapon_Firing : IState<CharacteContext>
{
    public void Enter(CharacteContext ctx) { }
    public void Exit(CharacteContext ctx) { }

    public void Tick(CharacteContext ctx, float dt)
    {
    }
}

public sealed class Weapon_Reloading : IState<CharacteContext>
{
    public void Enter(CharacteContext ctx)
    {
    }

    public void Exit(CharacteContext ctx) { }

    public void Tick(CharacteContext ctx, float dt)
    {
    }
}

public sealed class Weapon_NoBullet : IState<CharacteContext>
{
    public void Enter(CharacteContext ctx)
    {
    }

    public void Exit(CharacteContext ctx)
    {
    }

    public void Tick(CharacteContext ctx, float dt)
    {
    }
}

// -------- Life --------
public sealed class Life_Alive : IState<CharacteContext>
{
    public void Enter(CharacteContext ctx) { }
    public void Exit(CharacteContext ctx) { }

    public void Tick(CharacteContext ctx, float dt)
    {
    }
}

public sealed class Life_Down : IState<CharacteContext>
{
    public void Enter(CharacteContext ctx) { }
    public void Exit(CharacteContext ctx) { }

    public void Tick(CharacteContext ctx, float dt)
    {
    }
}

public sealed class Life_Dead : IState<CharacteContext>
{
    public void Enter(CharacteContext ctx)
    {
    }

    public void Exit(CharacteContext ctx)
    {
    }

    public void Tick(CharacteContext ctx, float dt)
    {
    }
}

// -------- UI --------
public sealed class UI_Normal : IState<CharacteContext>
{
    public void Enter(CharacteContext ctx)
    {
    }

    public void Exit(CharacteContext ctx)
    {
    }

    public void Tick(CharacteContext ctx, float dt)
    {
    }
}

public sealed class UI_Inventory : IState<CharacteContext>
{
    public void Enter(CharacteContext ctx)
    {
    }

    public void Exit(CharacteContext ctx)
    {
    }

    public void Tick(CharacteContext ctx, float dt)
    {
    }
}

public sealed class UI_Pause : IState<CharacteContext>
{
    public void Enter(CharacteContext ctx) => GlobalTimeScaleManager.Instance.SetPaused(true);
    public void Exit(CharacteContext ctx) => GlobalTimeScaleManager.Instance.SetPaused(false);

    public void Tick(CharacteContext ctx, float dt)
    {
    }
}
