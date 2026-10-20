using UnityEngine;
using UnityEngine.UI;
using static MapMesh;

// Player health as a row of segments, in the same phosphor style as the radar. Segments go red
// when health is low, and the whole bar flashes when the player is hit. Also puts the pulsing
// red low-health glow round the screen edges (LowHealthVignette) on the canvas.
public class HealthDisplay : MaskableGraphic
{
    [SerializeField] private PlayerHealth playerHealth;

    [Header("Look")]
    [SerializeField] private float segmentGap = 4f;
    [Tooltip("At or below this much health the bar turns red.")]
    [SerializeField] private int lowHealth = 2;
    [SerializeField] private float hitFlashTime = 0.25f;

    private int lastHealth = -1;
    private float flashUntil;
    private LowHealthVignette vignette;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        if (playerHealth != null)
            playerHealth.Damaged += OnDamaged;
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        if (playerHealth != null)
            playerHealth.Damaged -= OnDamaged;
    }

    private void OnDamaged(PlayerHealth health)
    {
        flashUntil = Time.unscaledTime + hitFlashTime;
    }

    private void Update()
    {
        if (!Application.isPlaying || playerHealth == null)
            return;

        if (vignette == null && canvas != null)
            vignette = LowHealthVignette.Create(canvas.rootCanvas.transform, playerHealth);

        // Redraw when health changes, and every frame while flashing.
        if (playerHealth.Health != lastHealth || Time.unscaledTime < flashUntil + 0.05f)
        {
            lastHealth = playerHealth.Health;
            SetVerticesDirty();
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect bounds = rectTransform.rect;
        int max = playerHealth != null ? playerHealth.MaxHealth : 6;
        int health = playerHealth != null && Application.isPlaying ? playerHealth.Health : max;

        bool flashing = Time.unscaledTime < flashUntil;
        Color full = flashing ? Color.white : health <= lowHealth ? UiColours.Hostile : UiColours.Phosphor;

        float width = (bounds.width - segmentGap * (max - 1)) / max;

        for (int i = 0; i < max; i++)
        {
            Rect segment = new Rect(bounds.xMin + i * (width + segmentGap), bounds.yMin, width, bounds.height);

            if (i < health)
            {
                FillRect(vh, segment, full);
            }
            else
            {
                FillRect(vh, segment, Fade(full == Color.white ? UiColours.Phosphor : full, 0.08f));
                OutlineRect(vh, segment, 1f, Fade(full == Color.white ? UiColours.Phosphor : full, 0.45f));
            }
        }
    }
}
