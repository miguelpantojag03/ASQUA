using UnityEngine;

namespace Ascua.Combat
{
    /// <summary>
    /// Eco: una sombra que repite. Al recibir su primer golpe FUERTE (remate del combo, ataque cargado,
    /// Pulso, vela de Teo) se parte en 2 copias pequeñas de 1 de vida que salen disparadas a los lados.
    /// Obliga a variar el ritmo: rematar pronto multiplica enemigos; golpes sueltos (1º-2º del combo)
    /// lo desgastan sin partirlo. Las copias usan un pool propio (no se instancia nada).
    /// </summary>
    public class EchoShade : ShadowCreature
    {
        [Header("Eco")]
        [Tooltip("True en las copias pequeñas (no se vuelven a dividir).")]
        [SerializeField] private bool isSplitCopy;
        [SerializeField] private float moveSpeed = 2.6f;
        [SerializeField] private float attackRange = 1.1f;
        [Tooltip("Copias pequeñas disponibles (compartidas entre todos los Ecos de la escena).")]
        [SerializeField] private EchoShade[] splitPool;
        [SerializeField] private Transform body;

        private float wobbleSeed;

        public bool IsSplitCopy => isSplitCopy;

        protected override void Awake()
        {
            base.Awake();
            typeId = isSplitCopy ? "eco_copia" : "eco";
            wobbleSeed = Random.value * 10f;
        }

        protected override void Update()
        {
            base.Update();
            // Silueta que "tiembla" como un reflejo: se desdobla levemente todo el tiempo.
            if (body != null)
            {
                float s = 1f + Mathf.Sin((Time.time + wobbleSeed) * 9f) * 0.04f;
                body.localScale = new Vector3(s, 2f - s, s);
                body.localRotation = Quaternion.Euler(0f, Time.time * 40f, 0f);
            }
        }

        protected override void Tick(float dt)
        {
            ChasePlayer(moveSpeed, attackRange * 0.8f);
            FacePlayer(dt);
            if (FlatToPlayer().magnitude <= attackRange) TryContactDamage();
        }

        protected override void OnHit(HitInfo hit)
        {
            if (isSplitCopy || !hit.IsStrong || health <= -10) return;

            // Se parte en dos: las copias aparecen a los lados y esta se deshace (sin botín).
            Vector3 side = Vector3.Cross(Vector3.up, (transform.position - hit.SourcePosition).normalized);
            if (side.sqrMagnitude < 0.01f) side = transform.right;
            int spawned = 0;
            if (splitPool != null)
            {
                for (int i = 0; i < splitPool.Length && spawned < 2; i++)
                {
                    EchoShade copy = splitPool[i];
                    if (copy == null || copy.gameObject.activeSelf) continue;
                    Vector3 offset = side * (spawned == 0 ? 1.1f : -1.1f);
                    copy.Spawn(transform.position + offset, ZoneId);
                    spawned++;
                }
            }
            health = -100; // fuerza el Die() de la base (y evita volver a dividirse)
            energyDrop = 0f;
        }
    }
}
