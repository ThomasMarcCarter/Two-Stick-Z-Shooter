using UnityEngine;

// Which of a sheet's drawn facings shows a world direction, as seen on screen: eight ways round
// from five drawn rows (down, down-side, side, up-side, up), the left-hand three mirrored from
// the right. "Down" is toward the bottom of the screen (facing the camera).
public static class SpriteFacing
{
    public const int Down = 0;
    public const int DownSide = 1;
    public const int Side = 2;
    public const int UpSide = 3;
    public const int Up = 4;

    public struct Facing
    {
        public int Row;      // 0-4, as above
        public bool Mirror;  // facing left
        public bool Away;    // facing up the screen (a held gun goes behind the body)
    }

    // The screen angle of a world direction: 0 = up the screen, 90 = right, 180 = down.
    public static float ScreenAngle(Vector3 direction, Transform view)
    {
        direction.y = 0f;
        float x = Vector3.Dot(direction, view.right);
        Vector3 up = Vector3.ProjectOnPlane(view.up, Vector3.up);
        float y = Vector3.Dot(direction, up.sqrMagnitude > 0.0001f ? up.normalized : Vector3.forward);
        return Mathf.Atan2(x, y) * Mathf.Rad2Deg;
    }

    public static Facing For(Vector3 direction, Transform view)
    {
        float angle = ScreenAngle(direction, view);
        int sector = Mathf.RoundToInt(Mathf.Repeat(angle, 360f) / 45f) % 8;

        // 0 up, 1 up-right, 2 right, 3 down-right, 4 down, 5 down-left, 6 left, 7 up-left.
        switch (sector)
        {
            case 0: return new Facing { Row = Up, Away = true };
            case 1: return new Facing { Row = UpSide, Away = true };
            case 2: return new Facing { Row = Side };
            case 3: return new Facing { Row = DownSide };
            case 4: return new Facing { Row = Down };
            case 5: return new Facing { Row = DownSide, Mirror = true };
            case 6: return new Facing { Row = Side, Mirror = true };
            default: return new Facing { Row = UpSide, Mirror = true, Away = true };
        }
    }
}
