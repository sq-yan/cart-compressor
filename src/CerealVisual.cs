using System.IO;
using System.Reflection;
using UnityEngine;

namespace CartCompressor
{
    internal static class CerealVisual
    {
        private static Texture2D artwork;
        internal static void Apply(GameObject prefab)
        {
            if (artwork == null)
            {
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("CartCompressor.CartCrunch.png"))
                {
                    if (stream == null) throw new FileNotFoundException("Embedded cereal artwork missing");
                    using (var buffer = new MemoryStream())
                    {
                        stream.CopyTo(buffer);
                        artwork = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                        if (!ImageConversion.LoadImage(artwork, buffer.ToArray())) throw new IOException("Invalid cereal texture");
                    }
                }
                artwork.name = "Cart Crunch Cereal Albedo";
                artwork.wrapMode = TextureWrapMode.Clamp;
                artwork.filterMode = FilterMode.Bilinear;
                Object.DontDestroyOnLoad(artwork);
            }
            // Keep the exact vanilla cereal mesh, UVs, colliders, sounds and animations.
            var root = prefab.transform.Find("Mesh");
            if (root == null) throw new MissingComponentException("Vanilla cereal mesh not found");
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i] = new Material(materials[i]);
                    if (materials[i].HasProperty("_MainTex")) materials[i].SetTexture("_MainTex", artwork);
                    if (materials[i].HasProperty("_BaseMap")) materials[i].SetTexture("_BaseMap", artwork);
                    if (materials[i].HasProperty("_BaseColorMap")) materials[i].SetTexture("_BaseColorMap", artwork);
                }
                renderer.sharedMaterials = materials;
            }
        }
    }
}
