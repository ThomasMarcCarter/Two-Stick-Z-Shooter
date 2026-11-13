using UnityEngine;

// The elite side of Enemy: SetElite applies the kind's numbers (Elites) and look, and the
// elite's kill count and death blast are raised from Die via OnEliteDied.
public partial class Enemy
{
    public EliteKind Elite { get; private set; }

    // Called once after Initialise/SetKind. Never for bosses.
    public void SetElite(EliteKind kind)
    {
        if (kind == EliteKind.None || Elite != EliteKind.None)
            return;

        Elite = kind;

        if (kind == EliteKind.Fast)
            moveSpeed *= Elites.FastSpeed;
        else if (kind == EliteKind.Armoured)
        {
            maxHealth = Mathf.Max(1, Mathf.RoundToInt(maxHealth * Elites.ArmouredHealth));
            health = maxHealth;
            knockbackScale *= Elites.ArmouredKnockback;
        }

        Color colour = Elites.Tint(kind);
        tint = Color.Lerp(Color.white, colour, 0.6f);
        emission = colour * 0.2f;
        ApplyTint();
        EliteMarker.Create(this, colour);
    }

    // The tint laid over a sprite body (white for normal zombies).
    private Color SpriteTint => Elite == EliteKind.None ? Color.white : tint ?? Color.white;

    private void OnEliteDied()
    {
        MetaProgress.AddStat(Stat.EliteKills);

        if (Elite == EliteKind.Explosive)
        {
            Blast.Settings burst = PlayerItems.SmallBlast(3);
            burst.Theirs = true;
            Blast.Explode(transform.position, room != null ? room.transform : null, burst);
        }
    }
}
