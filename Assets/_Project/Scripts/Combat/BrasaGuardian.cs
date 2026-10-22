using Ascua.Core;
using UnityEngine;

namespace Ascua.Combat
{
    /// <summary>
    /// Brasa guardiana (Vínculo 5 con Mina): una chispa de Brasa acompaña a Ilse, orbitando su Farol.
    /// Cada 2.5 s, si hay una sombra a menos de 7 m, se lanza contra ella (1 de daño, golpe ligero)
    /// y vuelve. Es pasiva: Mina "presta" a Brasa para proteger a quien le devolvió la luz.
    /// </summary>
    public class BrasaGuardian : MonoBehaviour
    {
        [SerializeField] private Transform orbitCenter;
        [SerializeField] private float orbitRadius = 0.7f;
        [SerializeField] private float orbitHeight = 1.5f;
        [SerializeField] private float orbitSpeed = 2.2f;
        [SerializeField] private float attackInterval = 2.5f;
        [SerializeField] private float attackRange = 7f;
        [SerializeField] private float strikeSpeed = 16f;
        [SerializeField] private ParticleSystem trail;

        private float angle;
        private float cooldown;
        private ILightTarget target;
        private bool returning;

        private void OnEnable()
        {
            cooldown = attackInterval;
            target = null;
            returning = false;
            if (trail != null) trail.Play();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || orbitCenter == null) return;

            angle += orbitSpeed * dt;
            Vector3 home = orbitCenter.position + new Vector3(Mathf.Cos(angle) * orbitRadius, orbitHeight + Mathf.Sin(angle * 2f) * 0.1f, Mathf.Sin(angle) * orbitRadius);

            if (target != null)
            {
                if (!target.CanBeHit)
                {
                    target = null;
                    returning = true;
                }
                else
                {
                    transform.position = Vector3.MoveTowards(transform.position, target.TargetPosition, strikeSpeed * dt);
                    if ((transform.position - target.TargetPosition).sqrMagnitude < 0.09f)
                    {
                        target.ReceiveLight(new HitInfo(1, 1f, orbitCenter.position, HitKind.Brasa, 0.3f));
                        EventBus.RaiseAbilityUsed("brasa_golpe");
                        target = null;
                        returning = true;
                    }
                    return;
                }
            }

            if (returning)
            {
                transform.position = Vector3.MoveTowards(transform.position, home, strikeSpeed * 0.6f * dt);
                if ((transform.position - home).sqrMagnitude < 0.04f) returning = false;
                return;
            }

            transform.position = Vector3.Lerp(transform.position, home, 1f - Mathf.Exp(-10f * dt));
            cooldown -= dt;
            if (cooldown > 0f) return;
            cooldown = attackInterval;

            // La sombra más cercana en cualquier dirección.
            ILightTarget best = null;
            float bestDistance = attackRange * attackRange;
            var all = LightTargetRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                ILightTarget t = all[i];
                if (t == null || !t.CanBeHit || !(t is ShadowCreature)) continue;
                float d = (t.TargetPosition - orbitCenter.position).sqrMagnitude;
                if (d >= bestDistance) continue;
                bestDistance = d;
                best = t;
            }
            target = best;
        }
    }
}
