using UnityEngine;


// === หน้าที่ของ Stand คือ ทำอะไรต่อ ห้าม ให้ stamd เปลี่ยน Stand ด้วยตัวเอง 

// -------- Move --------
public sealed class Move_Stand : IState<CharacteContext>
{
    public void Enter(CharacteContext ctx) { }
    public void Exit(CharacteContext ctx)  { }

    public void Tick(CharacteContext ctx, float dt)
    {
        if (!ctx.stateHub.CanMove()) return;

        // ถ้า DashSystem เริ่ม dash แล้ว -> เข้า Dash
        if (ctx.DashSystem != null && ctx.DashSystem.IsDashing)
        {
            ctx.stateHub.MoveSM.TryChange(MoveStateId.Dash);
            return;
        }

        // ถ้ากดเดิน -> ไป Moving
        if (MoveCheck.IsMoveIntent(ctx) && ctx.cc != null  )
        {
            ctx.stateHub.MoveSM.TryChange(MoveStateId.Moveing);
            return;
        }

        //////// todo แก้ เป็น AIcontext แล้วค่อยสืบเป้น allt และ enemy /////////
        
        if (ctx is AllyContext ally )
        {
            if (ally.AgentMoveDriver.agentismoving)
            {
                ctx.stateHub.MoveSM.TryChange(MoveStateId.Moveing);
            }
           
        }
        else if (ctx is EnemyContext enemy )
        {
            if (enemy.AgentMoveDriver.agentismoving)
            {
                ctx.stateHub.MoveSM.TryChange(MoveStateId.Moveing);
            }
        }
    }
}

public sealed class Move_Moving : IState<CharacteContext>
{
    public void Enter(CharacteContext ctx) { }
    public void Exit(CharacteContext ctx)  { }

    public void Tick(CharacteContext ctx, float dt)
    {
        if (!ctx.stateHub.CanMove()) return;

        if (ctx.DashSystem != null && ctx.DashSystem.IsDashing)
        {
            ctx.stateHub.MoveSM.TryChange(MoveStateId.Dash);
            return;
        }

        // ถ้าเลิกกดเดิน -> กลับ Grounded
        if (!MoveCheck.IsMoveIntent(ctx) && ctx.cc != null )
        {
            ctx.stateHub.MoveSM.TryChange(MoveStateId.Stand);
            return;
        }
        
        if (ctx is AllyContext ally )
        {
            if (!ally.AgentMoveDriver.agentismoving)
            {
                ctx.stateHub.MoveSM.TryChange(MoveStateId.Stand);
                return;
            }
           
        }
        if (ctx is EnemyContext enemy )
        {
            if (!enemy.AgentMoveDriver.agentismoving)
            {
                ctx.stateHub.MoveSM.TryChange(MoveStateId.Stand);
                
            }
        }
    }
}

public sealed class Move_Dash : IState<CharacteContext>
{
    public void Enter(CharacteContext ctx)
    {
        // เริ่ม dash
    }

    public void Exit(CharacteContext ctx) { }

    public void Tick(CharacteContext ctx, float dt)
    {
        if (ctx.DashSystem == null || !ctx.DashSystem.IsDashing)
        {
            ctx.stateHub.MoveSM.TryChange(
                MoveCheck.IsMoveIntent(ctx) ? MoveStateId.Moveing : MoveStateId.Stand
            );
        }
    }
}

public sealed class Move_Stunned : IState<CharacteContext>
{
    public void Enter(CharacteContext ctx) { }
    public void Exit(CharacteContext ctx)  { }

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
    public void Exit(CharacteContext ctx)  { }

    public void Tick(CharacteContext ctx, float dt)
    {
        
    }
}

public sealed class Weapon_Firing : IState<CharacteContext>
{
    public void Enter(CharacteContext ctx) { }
    public void Exit(CharacteContext ctx)  { }

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
    public void Exit(CharacteContext ctx)  { }

    public void Tick(CharacteContext ctx, float dt)
    {
        
    }
}

public sealed class Life_Down : IState<CharacteContext>
{
    public void Enter(CharacteContext ctx) { }
    public void Exit(CharacteContext ctx)  { }

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
        public void Enter(CharacteContext ctx) => Time.timeScale = 0f;
        public void Exit(CharacteContext ctx) => Time.timeScale = 1f;

        public void Tick(CharacteContext ctx, float dt)
        {
        }
    }
