// New Game+: once the evac chopper has flown everyone out, the credits offer to start the story
// over. Only the banked scrap and crafting materials carry across (user, 2026-10-27); the
// upgrades, the survivors, every goal's story items, the truck, the zones and the one-time radio
// scenes all start again from nothing. Each loop makes the zombies and bosses tougher: 25% more
// health and harder hits per loop, stacking, and one more zombie in every combat room
// (FloorGenerator.SpawnEnemy). The lifetime record (runs, wins, kills, evacs) keeps counting.
using UnityEngine;

public static partial class MetaProgress
{
    public const float ToughnessPerLoop = 0.25f;

    // New Game+ loops started: 0 on the first playthrough.
    public static int Loop => Data.loop;

    // What enemy health and damage are multiplied by on this loop.
    public static float Toughness => 1f + ToughnessPerLoop * Loop;

    // Whether New Game+ can be started: the evac's been flown on this loop.
    public static bool CanStartNewGamePlus => Data.evacs > Data.loopEvacs;

    // From the debug menu, without flying the evac first; back to the (reset) hideout.
    public static void DebugStartNewGamePlus()
    {
        Data.evacs = Mathf.Max(Data.evacs, Data.loopEvacs + 1);
        StartNewGamePlus();
        RunFlow.ExitToHideout();
    }

    public static void StartNewGamePlus()
    {
        if (!CanStartNewGamePlus)
            return;

        runActive = false;
        Data.loop++;
        Data.loopEvacs = Data.evacs;
        Data.levels = new int[0];
        Data.rescued = new int[0];
        Data.story = new int[0];
        Data.scenes = new int[0];
        Data.zones = new int[0];
        Data.zone = 0;
        Data.cookArrived = -1;
        Data.cookCureSeen = 0;
        Save();
        Changed?.Invoke();
    }
}
