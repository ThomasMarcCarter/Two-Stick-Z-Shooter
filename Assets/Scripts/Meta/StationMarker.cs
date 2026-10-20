using System.Collections.Generic;
using System.Text;
using UnityEngine;

// A hideout station as it shows on the maps: an icon in the station's colour on the minimap and
// the tactical map, with its upgrade level as pips under it on the tactical map, and a readout
// of its upgrades in the map's side panel while the cursor is over it. Put on each station
// (StationConsole) and on each survivor's abandoned area (dim, until they're rescued and the
// station is set up there).
public class StationMarker : MonoBehaviour
{
    private static readonly List<StationMarker> all = new List<StationMarker>();

    public static IReadOnlyList<StationMarker> All => all;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        all.Clear();
    }

    public HideoutStation Station { get; private set; }

    // A survivor's area that hasn't been set up yet: the station isn't there.
    public bool Abandoned { get; private set; }

    public Vector3 Position => transform.position;

    public static StationMarker Add(GameObject owner, HideoutStation station, bool abandoned)
    {
        StationMarker marker = owner.AddComponent<StationMarker>();
        marker.Station = station;
        marker.Abandoned = abandoned;
        return marker;
    }

    private void OnEnable()
    {
        if (!all.Contains(this))
            all.Add(this);
    }

    private void OnDisable()
    {
        all.Remove(this);
    }

    // Levels bought across all its upgrades, and the most there are.
    public int Level
    {
        get
        {
            int level = 0;

            foreach (MetaUpgrade upgrade in Stations.Upgrades(Station))
                level += MetaProgress.Level(upgrade);

            return level;
        }
    }

    public int MaxLevel
    {
        get
        {
            int max = 0;

            foreach (MetaUpgrade upgrade in Stations.Upgrades(Station))
                max += MetaUpgrades.MaxLevel(upgrade);

            return max;
        }
    }

    // The side panel's readout (monospaced, about 22 characters wide): who runs it and each
    // upgrade's level and what the next one costs; the trader's stock; or, for an abandoned
    // area, who has to be rescued to open it.
    public string Describe()
    {
        StringBuilder text = new StringBuilder();
        string dim = "<alpha=#77>";
        string undim = "<alpha=#FF>";
        string colour = "#" + ColorUtility.ToHtmlStringRGB(Stations.Colour(Station));
        NpcKind? owner = Stations.Owner(Station);

        text.AppendLine("STATION DATA");
        text.AppendLine();
        text.AppendLine($"<color={colour}>{Stations.Name(Station)}</color>");

        if (Abandoned && owner is NpcKind missing)
        {
            int needed = Npcs.RequiredVictories(missing) - MetaProgress.Victories;
            text.AppendLine($"<color={UiColours.DangerHex}>ABANDONED</color>");
            text.AppendLine();
            text.AppendLine($"{dim}RESCUE THE{undim}");
            text.AppendLine($"{Npcs.Role(missing)} TO OPEN IT");
            text.AppendLine();
            text.AppendLine(needed > 0
                ? $"{dim}NO SIGNAL YET\n{dim}{needed} MORE RUN{(needed == 1 ? "" : "S")} TO GO{undim}"
                : $"<color={UiColours.AmberHex}>SIGNAL ACTIVE</color>");
            return text.ToString();
        }

        text.AppendLine((owner is NpcKind runBy ? $"{dim}RUN BY {Npcs.Name(runBy)}" : $"{dim}NO OPERATOR NEEDED") + undim);
        text.AppendLine();

        if (Station == HideoutStation.TradePost)
        {
            int left = 0;

            foreach (TraderStock.Offer offer in TraderStock.Offers)
                left += offer.Left;

            text.AppendLine("STOCK");
            text.AppendLine($"{left} ITEM{(left == 1 ? "" : "S")} FOR SALE");
            text.AppendLine($"{dim}NEW STOCK EVERY RUN{undim}");
            return text.ToString();
        }

        text.AppendLine($"UPGRADES  LV {Level}/{MaxLevel}");

        foreach (MetaUpgrade upgrade in Stations.Upgrades(Station))
        {
            int level = MetaProgress.Level(upgrade);
            int max = MetaUpgrades.MaxLevel(upgrade);
            int cost = MetaProgress.NextCost(upgrade);
            string pips = new string('#', level) + new string('-', max - level);

            text.AppendLine();
            text.AppendLine($"{MetaUpgrades.Name(upgrade),-13}[{pips}]");

            if (cost < 0)
            {
                text.AppendLine($"{dim}  MAXED{undim}");
                continue;
            }

            string scrap = MetaProgress.Scrap >= cost ? $"{cost} SCRAP" : $"<color={UiColours.DangerHex}>{cost} SCRAP</color>";
            bool materials = MetaUpgrades.MaterialCosts(upgrade, level + 1).Length > 0;
            text.AppendLine($"  NEXT {scrap}{(materials ? " +MATS" : "")}");
        }

        return text.ToString();
    }
}
