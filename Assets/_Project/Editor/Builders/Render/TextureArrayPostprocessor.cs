// Import settings for the R74 painted texture arrays under Content/Textures (look-dev 2026-10-12). Authority: none (editor).
// Each source PNG is a flipbook sheet of 512 x 512 tiles imported as a Texture2DArray (layer 0 = the top-left tile, row by row), so a fresh clone
// imports correctly before any builder runs. Layer 0 of every array is neutral (detail 0.5 / albedo white): FishGame/Toon reads the layer from uv1.y,
// and a mesh with no uv1.y is untouched.
//   T_EnvDetailArray.png  4 x 4, linear (detail centred on 0.5; the material gain is 2). Layers: 0 neutral, 1 grass, 2 foliage, 3 wood (grain
//                         along u), 4 bark (ridges along v), 5 sand, 6 rock, 7 cloth, 8..15 neutral (reserved). Generator: Art/Blender/
//                         fishgame_lookdev.blend, text block fg_textures.py.
//   T_FishSkinArray.png   4 x 2, sRGB albedo. Layers: 0 white, 1 bluegill, 2 perch, 3 carp, 4 trout, 5 catfish, 6..7 white (one tile per species skin; M3 grows the sheet).
// Bump GetVersion when Apply changes, so the textures reimport.
using UnityEditor;
using UnityEngine;

namespace FishGame.EditorTools
{
    public sealed class TextureArrayPostprocessor : AssetPostprocessor
    {
        internal readonly struct ArraySpec
        {
            public readonly string Path;
            public readonly int Columns, Rows;
            public readonly bool Srgb;
            public ArraySpec(string path, int columns, int rows, bool srgb) { Path = path; Columns = columns; Rows = rows; Srgb = srgb; }
        }

        internal static readonly ArraySpec[] Arrays =
        {
            new ArraySpec(AssetPaths.EnvDetailArray, 4, 4, false),
            new ArraySpec(AssetPaths.FishSkinArray, 4, 2, true),
        };

        public override uint GetVersion() => 1;

        void OnPreprocessTexture()
        {
            foreach (ArraySpec spec in Arrays)
                if (assetPath == spec.Path) Apply((TextureImporter)assetImporter, spec);
        }

        // Returns true when any setting changed.
        internal static bool Apply(TextureImporter importer, ArraySpec spec)
        {
            bool changed = false;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (settings.textureType != TextureImporterType.Default) { settings.textureType = TextureImporterType.Default; changed = true; }
            if (settings.textureShape != TextureImporterShape.Texture2DArray) { settings.textureShape = TextureImporterShape.Texture2DArray; changed = true; }
            if (settings.flipbookColumns != spec.Columns) { settings.flipbookColumns = spec.Columns; changed = true; }
            if (settings.flipbookRows != spec.Rows) { settings.flipbookRows = spec.Rows; changed = true; }
            if (settings.sRGBTexture != spec.Srgb) { settings.sRGBTexture = spec.Srgb; changed = true; }
            if (!settings.mipmapEnabled) { settings.mipmapEnabled = true; changed = true; }
            if (settings.alphaSource != TextureImporterAlphaSource.None) { settings.alphaSource = TextureImporterAlphaSource.None; changed = true; }
            if (settings.wrapMode != TextureWrapMode.Repeat) { settings.wrapMode = TextureWrapMode.Repeat; changed = true; }
            if (settings.filterMode != FilterMode.Trilinear) { settings.filterMode = FilterMode.Trilinear; changed = true; }
            if (settings.aniso != 4) { settings.aniso = 4; changed = true; }
            if (changed) importer.SetTextureSettings(settings);
            if (importer.textureCompression != TextureImporterCompression.CompressedHQ) { importer.textureCompression = TextureImporterCompression.CompressedHQ; changed = true; }
            if (importer.maxTextureSize != 2048) { importer.maxTextureSize = 2048; changed = true; }
            if (importer.isReadable) { importer.isReadable = false; changed = true; }
            return changed;
        }

        // Applies the settings to both sources and reimports only the ones that changed. Missing sources throw (the materials need them).
        internal static void EnsureImportSettings()
        {
            foreach (ArraySpec spec in Arrays)
            {
                var importer = AssetImporter.GetAtPath(spec.Path) as TextureImporter;
                if (importer == null) throw new System.IO.FileNotFoundException("Missing texture array source " + spec.Path);
                if (Apply(importer, spec)) importer.SaveAndReimport();
            }
        }

        internal static Texture2DArray Load(string path)
        {
            var array = AssetDatabase.LoadAssetAtPath<Texture2DArray>(path);
            if (array == null) throw new System.InvalidOperationException("Not imported as a Texture2DArray: " + path);
            return array;
        }
    }
}
