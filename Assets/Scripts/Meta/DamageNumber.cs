using TMPro;
using UnityEngine;

// A damage number that pops up where something was hit, drifts up and fades, facing the camera.
// In the phosphor style of the UI. Used by the hideout's training dummy.
public class DamageNumber : MonoBehaviour
{
    private const float Life = 0.8f;

    private TextMeshPro text;
    private float born;
    private Vector3 drift;

    public static void Show(Vector3 position, int damage)
    {
        GameObject go = new GameObject("Damage Number");
        go.transform.position = position;

        DamageNumber number = go.AddComponent<DamageNumber>();
        number.text = go.AddComponent<TextMeshPro>();
        number.text.text = damage.ToString();
        number.text.fontSize = 3.2f + damage * 0.6f;
        number.text.alignment = TextAlignmentOptions.Center;
        number.text.color = damage >= 3 ? UiColours.Amber : UiColours.Phosphor;
        number.text.textWrappingMode = TextWrappingModes.NoWrap;
        number.text.rectTransform.sizeDelta = new Vector2(3f, 1f);
        number.born = Time.time;
        number.drift = new Vector3(Random.Range(-0.3f, 0.3f), 1.2f, Random.Range(-0.3f, 0.3f));
    }

    private void LateUpdate()
    {
        float age = Time.time - born;

        if (age >= Life)
        {
            Destroy(gameObject);
            return;
        }

        transform.position += drift * Time.deltaTime * (1f - age / Life);

        Camera camera = Camera.main;

        if (camera != null)
            transform.rotation = camera.transform.rotation;

        // Hidden under menus and screens, like the other labels over the world.
        text.enabled = !GameFlow.ScreenOverWorld;

        Color colour = text.color;
        colour.a = 1f - Mathf.Pow(age / Life, 2f);
        text.color = colour;
    }
}
