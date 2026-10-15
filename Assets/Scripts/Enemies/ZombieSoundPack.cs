using UnityEngine;

// One zombie's voice: groans while it's alive, a pained grunt when it's hit, and a death sound.
// Each enemy picks a pack when it spawns (FloorGenerator's Enemy Sound Packs), and EnemyVoice
// plays a random clip from the right list. Create more with Assets > Create > Roguelike >
// Zombie Sound Pack.
[CreateAssetMenu(menuName = "Roguelike/Zombie Sound Pack", fileName = "ZombieSoundPack")]
public class ZombieSoundPack : ScriptableObject
{
    [Tooltip("Groans and growls played now and then while the enemy is alive.")]
    public AudioClip[] alive = new AudioClip[0];
    [Tooltip("Played when the enemy takes damage (but not on the killing blow).")]
    public AudioClip[] hit = new AudioClip[0];
    [Tooltip("Played when the enemy dies.")]
    public AudioClip[] death = new AudioClip[0];

    [Header("Mix")]
    [Range(0f, 1f)] public float aliveVolume = 0.5f;
    [Range(0f, 1f)] public float hitVolume = 0.7f;
    [Range(0f, 1f)] public float deathVolume = 0.8f;

    public static AudioClip Pick(AudioClip[] clips)
    {
        return clips == null || clips.Length == 0 ? null : clips[Random.Range(0, clips.Length)];
    }
}
