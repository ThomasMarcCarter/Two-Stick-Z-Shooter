using System.Collections;
using UnityEngine;

// The pustule: weak, slow and covered in glowing boils, it just shambles in and claws. Killing it
// is the danger: a moment after it dies it bursts into a ring of bile globs (7, or 10 from floor
// 3), so it's better shot from range than finished up close. It bursts once, however it died,
// unless it was the last one in the room.
[RequireComponent(typeof(Enemy))]
public class EnemyPustule : MonoBehaviour
{
    private const float Delay = 0.3f;
    private const float Speed = 5f;

    public int Floor { get; set; } = 1;

    private Enemy enemy;
    private bool burst;

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
    }

    private void OnEnable()
    {
        enemy.Died += OnDied;
    }

    private void OnDisable()
    {
        enemy.Died -= OnDied;
    }

    private void OnDied(Enemy _)
    {
        if (burst)
            return;

        burst = true;

        // Run on its own object: the body may be gone before the burst.
        GameObject timer = new GameObject("Pustule Burst");
        Transform room = enemy.Room != null ? enemy.Room.transform : null;
        timer.transform.SetParent(room, false);
        timer.transform.position = transform.position;
        timer.AddComponent<Burst>().Begin(Floor >= 3 ? 10 : 7, room);
    }

    private class Burst : MonoBehaviour
    {
        public void Begin(int count, Transform room)
        {
            StartCoroutine(Run(count, room));
        }

        private IEnumerator Run(int count, Transform room)
        {
            yield return new WaitForSeconds(Delay);

            // The last one in the room: the fight's over (the room clearing wipes bullets), so
            // it just pops.
            Room owner = room != null ? room.GetComponent<Room>() : null;

            if (owner != null && !owner.IsLocked)
            {
                Destroy(gameObject);
                yield break;
            }

            EnemyBullet.Ring(transform.position, count, Random.Range(0f, 360f), Speed, EnemyBullet.Look.Bile, room);
            SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("bloater.swell", 2, ProceduralAudio.BloaterSwell)), transform.position, 0.8f, 1.3f);
            Destroy(gameObject);
        }
    }
}
