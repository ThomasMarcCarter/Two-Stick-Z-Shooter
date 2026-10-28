using UnityEngine;

// Wires hanging out of a broken wall panel (room life, 2026-10-28; the LooseCable sprite) that
// short every few seconds: a shower of pixel sparks bouncing on the floor (LightAtmosphere.Sparks),
// an orange flash on the wall and a crackle (ProceduralAudio.ElectricCrackle); now and then a
// second short right after. Only an effect: it hurts no one. Placed by FloorGenerator.RoomLife.cs.
public class SparkingCable : MonoBehaviour
{
    private ParticleSystem sparks;
    private Light flash;
    private float next;
    private float flashUntil;

    // `wires` is where the bare ends hang, on the wall face; sparks spray out from the wall (`outward`).
    public static SparkingCable Create(Transform parent, Vector3 wires, Vector3 outward)
    {
        GameObject root = new GameObject("Sparking Cable");
        root.transform.SetParent(parent, false);
        root.transform.SetPositionAndRotation(wires, Quaternion.LookRotation(outward + Vector3.down * 0.4f));

        SparkingCable cable = root.AddComponent<SparkingCable>();
        cable.sparks = LightAtmosphere.Sparks(root.transform);

        cable.flash = new GameObject("Flash").AddComponent<Light>();
        cable.flash.transform.SetParent(root.transform, false);
        cable.flash.transform.localPosition = Vector3.forward * 0.3f;
        cable.flash.type = LightType.Point;
        cable.flash.color = new Color(1f, 0.7f, 0.35f);
        cable.flash.range = 3.5f;
        cable.flash.intensity = 0f;
        cable.flash.shadows = LightShadows.None;
        cable.next = Time.time + Random.Range(0.5f, 4f);
        return cable;
    }

    private void Update()
    {
        if (flash != null)
            flash.intensity = Time.time < flashUntil ? Random.Range(2f, 5f) : 0f;

        if (Time.time < next)
            return;

        if (sparks != null)
            sparks.Emit(Random.Range(6, 16));

        flashUntil = Time.time + Random.Range(0.06f, 0.18f);
        SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("electric.crackle", 3, ProceduralAudio.ElectricCrackle)), transform.position, 0.35f,
            Random.Range(0.9f, 1.15f));

        // Now and then a quick second short.
        next = Time.time + (Random.value < 0.25f ? Random.Range(0.25f, 0.5f) : Random.Range(2f, 7f));
    }
}
