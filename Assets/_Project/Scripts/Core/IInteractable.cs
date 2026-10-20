using System.Collections.Generic;
using UnityEngine;

namespace Ascua.Core
{
    /// <summary>
    /// Todo lo que Ilse puede usar con la tecla de interacción (E / botón X): personajes con los que
    /// hablar y objetos que recoger. PlayerInteractor elige el más cercano y muestra su aviso.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>Punto de referencia para medir la distancia.</summary>
        Vector3 InteractionPoint { get; }
        /// <summary>Verbo del aviso: "Hablar con", "Recoger"...</summary>
        string InteractionVerb { get; }
        /// <summary>Nombre del aviso: "Mina", "Postal de la abuela"...</summary>
        string InteractionName { get; }
        /// <summary>Segundos que hay que mantener la tecla (0 = pulsar).</summary>
        float InteractionHoldTime { get; }
        /// <summary>Distancia máxima para interactuar.</summary>
        float InteractionRadius { get; }
        bool CanInteract { get; }
        void Interact(Transform interactor);
    }

    /// <summary>Registro global de cosas con las que se puede interactuar.</summary>
    public static class InteractableRegistry
    {
        private static readonly List<IInteractable> all = new List<IInteractable>();

        public static IReadOnlyList<IInteractable> All => all;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            all.Clear();
        }

        public static void Register(IInteractable interactable)
        {
            if (interactable != null && !all.Contains(interactable)) all.Add(interactable);
        }

        public static void Unregister(IInteractable interactable)
        {
            all.Remove(interactable);
        }
    }
}
