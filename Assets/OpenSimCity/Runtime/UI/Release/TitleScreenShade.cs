using UnityEngine;
using UnityEngine.UIElements;

namespace OpenSimCity.UI.Release
{
    /// <summary>Resolution-independent directional scrim; keeps the city render intact.</summary>
    internal sealed class TitleScreenShade : VisualElement
    {
        public TitleScreenShade()
        {
            generateVisualContent += Paint;
        }

        private void Paint(MeshGenerationContext context)
        {
            var rect = contentRect;
            if (rect.width <= 0 || rect.height <= 0) return;
            var mesh = context.Allocate(8, 18);
            var stops = new[] { 0f, 0.35f, 0.70f, 1f };
            var alpha = new[] { 0.94f, 0.83f, 0.23f, 0.06f };
            for (var index = 0; index < 4; index++)
            {
                var color = new Color(0.035f, 0.042f, 0.045f, alpha[index]);
                mesh.SetNextVertex(new Vertex { position = new Vector3(rect.width * stops[index], 0, Vertex.nearZ), tint = color });
                mesh.SetNextVertex(new Vertex { position = new Vector3(rect.width * stops[index], rect.height, Vertex.nearZ), tint = color });
            }
            for (ushort index = 0; index < 6; index += 2)
            {
                mesh.SetNextIndex(index); mesh.SetNextIndex((ushort)(index + 2)); mesh.SetNextIndex((ushort)(index + 1));
                mesh.SetNextIndex((ushort)(index + 1)); mesh.SetNextIndex((ushort)(index + 2)); mesh.SetNextIndex((ushort)(index + 3));
            }
        }
    }
}
