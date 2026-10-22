using UnityEngine;

namespace Ascua.World
{
    /// <summary>
    /// Tinte por instancia con MaterialPropertyBlock: muchos objetos comparten UN material base
    /// (ladrillo, madera, piedra, metal) y cada uno tiene su color sin duplicar materiales.
    /// Se aplica también en el editor para ver el resultado sin dar a Play.
    /// Nota de rendimiento: con MaterialPropertyBlock esos renderers salen del SRP Batcher, pero
    /// siguen usando el batching estático; en Ascua son pocas decenas, coste despreciable.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Renderer))]
    public class SurfaceTint : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Color tint = Color.white;

        private MaterialPropertyBlock block;

        public Color Tint => tint;

        public void Configure(Color color)
        {
            tint = color;
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

        private void Apply()
        {
            Renderer r = GetComponent<Renderer>();
            if (r == null) return;
            r.GetPropertyBlock(block ??= new MaterialPropertyBlock());
            block.SetColor(BaseColorId, tint);
            r.SetPropertyBlock(block);
        }
    }
}
