using System.Collections.Generic;
using UnityEngine;

namespace Ascua.Combat
{
    /// <summary>De dónde viene un golpe de luz (para efectos y reglas especiales).</summary>
    public enum HitKind
    {
        /// <summary>Barrido de luz del combo (1º y 2º golpe).</summary>
        Barrido,
        /// <summary>3er golpe del combo: más daño y empuje.</summary>
        Remate,
        /// <summary>Ataque cargado (Recuerdo: Lupa de Mina).</summary>
        Cargado,
        /// <summary>Contraataque (Recuerdo: Reloj de Nadir).</summary>
        Contraataque,
        /// <summary>Pulso de luz en área.</summary>
        Pulso,
        /// <summary>Vela explosiva de Teo (Vínculo 5).</summary>
        VelaTeo,
        /// <summary>Brasa guardiana de Mina (Vínculo 5).</summary>
        Brasa
    }

    /// <summary>Datos de un golpe de luz. No es "daño físico": es luz que deshace la sombra.</summary>
    public struct HitInfo
    {
        public int Damage;
        public float Knockback;
        public float StunSeconds;
        public Vector3 SourcePosition;
        public HitKind Kind;

        /// <summary>Golpes fuertes: rompen guardias, dividen Ecos y lanzan el finisher.</summary>
        public bool IsStrong => Kind != HitKind.Barrido && Kind != HitKind.Brasa;

        public HitInfo(int damage, float knockback, Vector3 source, HitKind kind, float stun = 0f)
        {
            Damage = damage;
            Knockback = knockback;
            StunSeconds = stun;
            SourcePosition = source;
            Kind = kind;
        }
    }

    /// <summary>Todo lo que la luz de Ilse puede alcanzar: criaturas, proyectiles y el Heraldo.</summary>
    public interface ILightTarget
    {
        Vector3 TargetPosition { get; }
        bool CanBeHit { get; }

        /// <summary>Recibe un golpe. Devuelve true si "conectó" (cuenta para el combo).</summary>
        bool ReceiveLight(HitInfo hit);
    }

    /// <summary>
    /// Registro de objetivos activos. Evita FindObjectsOfType en cada golpe: los ataques
    /// recorren esta lista corta (normalmente menos de 10 elementos).
    /// </summary>
    public static class LightTargetRegistry
    {
        private static readonly List<ILightTarget> targets = new List<ILightTarget>();
        private static readonly List<ILightTarget> buffer = new List<ILightTarget>();

        public static IReadOnlyList<ILightTarget> All => targets;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            targets.Clear();
            buffer.Clear();
        }

        public static void Register(ILightTarget target)
        {
            if (!targets.Contains(target)) targets.Add(target);
        }

        public static void Unregister(ILightTarget target)
        {
            targets.Remove(target);
        }

        /// <summary>
        /// Objetivos dentro de una esfera. Devuelve una lista compartida (no guardarla):
        /// se copia para que un objetivo pueda desregistrarse mientras se recorre.
        /// </summary>
        public static List<ILightTarget> InSphere(Vector3 center, float radius)
        {
            buffer.Clear();
            float r2 = radius * radius;
            for (int i = 0; i < targets.Count; i++)
            {
                ILightTarget t = targets[i];
                if (t == null || !t.CanBeHit) continue;
                if ((t.TargetPosition - center).sqrMagnitude <= r2) buffer.Add(t);
            }
            return buffer;
        }

        /// <summary>El objetivo vivo más cercano dentro de un cono frente a 'forward' (para apuntar suave).</summary>
        public static ILightTarget NearestInCone(Vector3 origin, Vector3 forward, float range, float halfAngle)
        {
            ILightTarget best = null;
            float bestDistance = range;
            forward.y = 0f;
            forward.Normalize();
            for (int i = 0; i < targets.Count; i++)
            {
                ILightTarget t = targets[i];
                if (t == null || !t.CanBeHit) continue;
                Vector3 to = t.TargetPosition - origin;
                to.y = 0f;
                float d = to.magnitude;
                if (d > bestDistance || d < 0.01f) continue;
                if (Vector3.Angle(forward, to) > halfAngle) continue;
                bestDistance = d;
                best = t;
            }
            return best;
        }
    }
}
