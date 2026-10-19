using Ascua.Controls;
using Unity.Cinemachine;
using UnityEngine;

namespace Ascua.Player
{
    /// <summary>
    /// Alimenta la cámara Cinemachine (OrbitalFollow) con el PlayerInputReader:
    /// órbita con ratón / stick derecho y distancia ajustable con la rueda o la cruceta.
    /// La colisión con la geometría la resuelve el CinemachineDeoccluder del mismo objeto.
    /// </summary>
    [RequireComponent(typeof(CinemachineOrbitalFollow))]
    public class ThirdPersonCameraRig : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader input;

        [Header("Distancia")]
        [SerializeField] private float minDistance = 2.5f;
        [SerializeField] private float maxDistance = 8f;
        [SerializeField] private float startDistance = 5f;
        [SerializeField] private float zoomStep = 0.75f;
        [SerializeField] private float zoomSmoothing = 10f;

        [Header("Inclinación (grados)")]
        [SerializeField] private float minPitch = -15f;
        [SerializeField] private float maxPitch = 65f;
        [SerializeField] private float startPitch = 14f;

        private CinemachineOrbitalFollow orbital;
        private float targetDistance;

        private void Awake()
        {
            orbital = GetComponent<CinemachineOrbitalFollow>();
            if (input == null) input = FindFirstObjectByType<PlayerInputReader>();

            targetDistance = Mathf.Clamp(startDistance, minDistance, maxDistance);
            orbital.Radius = targetDistance;

            InputAxis vertical = orbital.VerticalAxis;
            vertical.Range = new Vector2(minPitch, maxPitch);
            vertical.Value = startPitch;
            vertical.Center = startPitch;
            orbital.VerticalAxis = vertical;
        }

        private void Update()
        {
            if (input == null) return;

            Vector2 look = input.LookDelta;

            InputAxis horizontal = orbital.HorizontalAxis;
            horizontal.Value = Mathf.Repeat(horizontal.Value + look.x + 180f, 360f) - 180f;
            orbital.HorizontalAxis = horizontal;

            // Ratón arriba = mirar arriba = la cámara baja en la órbita.
            InputAxis vertical = orbital.VerticalAxis;
            vertical.Value = Mathf.Clamp(vertical.Value - look.y, minPitch, maxPitch);
            orbital.VerticalAxis = vertical;

            targetDistance = Mathf.Clamp(targetDistance + input.ZoomDelta * zoomStep, minDistance, maxDistance);
            orbital.Radius = Mathf.Lerp(orbital.Radius, targetDistance, 1f - Mathf.Exp(-zoomSmoothing * Time.deltaTime));
        }
    }
}
