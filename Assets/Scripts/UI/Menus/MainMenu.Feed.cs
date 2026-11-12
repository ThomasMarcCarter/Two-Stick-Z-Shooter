using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The main menu's backdrop (2026-11-12): a security camera feed of the floor the scene built
// behind the menu. That floor is never played (Start Game always reloads the scene), so the game
// keeps running under the menu: lights flicker, fires burn, zombies stand dormant in their rooms.
// The camera holds on one room at a time, drifting slowly across it, then cuts to another
// through a burst of static (rooms with zombies come up more often). A dim layer, a shade that
// darkens the left of the screen behind the menu column, corner brackets, a camera label and a
// blinking REC clock dress it as a feed. Everything the Canvas draws under the menu (the HUD) is hidden while it's up.
public partial class MainMenu
{
    private const float ShotTime = 7f;
    private const float StaticTime = 0.3f;
    private const float DriftSpeed = 0.35f;
    private const float FeedDim = 0.6f;
    private const float StaticFloor = 0.03f;

    private CanvasGroup[] hiddenHud;
    private float[] hiddenHudAlpha;
    private TMP_Text feedLabel;
    private TMP_Text recLabel;
    private RawImage staticNoise;
    private FloorGenerator floor;
    private CameraFollow cameraFollow;
    private Room lastShotRoom;
    private float shotStarted = float.NegativeInfinity;
    private Vector3 shotFrom;
    private Vector3 shotDrift;
    private float feedClock;

    // ---------- Building ----------

    // Built under the backdrop, before the menu panel so the panel draws on top.
    private void BuildFeed(Image fill)
    {
        fill.color = UiColours.WithAlpha(UiColours.MenuBackground, FeedDim);

        staticNoise = new GameObject("Static", typeof(RectTransform)).AddComponent<RawImage>();
        Stretch(staticNoise.rectTransform);
        staticNoise.texture = MakeNoise(192, 108);
        staticNoise.raycastTarget = false;

        // Darker down the left, behind the menu column, so the text reads over a bright room.
        CrtPanel.MakeSideShade(backdrop.transform, UiColours.WithAlpha(UiColours.MenuBackground, 0.95f), 0.75f);

        Color bracket = CrtPanel.Solid(UiColours.MenuBackground, UiColours.Phosphor, 0.5f);

        for (int corner = 0; corner < 4; corner++)
        {
            Vector2 anchor = new Vector2(corner % 2, corner / 2);
            Vector2 inward = new Vector2(anchor.x == 0f ? 1f : -1f, anchor.y == 0f ? 1f : -1f);
            Vector2 at = inward * 28f;
            MakeBar(anchor, at, new Vector2(46f, 3f), inward, bracket);
            MakeBar(anchor, at, new Vector2(3f, 46f), inward, bracket);
        }

        Color dim = CrtPanel.Solid(UiColours.MenuBackground, UiColours.Phosphor, 0.7f);
        feedLabel = MakeCornerText("Feed Label", new Vector2(0f, 1f), new Vector2(44f, -40f), TextAlignmentOptions.TopLeft, dim);
        recLabel = MakeCornerText("Rec Label", new Vector2(1f, 1f), new Vector2(-44f, -40f), TextAlignmentOptions.TopRight, dim);
    }

    private void Stretch(RectTransform rect)
    {
        rect.gameObject.layer = gameObject.layer;
        rect.SetParent(backdrop.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    // One arm of a corner bracket, growing inward from the corner.
    private void MakeBar(Vector2 anchor, Vector2 position, Vector2 size, Vector2 inward, Color colour)
    {
        Vector2 pivot = new Vector2(inward.x > 0f ? 0f : 1f, inward.y > 0f ? 0f : 1f);
        RectTransform rect = CrtPanel.MakeRect(backdrop.transform, "Bracket", anchor, pivot, position, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = colour;
        image.raycastTarget = false;
    }

    private TMP_Text MakeCornerText(string name, Vector2 anchor, Vector2 position, TextAlignmentOptions alignment, Color colour)
    {
        RectTransform rect = CrtPanel.MakeRect(backdrop.transform, name, anchor, anchor, position, new Vector2(520f, 24f));
        return CrtPanel.MakeLabel(rect, TMP_Settings.defaultFontAsset, "", 16f, colour, 4f, alignment);
    }

    // Grey-green snow, drawn pixel-sharp and scrolled about each frame so it seethes.
    private static Texture2D MakeNoise(int width, int height)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat,
            name = "Menu Static",
        };
        Color32[] pixels = new Color32[width * height];

        for (int i = 0; i < pixels.Length; i++)
        {
            byte v = (byte)Random.Range(0, 256);
            pixels[i] = new Color32((byte)(v * 0.55f), v, (byte)(v * 0.65f), 255);
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        return texture;
    }

    // ---------- Showing ----------

    private void ShowFeed()
    {
        shotStarted = float.NegativeInfinity;
        lastShotRoom = null;
        feedClock = Random.Range(3600f, 86000f);
        HideHud(true);
    }

    private void HideFeed()
    {
        CameraFollow.Focus = null;
        HideHud(false);

        if (cameraFollow != null)
            cameraFollow.SnapToTarget();
    }

    // Everything on the Canvas under the menu (the HUD and its widgets) goes invisible while the
    // menu is up, and comes back as it was.
    private void HideHud(bool hide)
    {
        Transform canvas = transform.parent;

        if (canvas == null)
            return;

        if (hide)
        {
            int count = transform.GetSiblingIndex();
            hiddenHud = new CanvasGroup[count];
            hiddenHudAlpha = new float[count];

            for (int i = 0; i < count; i++)
            {
                Transform child = canvas.GetChild(i);
                hiddenHud[i] = child.GetComponent<CanvasGroup>();

                if (hiddenHud[i] == null)
                    hiddenHud[i] = child.gameObject.AddComponent<CanvasGroup>();

                hiddenHudAlpha[i] = hiddenHud[i].alpha;
                hiddenHud[i].alpha = 0f;
            }

            return;
        }

        if (hiddenHud == null)
            return;

        for (int i = 0; i < hiddenHud.Length; i++)
            if (hiddenHud[i] != null)
                hiddenHud[i].alpha = hiddenHudAlpha[i];

        hiddenHud = null;
    }

    private void UpdateFeed()
    {
        if (floor == null)
            floor = FindAnyObjectByType<FloorGenerator>();

        if (cameraFollow == null && Camera.main != null)
            cameraFollow = Camera.main.GetComponent<CameraFollow>();

        // HUD widgets built after the menu opened (in their Start) are hidden too.
        if (hiddenHud != null && hiddenHud.Length != transform.GetSiblingIndex())
        {
            HideHud(false);
            HideHud(true);
        }

        if (Time.unscaledTime - shotStarted >= ShotTime)
            NextShot();

        float shotAge = Time.unscaledTime - shotStarted;

        if (lastShotRoom != null)
            CameraFollow.Focus = shotFrom + shotDrift * shotAge;

        float burst = 1f - Mathf.Clamp01(shotAge / StaticTime);
        staticNoise.color = new Color(1f, 1f, 1f, Mathf.Lerp(StaticFloor, 0.9f, burst * burst));
        staticNoise.uvRect = new Rect(Random.value, Random.value, 1f, 1f);

        feedClock += Time.unscaledDeltaTime;
        int seconds = (int)feedClock;
        bool blink = Mathf.Repeat(Time.unscaledTime, 1.2f) < 0.7f;
        recLabel.text = $"{(blink ? $"<color={UiColours.DangerHex}>●</color>" : "  ")} REC  {seconds / 3600 % 24:00}:{seconds / 60 % 60:00}:{seconds % 60:00}";
    }

    // Cuts to a different room, picked at random with zombie rooms twice as likely, and drifts
    // across the middle of its biggest floor block.
    private void NextShot()
    {
        shotStarted = Time.unscaledTime;
        Room room = PickRoom();

        if (room == null)
        {
            feedLabel.text = "CAM -- // NO SIGNAL";
            return;
        }

        lastShotRoom = room;
        Rect block = room.Bounds;
        float biggest = 0f;

        foreach (Rect piece in room.Blocks)
            if (piece.width * piece.height > biggest)
            {
                biggest = piece.width * piece.height;
                block = piece;
            }

        Vector2 way = Random.insideUnitCircle.normalized;
        shotDrift = new Vector3(way.x, 0f, way.y) * DriftSpeed;
        shotFrom = new Vector3(block.center.x, 0f, block.center.y) - shotDrift * (ShotTime * 0.5f);
        CameraFollow.Focus = shotFrom;

        if (cameraFollow != null)
            cameraFollow.SnapToTarget();

        int number = 0;

        for (int i = 0; i < floor.Rooms.Count; i++)
            if (floor.Rooms[i] == room)
                number = i + 1;

        string where = !string.IsNullOrEmpty(room.DisplayName) ? room.DisplayName : $"{room.Type} ROOM";
        feedLabel.text = $"CAM {number:00} // {floor.FloorName} // {where.ToUpperInvariant()}";
    }

    private Room PickRoom()
    {
        if (floor == null || floor.Rooms.Count == 0)
            return null;

        float total = 0f;

        foreach (Room room in floor.Rooms)
            total += Weight(room);

        float roll = Random.value * total;

        foreach (Room room in floor.Rooms)
        {
            roll -= Weight(room);

            if (roll <= 0f && Weight(room) > 0f)
                return room;
        }

        return floor.Rooms[0];
    }

    private float Weight(Room room)
    {
        if (room == null || (room == lastShotRoom && floor.Rooms.Count > 1))
            return 0f;

        return room.Enemies.Count > 0 ? 2f : 1f;
    }
}
