using System;
using System.Collections.Generic;
using System.Linq;
using Sinbinder.Gameplay;

static class Program
{
    static int n;
    static void Check(bool yes, string reason) { if (!yes) throw new Exception("FAIL " + reason); n++; }
    static void Main()
    {
        var first = ExpeditionMapPlan.ForName("Соляной обоз");
        var second = ExpeditionMapPlan.ForName("Придорожная часовня");
        Check(first != null && second != null, "two real supported maps");
        Check(first.Id != second.Id && first.Kind != second.Kind, "distinct stable map ids");
        Check(ExpeditionMapPlan.ForName("Старый гарнизон") == null, "unsupported mission isn't a fake level");
        foreach (var m in new[]{first, second})
        {
            Check(m.Props.Count >= 35, "map has real assembled environment");
            Check(m.EntranceZ < -40 && m.GoalZ > 20, "clear traversable route");
            Check(m.Width > 95 && m.Depth > 110, "map has playable scale");
            Check(m.Props.All(p => Math.Abs(p.X) < m.Width/2 && Math.Abs(p.Z) < m.Depth/2),
                "no scenery outside field");
            Check(m.Props.All(p => p.Size > 0), "all props positive size");
            Check(m.Props.All(p => Math.Abs(p.X) > 9 || p.Z > 8),
                "no decorative blockers in the entry lane");
        }
        Check(first.Props.Any(p => p.Kind == ExpeditionPropKind.Wagon),
            "caravan has actual wagons");
        Check(second.Props.Any(p => p.Kind == ExpeditionPropKind.Grave),
            "chapel has graveyard");
        Check(second.Props.Any(p => p.Kind == ExpeditionPropKind.Altar),
            "chapel has physical altar");
        Check(first.Props.Count != second.Props.Count,
            "two maps genuinely differ");
        Console.WriteLine($"PASS: {n} expedition level assertions");
    }
}
