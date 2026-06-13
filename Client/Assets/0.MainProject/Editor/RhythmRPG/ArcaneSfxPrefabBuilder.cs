using System.IO;
using RhythmRPG.Game.Visual.SceneEffects;
using UnityEditor;
using UnityEngine;

namespace RhythmRPG.EditorTools
{
    public static class ArcaneSfxPrefabBuilder
    {
        private const string ArtFolder = "Assets/0.MainProject/Art/SFX/Arcane";
        private const string PrefabFolder = "Assets/Resources/Prefabs/VFX";
        private const string SoftCirclePath = ArtFolder + "/T_SFX_SoftCircle.png";
        private const string StarPath = ArtFolder + "/T_SFX_StarSpark.png";
        private const string LineMaterialPath = ArtFolder + "/M_SFX_Arcane_Line.mat";
        private const string SoftMaterialPath = ArtFolder + "/M_SFX_Arcane_SoftParticle.mat";
        private const string StarMaterialPath = ArtFolder + "/M_SFX_Arcane_Star.mat";
        private const string DarkCoreMaterialPath = ArtFolder + "/M_SFX_Abyss_Core.mat";

        [MenuItem("Tools/RhythmRPG/SFX/Rebuild Arcane Reference SFX")]
        public static void RebuildArcaneReferenceSfx()
        {
            EnsureFolders();
            WriteTexture(SoftCirclePath, CreateSoftCircleTexture(128));
            WriteTexture(StarPath, CreateStarTexture(128));
            ConfigureTextureImporter(SoftCirclePath);
            ConfigureTextureImporter(StarPath);

            var additiveShader = Shader.Find("RhythmRPG/SFX/Additive Tint");
            var alphaShader = Shader.Find("RhythmRPG/SFX/Alpha Tint");
            if (additiveShader == null || alphaShader == null)
            {
                Debug.LogError("[ArcaneSfxPrefabBuilder] Missing SFX shaders. Refresh assets, then run this menu again.");
                return;
            }

            var softTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(SoftCirclePath);
            var starTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(StarPath);
            var lineMaterial = CreateMaterial(LineMaterialPath, additiveShader, null, new Color(1.8f, 1.8f, 1.8f, 1f), 1f);
            var softMaterial = CreateMaterial(SoftMaterialPath, additiveShader, softTexture, new Color(0f, 2.6f, 1.75f, 1f), 0.92f);
            var starMaterial = CreateMaterial(StarMaterialPath, additiveShader, starTexture, new Color(0.35f, 3.1f, 1.65f, 1f), 0.95f);
            var darkCoreMaterial = CreateMaterial(DarkCoreMaterialPath, alphaShader, null, new Color(0f, 0f, 0.004f, 1f), 1f);

            BuildPrefab(
                "PF_SFX_WaveringAbyssPortal",
                ArcaneSfxPreset.WaveringAbyssPortal,
                ArcaneSfxPlane.XZ,
                2.25f,
                lineMaterial,
                softMaterial,
                starMaterial,
                darkCoreMaterial);

            BuildPrefab(
                "PF_SFX_SparkleSigilRing",
                ArcaneSfxPreset.SparkleSigilRing,
                ArcaneSfxPlane.XZ,
                1.85f,
                lineMaterial,
                softMaterial,
                starMaterial,
                darkCoreMaterial);

            BuildPrefab(
                "PF_SFX_PortalCenterVortex",
                ArcaneSfxPreset.PortalCenterVortex,
                ArcaneSfxPlane.XY,
                1.35f,
                lineMaterial,
                softMaterial,
                starMaterial,
                darkCoreMaterial);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ArcaneSfxPrefabBuilder] Rebuilt arcane SFX prefabs in " + PrefabFolder);
        }

        private static void BuildPrefab(
            string prefabName,
            ArcaneSfxPreset preset,
            ArcaneSfxPlane plane,
            float radius,
            Material lineMaterial,
            Material softMaterial,
            Material starMaterial,
            Material darkCoreMaterial)
        {
            var root = new GameObject(prefabName);
            try
            {
                var effect = root.AddComponent<ArcaneSfxEffect>();
                effect.Configure(preset, plane, radius, lineMaterial, softMaterial, starMaterial, darkCoreMaterial);
                effect.ClearGeneratedObjects();
                EditorUtility.SetDirty(effect);

                var prefabPath = PrefabFolder + "/" + prefabName + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Material CreateMaterial(string path, Shader shader, Texture texture, Color tint, float alpha)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
            }

            material.SetColor("_TintColor", tint);
            material.SetFloat("_Alpha", alpha);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void WriteTexture(string path, Texture2D texture)
        {
            var fullPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(fullPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        private static void ConfigureTextureImporter(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 256;
            importer.SaveAndReimport();
        }

        private static Texture2D CreateSoftCircleTexture(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "T_SFX_SoftCircle"
            };

            var center = (size - 1) * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = (x - center) / center;
                    var dy = (y - center) / center;
                    var distance = Mathf.Sqrt(dx * dx + dy * dy);
                    var alpha = Mathf.Clamp01(1f - Mathf.SmoothStep(0.12f, 1f, distance));
                    alpha *= alpha;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply(false, false);
            return texture;
        }

        private static Texture2D CreateStarTexture(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "T_SFX_StarSpark"
            };

            var center = (size - 1) * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var px = (x - center) / center;
                    var py = (y - center) / center;
                    var distance = Mathf.Sqrt(px * px + py * py);
                    var vertical = Mathf.Clamp01(1f - Mathf.Abs(px) * 5.5f) * Mathf.Clamp01(1f - Mathf.Abs(py) * 0.9f);
                    var horizontal = Mathf.Clamp01(1f - Mathf.Abs(py) * 5.5f) * Mathf.Clamp01(1f - Mathf.Abs(px) * 0.9f);
                    var diagonalA = Mathf.Clamp01(1f - Mathf.Abs(px - py) * 5.2f) * Mathf.Clamp01(1f - distance * 1.55f);
                    var diagonalB = Mathf.Clamp01(1f - Mathf.Abs(px + py) * 5.2f) * Mathf.Clamp01(1f - distance * 1.55f);
                    var alpha = Mathf.Max(vertical, horizontal, Mathf.Max(diagonalA, diagonalB) * 0.52f);
                    alpha *= Mathf.Clamp01(1f - Mathf.SmoothStep(0.74f, 1f, distance));
                    alpha = Mathf.Pow(Mathf.Clamp01(alpha), 1.35f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply(false, false);
            return texture;
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/0.MainProject/Art", "SFX");
            EnsureFolder("Assets/0.MainProject/Art/SFX", "Arcane");
            EnsureFolder("Assets/Resources", "Prefabs");
            EnsureFolder("Assets/Resources/Prefabs", "VFX");
        }

        private static void EnsureFolder(string parent, string folderName)
        {
            var path = parent + "/" + folderName;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }
    }
}
