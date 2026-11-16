using System;

// Global namespace: Elin resolves act classes by bare type name (ClassCache); Plugin adds a loader for this assembly.
public class ActPsiTeleport : Spell
{
    public override bool Perform()
    {
        var cc = Act.CC;
        if (cc == null || !cc.IsPC) return false;   // player-only power
        int cost = source.cost.Length > 0 ? source.cost[0] : 0;
        AM_PsiTeleport.Begin(cost);
        return true;
    }
}

public class AM_PsiTeleport : AM_ViewMap
{
    static AM_PsiTeleport instance;
    int refund;
    bool done;

    internal static void Begin(int cost)
    {
        instance = instance ?? new AM_PsiTeleport();
        instance.refund = cost; instance.done = false;
        instance.Activate();
        Msg.Say(PsionClass.UiText.Pick("Choose where to teleport.", "テレポート先を選んでください。", "请选择传送目的地。"));
    }

    // AM_ViewMap returns Default (never selects); clicks only process on Valid/Warning.
    public override HitResult HitTest(Point point, Point start)
    {
        if (point == null || !point.IsValid || !point.IsInBounds) return HitResult.Invalid;
        return (point.IsBlocked || point.HasChara) ? HitResult.Warning : HitResult.Valid;
    }

    public override void OnProcessTiles(Point point, int dir)
    {
        done = true;
        Deactivate();
        var pc = EClass.pc;
        bool clear = point != null && point.IsValid && point.IsInBounds && !point.IsBlocked && !point.HasChara;
        if (clear) pc.Teleport(point);
        else
        {
            Msg.Say(PsionClass.UiText.Pick("The destination resists you.", "目的地に阻まれた。", "目的地抗拒了你。"));
            ActEffect.Proc(EffectId.Teleport, pc);
        }
    }

    public override void OnCancel()
    {
        if (!done && refund > 0) EClass.pc.mana.Mod(refund); // cancelled: no cost
        done = true;
        base.OnCancel();
    }
}
