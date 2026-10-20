using System.Collections.Generic;
using UnityEngine;

namespace Ascua.Quests
{
    /// <summary>
    /// Punto con nombre en la escena (destinos de escolta, lugares de misión).
    /// Las misiones lo referencian por id, así los assets no dependen de objetos de la escena.
    /// </summary>
    public class QuestPoint : MonoBehaviour
    {
        private static readonly List<QuestPoint> all = new List<QuestPoint>();

        [SerializeField] private string pointId = "punto";

        public string PointId => pointId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            all.Clear();
        }

        public void Configure(string id)
        {
            pointId = id;
        }

        private void OnEnable()
        {
            if (!all.Contains(this)) all.Add(this);
        }

        private void OnDisable()
        {
            all.Remove(this);
        }

        /// <summary>Busca un punto por id (null si no existe).</summary>
        public static QuestPoint Find(string id)
        {
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].pointId == id) return all[i];
            }
            return null;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.77f, 0.42f, 0.9f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.2f, 0.6f);
        }
    }
}
