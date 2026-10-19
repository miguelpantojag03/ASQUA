using UnityEngine;
using UnityEngine.Rendering;

namespace Ascua.World
{
    /// <summary>
    /// Aplica niebla, luz ambiental y color de fondo de cámara de la escena.
    /// Funciona en editor y en Play, así se puede ajustar la atmósfera en vivo desde el Inspector
    /// sin depender de hornear iluminación.
    /// </summary>
    [ExecuteAlways]
    public class AtmosphereSettings : MonoBehaviour
    {
        [Header("Niebla")]
        [SerializeField] private bool fogEnabled = true;
        [SerializeField] private Color fogColor = new Color(0.102f, 0.137f, 0.314f);
        [SerializeField] private FogMode fogMode = FogMode.ExponentialSquared;
        [SerializeField, Range(0f, 0.1f)] private float fogDensity = 0.028f;

        [Header("Luz ambiental (cielo / horizonte / suelo)")]
        [SerializeField] private Color ambientSky = new Color(0.16f, 0.2f, 0.38f);
        [SerializeField] private Color ambientEquator = new Color(0.1f, 0.12f, 0.25f);
        [SerializeField] private Color ambientGround = new Color(0.05f, 0.06f, 0.12f);
        [SerializeField, Range(0f, 3f)] private float ambientIntensity = 1f;

        [Header("Cámara")]
        [Tooltip("Pinta el fondo de la cámara con el color de la niebla para que el horizonte se funda.")]
        [SerializeField] private bool matchCameraBackground = true;
        [SerializeField] private Camera targetCamera;

        /// <summary>Configuración rápida desde el constructor de escenas.</summary>
        public void Configure(Color newFogColor, float density, Color sky, Color equator, Color ground, Camera cameraToTint)
        {
            fogColor = newFogColor;
            fogDensity = density;
            ambientSky = sky;
            ambientEquator = equator;
            ambientGround = ground;
            targetCamera = cameraToTint;
            Apply();
        }

        private void OnEnable()
        {
            Apply();
        }

        private void OnValidate()
        {
            Apply();
        }

        /// <summary>Vuelca los valores a RenderSettings.</summary>
        public void Apply()
        {
            RenderSettings.fog = fogEnabled;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogMode = fogMode;
            RenderSettings.fogDensity = fogDensity;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = ambientSky * ambientIntensity;
            RenderSettings.ambientEquatorColor = ambientEquator * ambientIntensity;
            RenderSettings.ambientGroundColor = ambientGround * ambientIntensity;

            // Sonda ambiental explícita: garantiza luz ambiente sin hornear iluminación.
            SphericalHarmonicsL2 sh = new SphericalHarmonicsL2();
            sh.AddAmbientLight(ambientEquator * ambientIntensity);
            sh.AddDirectionalLight(Vector3.up, ambientSky * ambientIntensity, 0.5f);
            sh.AddDirectionalLight(Vector3.down, ambientGround * ambientIntensity, 0.5f);
            RenderSettings.ambientProbe = sh;

            if (matchCameraBackground)
            {
                Camera cam = targetCamera != null ? targetCamera : Camera.main;
                if (cam != null)
                {
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = fogColor;
                }
            }
        }
    }
}
