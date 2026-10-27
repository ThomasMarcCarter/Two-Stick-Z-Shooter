using System.Collections.Generic;
using UnityEngine;

// The FloorGenerator's hideout areas: each survivor's corner of the hall, lying abandoned under
// dust sheets behind a sealed door until they're rescued, then tidied up and furnished for what
// they do; plus the barracks corner with the stash, the hall's pillars and its clutter.
public partial class FloorGenerator
{
    // A survivor's area. Rescued: tidied up, with their station against the wall, them beside
    // it and their furniture about. Not yet: abandoned, a dust sheet over the spot where the
    // station will go, dusty furniture and empty boxes lying about, and a note saying whose it
    // was and how to find them.
    private void BuildArea(HideoutArea area, Room room)
    {
        Vector3 middle = Middle(area);
        Vector3 back = area.Back;
        Vector3 side = Side(area);
        Vector3 inward = -back;

        // The cook's kitchen stays shut while they're sick in the med bay.
        if (!MetaProgress.IsRescued(area.Owner) || (area.Owner == NpcKind.Cook && Npcs.CookSick))
        {
            BuildAbandonedArea(area, room, middle, back, side);
            return;
        }

        if (area.Owner == NpcKind.Trader)
        {
            Vector3 stall = middle + back * (area.Depth - 1.7f);
            StationConsole.Create(HideoutStation.TradePost, stall, inward, room.transform);
            HideoutNpc.Create(NpcKind.Trader, stall + back * 1.1f, inward, room.transform);
            Furniture("Crate", "Crate", room.transform, stall + side * 2.2f + back * 0.6f, inward, new Vector3(1f, 0.9f, 1f), new Color(0.3f, 0.33f, 0.2f));
            Furniture("Crate", "Crate", room.transform, stall - side * 2.2f + back * 0.8f, inward, new Vector3(0.9f, 0.7f, 0.9f), new Color(0.3f, 0.33f, 0.2f));
            return;
        }

        Vector3 station = middle + back * (area.Depth - 1.3f);
        StationConsole.Create(Npcs.Station(area.Owner), station, inward, room.transform);

        // The medic stands by the far bed, seeing to it, well clear of the med station's front so
        // talking to them and using the station don't get in each other's way, and just clear of
        // the bed's side so its sprite doesn't cut into them.
        // The mechanic works on the truck (BuildTruckBay), not at their bench.
        if (area.Owner == NpcKind.Medic)
            HideoutNpc.Create(area.Owner, station + inward * 2.9f - side * 1.7f, -side, room.transform);
        else if (area.Owner != NpcKind.Mechanic)
            HideoutNpc.Create(area.Owner, station + inward * 2.2f + side * 1.8f, inward, room.transform);
        DressArea(area.Owner, room, middle, back, side, area.Depth, area.Width);

        // The sick cook, up beside the near bed in the med bay, greener the worse they are.
        if (area.Owner == NpcKind.Medic && Npcs.CookSick)
        {
            Vector3 nearBed = middle - side * (area.Width - 1.2f) + back * (area.Depth - 5.1f);
            HideoutNpc cook = HideoutNpc.Create(NpcKind.Cook, nearBed + inward * 0.95f, back, room.transform);
            TintSick(cook.gameObject, Npcs.CookSickness);
        }
    }

    // A sickly green over a survivor's sprite (or model): stage 1 to 3.
    private static void TintSick(GameObject survivor, int stage)
    {
        Color[] tints = { Color.white, new Color(0.88f, 1f, 0.82f), new Color(0.78f, 0.95f, 0.68f), new Color(0.68f, 0.88f, 0.56f) };
        MaterialPropertyBlock block = new MaterialPropertyBlock();

        foreach (Renderer part in survivor.GetComponentsInChildren<Renderer>())
        {
            part.GetPropertyBlock(block);
            block.SetColor("_BaseColor", tints[Mathf.Clamp(stage, 0, 3)]);
            part.SetPropertyBlock(block);
        }
    }

    // Story scenes on the radio, a few seconds apart, after `wait`.
    private System.Collections.IEnumerator HideoutScenes(List<(NpcKind who, string line)> scenes, float wait)
    {
        yield return new WaitForSeconds(distressCallDelay + wait);

        foreach ((NpcKind who, string line) in scenes)
        {
            RadioMessage.Show("HIDEOUT // " + Npcs.Name(who), line);
            yield return new WaitForSeconds(8f);
        }
    }

    // Someone in the hideout on the radio, a moment after it's built.
    private System.Collections.IEnumerator HideoutRadio(string who, string line)
    {
        yield return new WaitForSeconds(distressCallDelay);
        RadioMessage.Show("HIDEOUT // " + who, line);
    }

    // Left as it was found: the station's spot under a dust sheet, a piece or two of the
    // survivor's furniture gone grey with dust, and empty cardboard boxes strewn about.
    private void BuildAbandonedArea(HideoutArea area, Room room, Vector3 middle, Vector3 back, Vector3 side)
    {
        Transform parent = room.transform;
        Vector3 inward = -back;
        Vector3 spot = middle + back * (area.Depth - 1.3f);
        Color sheet = new Color(0.52f, 0.5f, 0.45f);

        // A sheet thrown over something big, and a smaller lump beside it.
        GameObject big = HideoutProp("Dust Sheet", parent, spot + Vector3.up * 0.55f, RotateSize(Quaternion.LookRotation(inward), new Vector3(2.3f, 1.1f, 1.2f)), sheet, Color.black, true);
        GameObject small = HideoutProp("Dust Sheet", parent, spot + side * 1.9f + Vector3.up * 0.35f, RotateSize(Quaternion.LookRotation(inward), new Vector3(0.9f, 0.7f, 0.8f)), sheet * 0.95f, Color.black, true);
        FurnitureSprite.Apply(big, "DustSheet", inward, new Vector3(2.3f, 1.1f, 1.2f), spot);
        FurnitureSprite.Apply(small, "DustSheetSmall", inward, new Vector3(0.9f, 0.7f, 0.8f), spot + side * 1.9f);

        Color wood = new Color(0.3f, 0.24f, 0.16f);
        Color metal = new Color(0.22f, 0.23f, 0.24f);

        switch (area.Owner)
        {
            case NpcKind.Mechanic:
                Dusty(Furniture("Shelving", "Dusty Shelving", parent, middle - side * (area.Width - 0.6f) + back * (area.Depth - 2.2f), side, new Vector3(2.4f, 2f, 0.6f), metal));
                Dusty(Furniture("Tyres", "Dusty Tyres", parent, middle + side * (area.Width - 1.2f) - back * 0.5f, back, new Vector3(0.9f, 0.55f, 0.9f), new Color(0.08f, 0.08f, 0.08f), true, PrimitiveType.Cylinder));
                break;

            case NpcKind.Medic:
                Dusty(Furniture("HospitalBed", "Dusty Bed", parent, middle - side * (area.Width - 1.2f) - back * 0.4f, side, new Vector3(0.9f, 0.88f, 2f), new Color(0.6f, 0.62f, 0.6f)));
                Dusty(Furniture("Screen", "Dusty Screen", parent, middle + side * (area.Width - 1.5f), -side, new Vector3(2.2f, 2f, 0.4f), new Color(0.45f, 0.55f, 0.5f)));
                break;

            case NpcKind.Quartermaster:
                Dusty(Furniture("AmmoCrate", "Empty Ammo Crate", parent, middle - side * (area.Width - 1f) + back * (area.Depth - 1f), inward, new Vector3(1.1f, 0.6f, 0.7f), new Color(0.3f, 0.35f, 0.2f)));
                Dusty(Furniture("GunRack", "Empty Gun Rack", parent, middle + side * (area.Width - 0.3f), -side, new Vector3(2.6f, 2f, 0.3f), wood));
                break;

            default:
                Dusty(Furniture("Counter", "Dusty Counter", parent, middle + back * (area.Depth - 1.7f), inward, new Vector3(2.4f, 1.15f, 0.7f), wood));
                break;
        }

        // Empty boxes: against the wall either side, and one dumped out front.
        PlaceHideoutClutter(room, new[]
        {
            (middle + back * (area.Depth - 0.5f) + side * (area.Width - 0.8f), back),
            (middle + back * (area.Depth - 0.5f) - side * (area.Width - 0.8f), back),
            (middle - back * (area.Depth - 1.2f) + side * 0.6f, -back)
        });

        GameObject sign = new GameObject(area.Name + " Sign");
        sign.transform.SetParent(parent, false);
        sign.transform.position = middle - back * 0.5f;
        ProximityLabel.Add(sign, 1.6f, 4.5f, sealedDoorTint * 1.6f, () => SealedText(area.Name, area.Owner, "ABANDONED"));
        StationMarker.Add(sign, area.Owner == NpcKind.Trader ? HideoutStation.TradePost : Npcs.Station(area.Owner), true);
    }

    // Greys a piece of furniture over, like it's sat under years of dust.
    private static void Dusty(GameObject prop)
    {
        if (prop == null)
            return;

        // A sprite has its own dusty frames.
        if (prop.TryGetComponent(out FurnitureSprite sprite))
        {
            sprite.Dusty = true;
            return;
        }

        Color dust = new Color(0.42f, 0.4f, 0.36f);
        MaterialPropertyBlock block = new MaterialPropertyBlock();

        foreach (Renderer renderer in prop.GetComponentsInChildren<Renderer>())
        {
            Material[] materials = renderer.sharedMaterials;

            for (int i = 0; i < materials.Length; i++)
            {
                Color colour = materials[i] != null && materials[i].HasProperty("_BaseColor") ? materials[i].GetColor("_BaseColor") : Color.grey;
                block.Clear();
                block.SetColor("_BaseColor", Color.Lerp(colour, dust, 0.65f));
                block.SetColor("_EmissionColor", Color.black);
                renderer.SetPropertyBlock(block, i);
            }
        }
    }

    // What an abandoned area's note says: how to find the survivor (how many more truck parts
    // until their signal can be picked up), or that it's out there now.
    private static string SealedText(string area, NpcKind owner, string state)
    {
        string header = area + $"  <size=70%><alpha=#99>// {state}</size>\n";
        string hint = Npcs.FindHint(owner);

        if (hint != null)
            return header + $"<size=70%><alpha=#BB>{hint}</size>";

        return header + $"<size=70%><color={UiColours.AmberHex}>DISTRESS SIGNAL ACTIVE // FIND THE {Npcs.Role(owner)} ON A RUN</color></size>";
    }

    // A rescued survivor's things, tidied round their station: shelves of parts for the
    // workshop, beds and supplies for the med bay, crates and racks for the supply room.
    private void DressArea(NpcKind kind, Room room, Vector3 centre, Vector3 far, Vector3 side, float depth, float width)
    {
        Transform parent = room.transform;
        Quaternion facing = Quaternion.LookRotation(-far);
        Color metal = new Color(0.22f, 0.23f, 0.24f);

        switch (kind)
        {
            case NpcKind.Mechanic:
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 shelf = centre + side * s * (width - 0.6f) + far * (depth - 2.2f);
                    Furniture("Shelving", "Shelving", parent, shelf, -side * s, new Vector3(2.4f, 2f, 0.6f), metal);

                    // The model has its bins and boxes on the shelves already.
                    if (!HasPropModel("Shelving"))
                    {
                        HideoutProp("Parts Bin", parent, shelf + Vector3.up * 1.3f - side * s * 0.05f, RotateSize(facing, new Vector3(0.65f, 0.3f, 0.6f)), new Color(0.6f, 0.4f, 0.1f), Color.black, false);
                        HideoutProp("Parts Bin", parent, shelf + Vector3.up * 0.6f + far * 0.4f, RotateSize(facing, new Vector3(0.65f, 0.3f, 0.6f)), new Color(0.2f, 0.3f, 0.5f), Color.black, false);
                    }
                }

                Furniture("Tyres", "Tyres", parent, centre - side * (width - 1.2f) - far * (depth - 1.5f), far, new Vector3(0.9f, 0.55f, 0.9f),
                    new Color(0.06f, 0.06f, 0.06f), true, PrimitiveType.Cylinder);
                break;

            case NpcKind.Cook:
                // A bin and a crate of tins either side of the stove.
                Furniture("TrashBin", "Bin", parent, centre - side * (width - 0.6f) + far * (depth - 0.8f), -far, new Vector3(0.6f, 1f, 0.6f), metal, true, PrimitiveType.Cylinder);
                Furniture("Crate", "Crate", parent, centre + side * (width - 0.7f) + far * (depth - 0.9f), -far, new Vector3(0.9f, 0.8f, 0.9f), new Color(0.3f, 0.33f, 0.2f));
                break;

            case NpcKind.Medic:
                for (int i = 0; i < 2; i++)
                {
                    Vector3 bed = centre - side * (width - 1.2f) + far * (depth - 2.5f - i * 2.6f);

                    if (PropModel("HospitalBed", "Bed", parent, bed, side, new Vector3(0.9f, 0.88f, 2f)) == null)
                    {
                        HideoutProp("Bed", parent, bed + Vector3.up * 0.35f, RotateSize(Quaternion.LookRotation(side), new Vector3(0.9f, 0.25f, 2f)), new Color(0.75f, 0.77f, 0.75f), Color.black, true);
                        HideoutProp("Bed Frame", parent, bed + Vector3.up * 0.15f, RotateSize(Quaternion.LookRotation(side), new Vector3(0.85f, 0.3f, 1.9f)), metal, Color.black, true);
                    }
                }

                // (No privacy screen: the medic stood in front of it, and a sprite in front of
                // something meant to be taller than them looks wrong.)
                Furniture("MedCabinet", "Supplies", parent, centre + side * (width - 0.3f) + far * (depth - 3f), -side, new Vector3(1.2f, 1.2f, 0.5f), new Color(0.8f, 0.8f, 0.8f));
                break;

            default:
                for (int i = 0; i < 3; i++)
                {
                    Vector3 crate = centre - side * (width - 0.9f) + far * (depth - 1.5f - i * 1.3f);
                    Furniture("AmmoCrate", "Ammo Crate", parent, crate, side, new Vector3(1.1f, 0.6f, 0.7f), new Color(0.3f, 0.35f, 0.2f));
                    if (i != 1)
                        Furniture("AmmoCrate", "Ammo Crate", parent, crate + Vector3.up * 0.6f, side, new Vector3(0.9f, 0.5f, 0.6f), new Color(0.33f, 0.37f, 0.22f));
                }

                // The rack against the other side, rifles stood in it.
                Vector3 rack = centre + side * (width - 0.25f) + far * (depth - 2.5f);
                Furniture("GunRack", "Gun Rack", parent, rack, -side, new Vector3(2.6f, 2f, 0.3f), new Color(0.3f, 0.22f, 0.14f));

                if (!HasPropModel("GunRack"))
                {
                    for (int g = -2; g <= 2; g++)
                        HideoutProp("Rifle", parent, rack + Vector3.up * 1.1f + far * (g * 0.45f) - side * 0.25f, new Vector3(0.06f, 1.1f, 0.06f), new Color(0.1f, 0.1f, 0.1f), Color.black, false);
                }
                break;
        }
    }

    // The south-west corner: bunks against the west wall and the stash, a row of lockers on the
    // south wall whose middle one is lit, its label listing the banked scrap, materials and
    // records. Always there.
    private void BuildBarracksCorner(Room room)
    {
        Transform parent = room.transform;
        Vector3 centre = HallCentre;
        Vector2 half = HallHalf;
        Color metal = new Color(0.25f, 0.27f, 0.25f);
        Color blanket = new Color(0.25f, 0.3f, 0.2f);

        for (int i = 0; i < 2; i++)
        {
            // Head against the west wall.
            Vector3 bunk = centre + new Vector3(-half.x + 1.1f, 0f, -6.5f - i * 2.1f);

            if (PropModel("BunkBed", "Bunk", parent, bunk, Vector3.forward, new Vector3(2f, 1.9f, 0.9f)) != null)
                continue;

            Vector3 size = new Vector3(2f, 0.15f, 0.9f);
            HideoutProp("Bunk", parent, bunk + Vector3.up * 0.45f, size, blanket, Color.black, true);
            HideoutProp("Bunk", parent, bunk + Vector3.up * 1.45f, size, blanket, Color.black, true);
            HideoutProp("Bunk Frame", parent, bunk + Vector3.up * 1f, size + Vector3.up * 1.8f - new Vector3(0.1f, 0f, 0.1f), metal, Color.black, true);
        }

        Vector3 row = centre + new Vector3(-13.5f, 0f, -half.y + 0.4f);

        for (int i = -2; i <= 2; i++)
        {
            Vector3 locker = row + Vector3.right * i * 0.75f;

            if (PropModel(i == 0 ? "StashLocker" : "Locker", "Locker", parent, locker, Vector3.forward, new Vector3(0.7f, 2f, 0.6f)) == null)
                HideoutProp("Locker", parent, locker + Vector3.up * 1f, new Vector3(0.7f, 2f, 0.6f),
                    i == 0 ? new Color(0.25f, 0.4f, 0.3f) : metal, i == 0 ? new Color(0.05f, 0.25f, 0.1f) : Color.black, true);
        }

        GameObject stash = new GameObject("Stash");
        stash.transform.SetParent(parent, false);
        stash.transform.position = row + Vector3.forward * 0.5f;
        ProximityLabel.Add(stash, 2.3f, 3.5f, UiColours.Amber, StashText);

        Light glow = new GameObject("Stash Light").AddComponent<Light>();
        glow.transform.SetParent(stash.transform, false);
        glow.transform.localPosition = Vector3.up * 1.8f + Vector3.forward * 0.6f;
        glow.type = LightType.Point;
        glow.color = new Color(1f, 0.85f, 0.45f);
        glow.range = 4f;
        glow.intensity = 2f;
        glow.shadows = LightShadows.None;
    }

    private static string StashText()
    {
        string text = $"STASH  <size=70%><alpha=#99>// {MetaProgress.Scrap} SCRAP</size>\n<size=65%>";

        foreach (MaterialType type in Materials.All)
            text += $"<color={Materials.Hex(type)}>{Materials.Name(type)} {MetaProgress.MaterialCount(type)}</color>  ";

        return text + $"\n<alpha=#99>RUNS {MetaProgress.Runs} // WINS {MetaProgress.Victories} // DEEPEST {MetaProgress.BestFloor} // KILLS {MetaProgress.Kills}</size>";
    }

    // One of the floors' pillars, placed by hand.
    private void HideoutPillar(Room room, Vector3 position)
    {
        GameObject pillar;

        if (pillarPrefab != null)
        {
            pillar = Instantiate(pillarPrefab, position, Quaternion.Euler(0f, 90f * rng.Next(4), 0f), room.transform);
            pillar.name = "Pillar";
            pillar.transform.localScale = new Vector3(pillarSize, pillarHeight, pillarSize);
        }
        else
        {
            pillar = CreateBlock("Pillar", room.transform, ToLocal(room.transform, position + Vector3.up * wallHeight / 2f),
                new Vector3(pillarSize, wallHeight, pillarSize), wallMaterial);
        }

        pillar.tag = "Wall";
        pillar.GetComponent<Collider>().sharedMaterial = slidePhysics;
        FurnitureSprite.Apply(pillar, "Pillar", Vector3.back, new Vector3(pillarSize, pillarHeight, pillarSize), position);
    }

    // Cardboard box clusters at given spots against walls (position, the wall's outward direction).
    private void PlaceHideoutClutter(Room room, (Vector3 spot, Vector3 outward)[] spots)
    {
        if (clutterBoxes == null || clutterBoxes.Length == 0)
            return;

        foreach ((Vector3 spot, Vector3 outward) in spots)
            PlaceCluster(room, spot, outward, new Vector3(outward.z, 0f, outward.x));
    }
}
