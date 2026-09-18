using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ZLZ.Hub
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Unity imports a Default-type texture with "Non-Power of 2 : ToNearest",
    //  which quietly resamples a 1440x960 banner into a 1024x1024 square. The art
    //  then draws horizontally squashed and nothing in the layout code can tell,
    //  because Texture2D.width/height report the square size as the truth.
    //
    //  Banner art is UI, not a material map. It wants its real dimensions, a mip
    //  chain (the rail card draws a 1440px image into a ~150px box, which without
    //  mips samples full-res pixels at random and comes out grainy), and no block
    //  compression (DXT wrecks flat gradients and the hard edges of logo text).
    //
    //  Named ZLZ_Banner_* so this only ever touches our own art and never a
    //  customer's textures.
    // ─────────────────────────────────────────────────────────────────────────
    internal class ZLZ_HubBannerImporter : AssetPostprocessor
    {
        const string k_Prefix = "ZLZ_Banner_";

        void OnPreprocessTexture()
        {
            string name = Path.GetFileNameWithoutExtension(assetPath);
            if (string.IsNullOrEmpty(name) || !name.StartsWith(k_Prefix, StringComparison.Ordinal)) return;

            var importer = assetImporter as TextureImporter;
            if (importer == null) return;

            importer.npotScale          = TextureImporterNPOTScale.None;
            importer.mipmapEnabled      = true;
            importer.filterMode         = FilterMode.Trilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode           = TextureWrapMode.Clamp;
        }
    }
}
