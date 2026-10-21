using System;
using BunkerAI;

public static class TrapUtilityChecks
{
    private static int assertions;
    static void Check(bool value, string why)
    {
        assertions++;
        if (!value) throw new Exception("FAILED: " + why);
        Console.WriteLine("PASS: " + why);
    }
    public static void Main()
    {
        var memory = new PathMemory();
        memory.Observe(0, 0, 0);
        var first = memory.Cells[new CellKey(0, 0)];
        for (int i = 1; i <= 100; i++) memory.Observe(0, 0, i * .25);
        Check(first.Entries == 1, "standing still is one entry, not 100 visits");
        Check(!TrapUtility.HasEvidence(memory, first, 25), "one visit is insufficient");
        memory.Observe(4, 0, 26); memory.Observe(0, 0, 27);
        Check(first.Entries == 2 && TrapUtility.HasEvidence(memory, first, 27), "a real return visit unlocks a candidate");
        Check(Math.Abs(memory.HeatAt(first, 72) - first.Heat / 2) < 1e-8, "heat halves in 45 seconds");
        var s = TrapUtility.Score(memory, first, 27, 5);
        Check(s.Total >= .5 && s.Total <= 1, "repeat route earns a sufficient normalized score");
        var old = TrapUtility.Score(memory, first, 200, 5);
        Check(old.Total < s.Total, "old routes lose utility");
        first.BlockedUntil = 60;
        Check(!TrapUtility.HasEvidence(memory, first, 30), "consumed site respects cooldown");
        Check(TrapUtility.Choose(.8, 0, 3, true, double.PositiveInfinity) == TrapAction.Place, "place when budget is available");
        Check(TrapUtility.Choose(.8, 0, 3, false, 0) == TrapAction.Wait, "wait during action cooldown");
        Check(TrapUtility.Choose(.4, 0, 3, true, 0) == TrapAction.Wait, "wait below score threshold");
        Check(TrapUtility.Choose(.7, 3, 3, true, .6) == TrapAction.Wait, "hysteresis prevents needless relocation");
        Check(TrapUtility.Choose(.9, 3, 3, true, .4) == TrapAction.Relocate, "relocate when a site is substantially better");
        Check(TrapUtility.Choose(.9, 3, 3, true, double.PositiveInfinity) == TrapAction.Wait, "no eligible old trap means wait");
        Check(TrapUtility.Choose(1, 0, 0, true, 0) == TrapAction.Wait, "zero capacity is safe");
        var jitter = new PathMemory(); jitter.Observe(1.99f,0,0); jitter.Observe(2.01f,0,.1); jitter.Observe(1.99f,0,.2);
        Check(jitter.Cells[new CellKey(0,0)].Entries == 1, "boundary jitter is debounced");
        memory.Observe(-.1f,-.1f,210);
        Check(memory.Cells.ContainsKey(new CellKey(-1,-1)), "negative bunker coordinates map correctly");
        memory.Clear(); Check(memory.Cells.Count == 0, "round reset clears learning");
        for(int i=0;i<600;i++) memory.Observe(i*3,0,i*2);
        Check(memory.Cells.Count == 512, "memory is bounded and evicts old cells");
        var example = new PathMemory();
        example.Observe(0,0,0); example.Observe(4,0,2); example.Observe(0,0,4);
        var hot=example.Cells[new CellKey(0,0)]; var cold=example.Cells[new CellKey(2,0)];
        Check(TrapUtility.Score(example,hot,4,6).Total > TrapUtility.Score(example,cold,4,6).Total, "a repeated route outranks a single pass");
        Check(!TrapUtility.HasEvidence(example,hot,300), "ancient repeated routes no longer qualify");
        Console.WriteLine("ALL " + assertions + " CORE CHECKS PASSED");
    }
}
