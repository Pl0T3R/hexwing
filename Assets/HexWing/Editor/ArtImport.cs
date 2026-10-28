using UnityEditor;
using UnityEngine;

namespace HexWing.EditorTools
{
    /// <summary>
    /// Import settings for the Blender-made art (Art/blender), so nothing is set by hand:
    /// - Resources/Models/*.fbx: metres as exported, no materials, rig or animation, normals from the file
    ///   (bevels and weighted normals), MikkTSpace tangents (the same space the normal maps were baked in),
    ///   light mesh compression; Read/Write only for collision proxies ("_COL").
    /// - Resources/Textures/*_Albedo (sRGB), *_Mask (linear, alpha = AO), *_Normal (normal map): mipmaps,
    ///   ASTC 6x6 on Android (4x4 for normal maps), at most 2048.
    /// Logs one line per model so the PC session can check that the art arrived.
    /// </summary>
    public class ArtImport : AssetPostprocessor
    {
        const string ModelDir = "Assets/HexWing/Resources/Models/";
        const string TextureDir = "Assets/HexWing/Resources/Textures/";

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(ModelDir)) return;
            var m = (ModelImporter)assetImporter;
            m.globalScale = 1f;
            m.useFileScale = true;
            m.bakeAxisConversion = false;
            m.materialImportMode = ModelImporterMaterialImportMode.None;
            m.animationType = ModelImporterAnimationType.None;
            m.importAnimation = false;
            m.importCameras = false;
            m.importLights = false;
            m.importBlendShapes = false;
            m.importVisibility = false;
            m.importNormals = ModelImporterNormals.Import;
            m.importTangents = ModelImporterTangents.CalculateMikk;
            m.meshCompression = ModelImporterMeshCompression.Low;
            m.isReadable = assetPath.Contains("_COL");
            m.optimizeMeshPolygons = true;
            m.optimizeMeshVertices = true;
            m.weldVertices = true;
            m.addCollider = false;
        }

        void OnPostprocessModel(GameObject root)
        {
            if (!assetPath.StartsWith(ModelDir)) return;
            int meshes = 0, sockets = 0;
            Transform nose = null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.GetComponent<MeshFilter>() != null) meshes++;
                if (t.name.StartsWith("SOCKET_")) sockets++;
                if (t.name == "SOCKET_Nose") nose = t;
            }
            Debug.Log($"[HexWing] Art import {System.IO.Path.GetFileName(assetPath)}: {meshes} meshes, {sockets} sockets.");
            // the Blender scripts model in Unity's frame (z forward); a nose behind the centre means the axes went wrong
            if (nose != null && root.transform.InverseTransformPoint(nose.position).z < 0f)
                Debug.LogWarning($"[HexWing] {assetPath}: SOCKET_Nose points backwards. Check the FBX export axes in Art/blender/lib/export.py.");
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(TextureDir)) return;
            var t = (TextureImporter)assetImporter;
            bool normal = assetPath.EndsWith("_Normal.png");
            bool linear = normal || assetPath.EndsWith("_Mask.png");
            t.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            t.sRGBTexture = !linear;
            t.alphaSource = assetPath.EndsWith("_Mask.png") ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            t.mipmapEnabled = true;
            t.anisoLevel = 2;
            t.wrapMode = TextureWrapMode.Clamp;
            t.maxTextureSize = 2048;
            var android = t.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = 2048;
            android.format = normal ? TextureImporterFormat.ASTC_4x4 : TextureImporterFormat.ASTC_6x6;
            t.SetPlatformTextureSettings(android);
        }
    }
}
