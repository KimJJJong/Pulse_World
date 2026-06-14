using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

internal static class TownForestWaterBuilder
{
    private const string ScenePath = "Assets/0.MainProject/Scenes/Town/Town_Forest.unity";
    private const string RootName = "Town_Forest_Water_Implementation";
    private const string AssetRoot = "Assets/0.MainProject/Art/TownForestWater";
    private const string WaterMaterialPath = "Assets/0.MainProject/Art/TownForestWater/Materials/M_TownForest_River_StylizedWater.mat";
    private const int RiverSampleCount = 40;
    private const float RiverStartX = -7.5f;
    private const float RiverEndX = 60.5f;
    private const float RiverCenterZ = 0f;
    private const float RiverWidth = 6.4f;

    private static string RequestFilePath => Path.Combine(Application.dataPath, "0.MainProject/Editor/TownForestWaterBuilder.run");

    [InitializeOnLoadMethod]
    private static void BuildWhenRequested()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(RequestFilePath))
            {
                return;
            }

            try
            {
                Build();
            }
            finally
            {
                DeleteRequestFile();
            }
        };
    }

    [MenuItem("Tools/Town Forest/Rebuild Reference Water")]
    private static void BuildFromMenu()
    {
        Build();
    }

    private static void Build()
    {
        EnsureFolder("Assets/0.MainProject/Art");
        EnsureFolder(AssetRoot);
        EnsureFolder($"{AssetRoot}/Materials");
        EnsureFolder($"{AssetRoot}/Meshes");

        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        var existing = GameObject.Find(RootName);
        var preservedPose = CapturePose(existing);
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        var root = new GameObject(RootName);
        var townAppearance = GameObject.Find("Town_Appearance");
        if (townAppearance != null)
        {
            root.transform.SetParent(townAppearance.transform, false);
        }
        ApplyPose(root.transform, preservedPose);

        var riverBedY = WorldYToRootLocal(root.transform, 0.04f);
        var riverBankY = WorldYToRootLocal(root.transform, 0.07f);
        var waterBottomY = WorldYToRootLocal(root.transform, 0.045f);
        var waterTopY = WorldYToRootLocal(root.transform, 0.12f);
        var foamY = WorldYToRootLocal(root.transform, 0.155f);
        var flowY = WorldYToRootLocal(root.transform, 0.165f);

        var samples = BuildRiverSamples();
        var waterMaterial = GetWaterMaterial();
        var bedMaterial = GetLitMaterial($"{AssetRoot}/Materials/M_TownForest_RiverBed_Dark.mat", Hex("071923"), 1f, 0.9f);
        var wetBankMaterial = GetLitMaterial($"{AssetRoot}/Materials/M_TownForest_WetStoneBank.mat", Hex("13231f"), 1f, 0.78f);
        var rockMaterial = GetLitMaterial($"{AssetRoot}/Materials/M_TownForest_Fallback_Rock.mat", Hex("343b35"), 1f, 0.82f);
        var woodMaterial = GetLitMaterial($"{AssetRoot}/Materials/M_TownForest_WetBridgeWood.mat", Hex("3a2418"), 1f, 0.72f);
        var foamMaterial = GetTransparentMaterial($"{AssetRoot}/Materials/M_TownForest_RiverFoam.mat", HexAlpha("c9faff", 0.48f), false);
        var flowMaterial = GetTransparentMaterial($"{AssetRoot}/Materials/M_TownForest_FlowHighlights.mat", HexAlpha("6c9faf", 0.22f), false);
        var waterfallMaterial = GetTransparentMaterial($"{AssetRoot}/Materials/M_TownForest_WaterfallSheet.mat", HexAlpha("6c9faf", 0.66f), false);
        var mistMaterial = GetParticleMaterial($"{AssetRoot}/Materials/M_TownForest_MistParticle.mat", HexAlpha("d7ffff", 0.55f));

        var riverBed = CreateMeshObject(root.transform, "RiverBed_DarkCut", CreateRibbonMesh("M_RiverBed_DarkCut", samples, 0.42f, riverBedY), bedMaterial);
        riverBed.transform.localPosition = Vector3.zero;

        CreateMeshObject(root.transform, "RiverBank_WetShadow_North", CreateSideStripMesh("M_RiverBank_WetShadow_North", samples, 1, 0.04f, 0.62f, riverBankY), wetBankMaterial);
        CreateMeshObject(root.transform, "RiverBank_WetShadow_South", CreateSideStripMesh("M_RiverBank_WetShadow_South", samples, -1, 0.04f, 0.62f, riverBankY), wetBankMaterial);

        var water = CreateMeshObject(root.transform, "River_Water_Surface", CreateRibbonMesh("M_River_Water_Surface", samples, -0.04f, waterTopY), waterMaterial);
        water.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        water.GetComponent<MeshRenderer>().receiveShadows = false;
        ConfigureWaterPhysics(water, samples);

        CreateMeshObject(root.transform, "River_EdgeFoam_North", CreateSideStripMesh("M_River_EdgeFoam_North", samples, 1, -0.08f, 0.02f, foamY), foamMaterial);
        CreateMeshObject(root.transform, "River_EdgeFoam_South", CreateSideStripMesh("M_River_EdgeFoam_South", samples, -1, -0.08f, 0.02f, foamY), foamMaterial);

        CreateFlowLine(root.transform, samples, "River_FlowLine_A", -1.2f, 0.07f, flowY, flowMaterial);
        CreateFlowLine(root.transform, samples, "River_FlowLine_B", 0.0f, 0.06f, flowY + 0.01f, flowMaterial);
        CreateFlowLine(root.transform, samples, "River_FlowLine_C", 1.2f, 0.07f, flowY, flowMaterial);

        BuildWaterfall(root.transform, samples, waterfallMaterial, foamMaterial, mistMaterial, waterTopY);
        BuildBridge(root.transform, samples, woodMaterial, waterTopY);
        BuildRiverRocks(root.transform, samples, rockMaterial, riverBankY);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[TownForestWaterBuilder] Built reference-style river water in Town_Forest.");
    }

    private static void ConfigureWaterPhysics(GameObject water, List<RiverSample> samples)
    {
        var collider = water.AddComponent<BoxCollider>();
        var bounds = water.GetComponent<MeshFilter>().sharedMesh.bounds;
        collider.isTrigger = true;
        collider.center = bounds.center + new Vector3(0f, -0.22f, 0f);
        collider.size = new Vector3(bounds.size.x, 0.55f, bounds.size.z);
    }

    private static RootPose CapturePose(GameObject existing)
    {
        if (existing == null)
        {
            return RootPose.Default;
        }

        var transform = existing.transform;
        return new RootPose
        {
            LocalPosition = transform.localPosition,
            LocalRotation = transform.localRotation,
            LocalScale = transform.localScale
        };
    }

    private static void ApplyPose(Transform transform, RootPose pose)
    {
        transform.localPosition = pose.LocalPosition;
        transform.localRotation = pose.LocalRotation;
        transform.localScale = pose.LocalScale;
    }

    private static float WorldYToRootLocal(Transform root, float worldY)
    {
        var scaleY = Mathf.Abs(root.lossyScale.y);
        if (scaleY < 0.0001f)
        {
            scaleY = 1f;
        }

        return (worldY - root.position.y) / scaleY;
    }

    private static List<RiverSample> BuildRiverSamples()
    {
        var controlPoints = new List<Vector3>
        {
            new Vector3(-7.5f, 0f, 3.0f),
            new Vector3(15.0f, 0f, 3.0f),
            new Vector3(33.23f, 0f, 0.0f),
            new Vector3(42.0f, 0f, -3.0f),
            new Vector3(50.0f, 0f, 0.0f),
            new Vector3(60.5f, 0f, 0.0f)
        };

        // Calculate cumulative lengths of control points
        var lengths = new float[controlPoints.Count];
        lengths[0] = 0f;
        var totalLength = 0f;
        for (int i = 1; i < controlPoints.Count; i++)
        {
            totalLength += Vector3.Distance(controlPoints[i - 1], controlPoints[i]);
            lengths[i] = totalLength;
        }

        var centers = new List<Vector3>(RiverSampleCount);
        for (int i = 0; i < RiverSampleCount; i++)
        {
            float targetDist = (i / (float)(RiverSampleCount - 1)) * totalLength;
            int seg = 0;
            while (seg < controlPoints.Count - 2 && lengths[seg + 1] < targetDist)
            {
                seg++;
            }
            float segStartDist = lengths[seg];
            float segEndDist = lengths[seg + 1];
            float segLength = segEndDist - segStartDist;
            float t = (segLength > 0.0001f) ? (targetDist - segStartDist) / segLength : 0f;
            centers.Add(Vector3.Lerp(controlPoints[seg], controlPoints[seg + 1], t));
        }

        var samples = new List<RiverSample>(centers.Count);
        var distance = 0f;
        for (var i = 0; i < centers.Count; i++)
        {
            if (i > 0)
            {
                distance += Vector3.Distance(centers[i - 1], centers[i]);
            }

            Vector3 tangent;
            if (i < centers.Count - 1 && i > 0)
            {
                var dirForward = (centers[i + 1] - centers[i]).normalized;
                var dirBackward = (centers[i] - centers[i - 1]).normalized;
                tangent = (dirForward + dirBackward).normalized;
            }
            else if (i == 0)
            {
                tangent = (centers[1] - centers[0]).normalized;
            }
            else
            {
                tangent = (centers[i] - centers[i - 1]).normalized;
            }

            Vector3 normal = new Vector3(-tangent.z, 0f, tangent.x).normalized;

            samples.Add(new RiverSample
            {
                Center = centers[i],
                Tangent = tangent,
                Normal = normal,
                HalfWidth = RiverWidth * 0.5f,
                Distance = distance
            });
        }

        return samples;
    }

    private static Mesh CreateRibbonMesh(string assetName, List<RiverSample> samples, float widthOffset, float y)
    {
        var vertices = new Vector3[samples.Count * 2];
        var uvs = new Vector2[vertices.Length];
        var triangles = new int[(samples.Count - 1) * 6];

        for (var i = 0; i < samples.Count; i++)
        {
            var sample = samples[i];
            var left = sample.Center + sample.Normal * (sample.HalfWidth + widthOffset);
            var right = sample.Center - sample.Normal * (sample.HalfWidth + widthOffset);
            left.y = y;
            right.y = y;

            var vertexIndex = i * 2;
            vertices[vertexIndex] = left;
            vertices[vertexIndex + 1] = right;
            uvs[vertexIndex] = new Vector2(sample.Distance * 0.08f, 0f);
            uvs[vertexIndex + 1] = new Vector2(sample.Distance * 0.08f, 1f);
        }

        var triangleIndex = 0;
        for (var i = 0; i < samples.Count - 1; i++)
        {
            var current = i * 2;
            var next = current + 2;
            triangles[triangleIndex++] = current;
            triangles[triangleIndex++] = next;
            triangles[triangleIndex++] = next + 1;
            triangles[triangleIndex++] = current;
            triangles[triangleIndex++] = next + 1;
            triangles[triangleIndex++] = current + 1;
        }

        return SaveMesh(assetName, vertices, uvs, triangles);
    }

    private static Mesh CreateWaterBodyMesh(string assetName, List<RiverSample> samples, float widthOffset, float topY, float bottomY)
    {
        var topVertices = new Vector3[samples.Count * 2];
        var vertices = new Vector3[samples.Count * 4];
        var uvs = new Vector2[vertices.Length];

        for (var i = 0; i < samples.Count; i++)
        {
            var sample = samples[i];
            var left = sample.Center + sample.Normal * (sample.HalfWidth + widthOffset);
            var right = sample.Center - sample.Normal * (sample.HalfWidth + widthOffset);
            left.y = topY;
            right.y = topY;

            var topIndex = i * 2;
            topVertices[topIndex] = left;
            topVertices[topIndex + 1] = right;

            var vertexIndex = i * 4;
            vertices[vertexIndex] = left;
            vertices[vertexIndex + 1] = right;
            vertices[vertexIndex + 2] = new Vector3(left.x, bottomY, left.z);
            vertices[vertexIndex + 3] = new Vector3(right.x, bottomY, right.z);

            var u = sample.Distance * 0.08f;
            uvs[vertexIndex] = new Vector2(u, 0f);
            uvs[vertexIndex + 1] = new Vector2(u, 1f);
            uvs[vertexIndex + 2] = new Vector2(u, 0f);
            uvs[vertexIndex + 3] = new Vector2(u, 1f);
        }

        var triangles = new List<int>((samples.Count - 1) * 24 + 12);
        for (var i = 0; i < samples.Count - 1; i++)
        {
            var current = i * 4;
            var next = current + 4;

            AddQuad(triangles, current, next, next + 1, current + 1); // water top
            AddQuad(triangles, current + 2, current, next, next + 2); // left side
            AddQuad(triangles, current + 1, current + 3, next + 3, next + 1); // right side
        }

        AddQuad(triangles, 0, 1, 3, 2);
        var last = (samples.Count - 1) * 4;
        AddQuad(triangles, last, last + 2, last + 3, last + 1);

        return SaveMesh(assetName, vertices, uvs, triangles.ToArray());
    }

    private static Mesh CreateSideStripMesh(string assetName, List<RiverSample> samples, int side, float innerOffset, float outerOffset, float y)
    {
        var vertices = new Vector3[samples.Count * 2];
        var uvs = new Vector2[vertices.Length];
        var triangles = new int[(samples.Count - 1) * 6];

        for (var i = 0; i < samples.Count; i++)
        {
            var sample = samples[i];
            var inner = sample.Center + sample.Normal * side * (sample.HalfWidth + innerOffset);
            var outer = sample.Center + sample.Normal * side * (sample.HalfWidth + outerOffset);
            inner.y = y;
            outer.y = y;

            var vertexIndex = i * 2;
            if (side > 0)
            {
                vertices[vertexIndex] = outer;
                vertices[vertexIndex + 1] = inner;
            }
            else
            {
                vertices[vertexIndex] = inner;
                vertices[vertexIndex + 1] = outer;
            }

            uvs[vertexIndex] = new Vector2(sample.Distance * 0.12f, 0f);
            uvs[vertexIndex + 1] = new Vector2(sample.Distance * 0.12f, 1f);
        }

        var triangleIndex = 0;
        for (var i = 0; i < samples.Count - 1; i++)
        {
            var current = i * 2;
            var next = current + 2;
            triangles[triangleIndex++] = current;
            triangles[triangleIndex++] = next;
            triangles[triangleIndex++] = next + 1;
            triangles[triangleIndex++] = current;
            triangles[triangleIndex++] = next + 1;
            triangles[triangleIndex++] = current + 1;
        }

        return SaveMesh(assetName, vertices, uvs, triangles);
    }

    private static void AddQuad(List<int> triangles, int a, int b, int c, int d)
    {
        triangles.Add(a);
        triangles.Add(b);
        triangles.Add(c);
        triangles.Add(a);
        triangles.Add(c);
        triangles.Add(d);
    }

    private static void CreateFlowLine(Transform parent, List<RiverSample> samples, string name, float offset, float width, float y, Material material)
    {
        var vertices = new Vector3[samples.Count * 2];
        var uvs = new Vector2[vertices.Length];
        var triangles = new int[(samples.Count - 1) * 6];

        for (var i = 0; i < samples.Count; i++)
        {
            var sample = samples[i];
            var center = sample.Center + sample.Normal * offset;
            var left = center + sample.Normal * width;
            var right = center - sample.Normal * width;
            left.y = y;
            right.y = y;
            var vertexIndex = i * 2;
            vertices[vertexIndex] = left;
            vertices[vertexIndex + 1] = right;
            uvs[vertexIndex] = new Vector2(sample.Distance * 0.2f, 0f);
            uvs[vertexIndex + 1] = new Vector2(sample.Distance * 0.2f, 1f);
        }

        var triangleIndex = 0;
        for (var i = 0; i < samples.Count - 1; i++)
        {
            var current = i * 2;
            var next = current + 2;
            triangles[triangleIndex++] = current;
            triangles[triangleIndex++] = next;
            triangles[triangleIndex++] = next + 1;
            triangles[triangleIndex++] = current;
            triangles[triangleIndex++] = next + 1;
            triangles[triangleIndex++] = current + 1;
        }

        CreateMeshObject(parent, name, SaveMesh($"M_{name}", vertices, uvs, triangles), material);
    }

    private static void BuildWaterfall(Transform parent, List<RiverSample> samples, Material waterfallMaterial, Material foamMaterial, Material mistMaterial, float waterTopY)
    {
        var end = samples[samples.Count - 1];
        var widthDirection = end.Normal;
        var center = end.Center + end.Tangent * 0.25f;
        var topCenter = center + new Vector3(0f, waterTopY + 0.5f, 0f);
        var bottomCenter = center + end.Tangent * 0.92f + end.Normal * (-0.05f) + new Vector3(0f, waterTopY - 0.18f, 0f);
        var halfWidth = 3.05f;

        var topLeft = topCenter + widthDirection * halfWidth;
        var topRight = topCenter - widthDirection * halfWidth;
        var bottomLeft = bottomCenter + widthDirection * (halfWidth * 0.92f);
        var bottomRight = bottomCenter - widthDirection * (halfWidth * 0.92f);
        var vertices = new[] { topLeft, topRight, bottomLeft, bottomRight };
        var uvs = new[] { new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(1f, 0f) };
        var triangles = new[] { 0, 2, 1, 1, 2, 3, 0, 1, 2, 1, 3, 2 };
        CreateMeshObject(parent, "Waterfall_Sheet", SaveMesh("M_Waterfall_Sheet", vertices, uvs, triangles), waterfallMaterial);

        for (var i = 0; i < 5; i++)
        {
            var t = (i - 2) * 0.42f;
            var streakCenter = center + widthDirection * t + new Vector3(0.02f * i, 0f, 0.01f * i);
            var streakHalf = 0.07f + 0.02f * (i % 2);
            var v = new[]
            {
                streakCenter + widthDirection * streakHalf + Vector3.up * (waterTopY + 0.46f),
                streakCenter - widthDirection * streakHalf + Vector3.up * (waterTopY + 0.46f),
                streakCenter + widthDirection * (streakHalf * 1.8f) + Vector3.up * (waterTopY - 0.12f) + end.Tangent * 0.62f,
                streakCenter - widthDirection * (streakHalf * 1.8f) + Vector3.up * (waterTopY - 0.12f) + end.Tangent * 0.62f
            };
            CreateMeshObject(parent, $"Waterfall_WhiteStreak_{i + 1:00}", SaveMesh($"M_Waterfall_WhiteStreak_{i + 1:00}", v, uvs, triangles), foamMaterial);
        }

        var basinCenter = center + end.Tangent * 1.55f;
        basinCenter.y = waterTopY + 0.03f;
        var basinMesh = CreateRectMesh("M_Waterfall_FoamBasin", basinCenter, end.Tangent, widthDirection, 2.65f, halfWidth * 1.84f);
        CreateMeshObject(parent, "Waterfall_FoamBasin", basinMesh, foamMaterial);
        CreateMist(parent, mistMaterial, basinCenter + Vector3.up * 0.1f);
    }

    private static void BuildBridge(Transform parent, List<RiverSample> samples, Material woodMaterial, float waterTopY)
    {
        var targetBridgeX = 33.23f;
        var bridgeSample = samples[0];
        var minDiff = float.MaxValue;
        foreach (var sample in samples)
        {
            var diff = Mathf.Abs(sample.Center.x - targetBridgeX);
            if (diff < minDiff)
            {
                minDiff = diff;
                bridgeSample = sample;
            }
        }
        var bridgeCenter = bridgeSample.Center;
        bridgeCenter.y = waterTopY + 0.24f;

        var bridgeRoot = new GameObject("River_Crossing_WetWoodBridge");
        bridgeRoot.transform.SetParent(parent, false);
        bridgeRoot.transform.position = Vector3.zero;

        var bridgeRotation = Quaternion.LookRotation(bridgeSample.Normal, Vector3.up);

        for (var i = -1; i <= 1; i++)
        {
            CreateCube(
                bridgeRoot.transform,
                $"Bridge_LongPlank_{i + 2:00}",
                bridgeCenter + bridgeSample.Tangent * (i * 0.58f),
                new Vector3(0.46f, 0.18f, 8.15f),
                bridgeRotation,
                woodMaterial);
        }

        CreateCube(bridgeRoot.transform, "Bridge_NorthRail", bridgeCenter + bridgeSample.Tangent * 1.3f + Vector3.up * 0.53f, new Vector3(0.18f, 0.18f, 8.45f), bridgeRotation, woodMaterial);
        CreateCube(bridgeRoot.transform, "Bridge_SouthRail", bridgeCenter - bridgeSample.Tangent * 1.3f + Vector3.up * 0.53f, new Vector3(0.18f, 0.18f, 8.45f), bridgeRotation, woodMaterial);

        var postPositions = new[]
        {
            bridgeCenter + bridgeSample.Tangent * 1.3f + bridgeSample.Normal * 4.1f + Vector3.up * 0.3f,
            bridgeCenter + bridgeSample.Tangent * 1.3f - bridgeSample.Normal * 4.1f + Vector3.up * 0.3f,
            bridgeCenter - bridgeSample.Tangent * 1.3f + bridgeSample.Normal * 4.1f + Vector3.up * 0.3f,
            bridgeCenter - bridgeSample.Tangent * 1.3f - bridgeSample.Normal * 4.1f + Vector3.up * 0.3f
        };

        for (var i = 0; i < postPositions.Length; i++)
        {
            CreateCube(bridgeRoot.transform, $"Bridge_Post_{i + 1:00}", postPositions[i], new Vector3(0.28f, 0.78f, 0.28f), bridgeRotation, woodMaterial);
        }
    }

    private static void BuildRiverRocks(Transform parent, List<RiverSample> samples, Material fallbackMaterial, float riverBankY)
    {
        var rocksRoot = new GameObject("RiverBank_Rocks");
        rocksRoot.transform.SetParent(parent, false);

        var source = GameObject.Find("Meshy_AI_Geometric_Rock_Pile_0529051806_texture (1)") ??
                     GameObject.Find("Meshy_AI_Geometric_Rock_Pile_0529051806_texture");
        var sourceMesh = source != null ? source.GetComponent<MeshFilter>()?.sharedMesh : null;
        var sourceMaterials = source != null ? source.GetComponent<MeshRenderer>()?.sharedMaterials : null;

        var placements = new[]
        {
            new RockPlacement(0, 1, 1.05f, 240f, 18f),
            new RockPlacement(0, -1, 0.95f, 185f, 74f),
            new RockPlacement(0, 1, 1.28f, 220f, 138f),
            new RockPlacement(0, -1, 1.18f, 285f, 211f),
            new RockPlacement(0, 1, 0.9f, 180f, 292f),
            new RockPlacement(0, -1, 1.36f, 250f, 327f),
            new RockPlacement(0, 1, 1.05f, 310f, 41f),
            new RockPlacement(0, -1, 1.1f, 210f, 115f)
        };

        for (var i = 0; i < placements.Length; i++)
        {
            var placement = placements[i];
            var t = (i / 2 + 1) / 5f;
            var sample = samples[Mathf.Clamp(Mathf.RoundToInt(t * (samples.Count - 1)), 0, samples.Count - 1)];
            var position = sample.Center + sample.Normal * placement.Side * (sample.HalfWidth + placement.BankOffset);
            position.y = riverBankY + 0.04f;

            GameObject rock;
            if (sourceMesh != null)
            {
                rock = new GameObject($"RiverBank_RockCluster_{i + 1:00}");
                rock.transform.SetParent(rocksRoot.transform, false);
                var filter = rock.AddComponent<MeshFilter>();
                filter.sharedMesh = sourceMesh;
                var renderer = rock.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = sourceMaterials != null && sourceMaterials.Length > 0 ? sourceMaterials : new[] { fallbackMaterial };
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                rock.transform.position = position;
                rock.transform.rotation = Quaternion.Euler(270f, placement.Yaw, 0f);
                rock.transform.localScale = Vector3.one * placement.Scale;
            }
            else
            {
                rock = CreateFallbackRock(rocksRoot.transform, $"RiverBank_RockCluster_{i + 1:00}", position, placement.Scale * 0.0065f, placement.Yaw, fallbackMaterial);
            }
        }

        CreateFallbackRock(rocksRoot.transform, "Waterfall_Ledge_Rock_A", new Vector3(51.1f, riverBankY + 0.02f, 1.9f), 1.55f, 13f, fallbackMaterial);
        CreateFallbackRock(rocksRoot.transform, "Waterfall_Ledge_Rock_B", new Vector3(52.9f, riverBankY + 0.02f, -1.9f), 1.35f, 71f, fallbackMaterial);
        CreateFallbackRock(rocksRoot.transform, "Waterfall_Ledge_Rock_C", new Vector3(55.25f, riverBankY + 0.02f, 0.95f), 1.1f, 133f, fallbackMaterial);
    }

    private static RiverSample LerpRiverSample(RiverSample start, RiverSample end, float t)
    {
        return new RiverSample
        {
            Center = Vector3.Lerp(start.Center, end.Center, t),
            Tangent = Vector3.Normalize(Vector3.Lerp(start.Tangent, end.Tangent, t)),
            Normal = Vector3.Normalize(Vector3.Lerp(start.Normal, end.Normal, t)),
            HalfWidth = Mathf.Lerp(start.HalfWidth, end.HalfWidth, t),
            Distance = Mathf.Lerp(start.Distance, end.Distance, t)
        };
    }

    private static GameObject CreateFallbackRock(Transform parent, string name, Vector3 position, float scale, float yaw, Material material)
    {
        var mesh = CreateLowPolyRockMesh($"M_{name}", scale);
        var rock = CreateMeshObject(parent, name, mesh, material);
        rock.transform.position = position;
        rock.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        return rock;
    }

    private static Mesh CreateLowPolyRockMesh(string assetName, float scale)
    {
        var vertices = new[]
        {
            new Vector3(-0.65f, 0f, -0.45f),
            new Vector3(0.55f, 0f, -0.55f),
            new Vector3(0.75f, 0f, 0.35f),
            new Vector3(-0.45f, 0f, 0.6f),
            new Vector3(-0.22f, 0.55f, -0.18f),
            new Vector3(0.32f, 0.48f, 0.08f)
        };

        for (var i = 0; i < vertices.Length; i++)
        {
            vertices[i] *= scale;
        }

        var uvs = new[]
        {
            Vector2.zero, Vector2.right, Vector2.one, Vector2.up, new Vector2(0.4f, 0.6f), new Vector2(0.65f, 0.45f)
        };
        var triangles = new[]
        {
            0, 4, 1, 1, 4, 5, 1, 5, 2, 2, 5, 3, 3, 5, 4, 3, 4, 0,
            0, 1, 2, 0, 2, 3
        };
        return SaveMesh(assetName, vertices, uvs, triangles);
    }

    private static void CreateMist(Transform parent, Material mistMaterial, Vector3 position)
    {
        var go = new GameObject("Waterfall_Mist_Continuous");
        go.transform.SetParent(parent, false);
        go.transform.position = position;

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.duration = 4f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 1.15f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.55f);
        main.startColor = new ParticleSystem.MinMaxGradient(HexAlpha("d7ffff", 0.12f), HexAlpha("ffffff", 0.38f));
        main.gravityModifier = -0.08f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 32f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(3.6f, 0.2f, 1.8f);

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.16f, 0.22f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.22f, 0.58f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);

        var color = ps.colorOverLifetime;
        color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Hex("c9faff"), 0.45f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.34f, 0.2f),
                new GradientAlphaKey(0f, 1f)
            });
        color.color = gradient;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = mistMaterial;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingFudge = 2f;
    }

    private static GameObject CreateMeshObject(Transform parent, string name, Mesh mesh, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = true;
        return go;
    }

    private static GameObject CreateCube(Transform parent, string name, Vector3 position, Vector3 scale, Quaternion rotation, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.transform.rotation = rotation;
        go.transform.localScale = scale;
        var collider = go.GetComponent<Collider>();
        if (collider != null)
        {
            Object.DestroyImmediate(collider);
        }

        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.On;
        renderer.receiveShadows = true;
        return go;
    }

    private static Mesh CreateEllipseMesh(string assetName, Vector3 center, float radiusX, float radiusZ, int segments, float noise)
    {
        var vertices = new Vector3[segments + 1];
        var uvs = new Vector2[vertices.Length];
        var triangles = new int[segments * 3];
        vertices[0] = center;
        uvs[0] = new Vector2(0.5f, 0.5f);

        for (var i = 0; i < segments; i++)
        {
            var angle = i / (float)segments * Mathf.PI * 2f;
            var wobble = 1f + noise * 0.18f * Mathf.Sin(i * 1.73f) + noise * 0.12f * Mathf.Sin(i * 0.67f + 1.4f);
            vertices[i + 1] = center + new Vector3(Mathf.Cos(angle) * radiusX * wobble, 0f, Mathf.Sin(angle) * radiusZ * wobble);
            uvs[i + 1] = new Vector2(0.5f + Mathf.Cos(angle) * 0.5f, 0.5f + Mathf.Sin(angle) * 0.5f);
        }

        var triangleIndex = 0;
        for (var i = 0; i < segments; i++)
        {
            triangles[triangleIndex++] = 0;
            triangles[triangleIndex++] = i + 1;
            triangles[triangleIndex++] = i == segments - 1 ? 1 : i + 2;
        }

        return SaveMesh(assetName, vertices, uvs, triangles);
    }

    private static Mesh CreateRectMesh(string assetName, Vector3 center, Vector3 lengthDirection, Vector3 widthDirection, float length, float width)
    {
        var lengthAxis = lengthDirection.normalized;
        var widthAxis = widthDirection.normalized;
        var halfLength = length * 0.5f;
        var halfWidth = width * 0.5f;

        var vertices = new[]
        {
            center - lengthAxis * halfLength + widthAxis * halfWidth,
            center - lengthAxis * halfLength - widthAxis * halfWidth,
            center + lengthAxis * halfLength + widthAxis * halfWidth,
            center + lengthAxis * halfLength - widthAxis * halfWidth
        };
        var uvs = new[] { new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
        var triangles = new[] { 0, 2, 1, 1, 2, 3, 0, 1, 2, 1, 3, 2 };
        return SaveMesh(assetName, vertices, uvs, triangles);
    }

    private static Mesh SaveMesh(string assetName, Vector3[] vertices, Vector2[] uvs, int[] triangles)
    {
        var mesh = new Mesh { name = assetName };
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        return SaveMeshAsset(assetName, mesh);
    }

    private static Mesh SaveMeshAsset(string assetName, Mesh mesh)
    {
        var path = $"{AssetRoot}/Meshes/{assetName}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
        {
            EditorUtility.CopySerialized(mesh, existing);
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(mesh);
            return existing;
        }

        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static Material GetWaterMaterial()
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                     Shader.Find("Unlit/Color") ??
                     Shader.Find("Standard");
        var material = AssetDatabase.LoadAssetAtPath<Material>(WaterMaterialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "M_TownForest_River_StylizedWater" };
            AssetDatabase.CreateAsset(material, WaterMaterialPath);
        }
        else if (shader != null && material.shader != shader)
        {
            material.shader = shader;
        }

        SetColor(material, "_BaseColor", HexAlpha("187b9d", 1f));
        SetColor(material, "_Color", HexAlpha("187b9d", 1f));
        SetColor(material, "_ShallowColor", HexAlpha("187b9d", 0.9f));
        SetColor(material, "_DeepColor", HexAlpha("0b4a6a", 0.96f));
        SetColor(material, "_HorizonColor", HexAlpha("36b7d4", 0.9f));
        SetColor(material, "_AbsorptionColor", Hex("187b9d"));
        SetColor(material, "_FlowColor", HexAlpha("bff9ff", 0.42f));
        SetColor(material, "_FoamColor", HexAlpha("e8ffff", 0.72f));
        SetFloat(material, "_Alpha", 0.94f);
        SetFloat(material, "_FlowSpeed", 0.9f);
        SetFloat(material, "_RippleScale", 18f);
        SetFloat(material, "_FoamWidth", 0.1f);
        SetFloat(material, "_FoamStrength", 0.38f);
        SetFloat(material, "_FlowStrength", 0.11f);
        SetFloat(material, "_DepthMaxDistance", 3.6f);
        SetFloat(material, "_DepthStrength", 0.82f);
        SetFloat(material, "_WaveSpeed", 1.35f);
        SetFloat(material, "_WaveHeight", 0.045f);
        SetFloat(material, "_Choppiness", 0.28f);
        SetFloat(material, "_NormalStrength", 0.85f);
        SetFloat(material, "_FoamDistance", 0.65f);
        SetFloat(material, "_FoamNoiseScale", 18f);
        SetFloat(material, "_FoamSpeed", 0.9f);
        SetFloat(material, "_FoamSharpness", 0.62f);
        SetFloat(material, "_WaveFoamEnabled", 1f);
        SetFloat(material, "_CausticsEnabled", 1f);
        SetFloat(material, "_CausticsStrength", 0.35f);
        SetFloat(material, "_Smoothness", 0.9f);
        SetFloat(material, "_ReflectionStrength", 0.32f);
        SetFloat(material, "_Surface", 0f);
        SetFloat(material, "_ZWrite", 1f);
        SetFloat(material, "_Cull", 0f);
        material.SetOverrideTag("RenderType", "Opaque");
        material.SetOverrideTag("Queue", "Geometry");
        material.renderQueue = 2450;
        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material GetLitMaterial(string path, Color color, float alpha, float smoothness)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var material = GetOrCreateMaterial(path, shader);
        color.a = alpha;
        SetColor(material, "_BaseColor", color);
        SetColor(material, "_Color", color);
        SetFloat(material, "_Smoothness", smoothness);
        SetFloat(material, "_Metallic", 0f);
        material.renderQueue = -1;
        material.SetOverrideTag("RenderType", "Opaque");
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material GetTransparentMaterial(string path, Color color, bool additive)
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Transparent") ?? Shader.Find("Sprites/Default");
        var material = GetOrCreateMaterial(path, shader);
        SetColor(material, "_BaseColor", color);
        SetColor(material, "_Color", color);
        SetFloat(material, "_Surface", 1f);
        SetFloat(material, "_Blend", additive ? 1f : 0f);
        SetFloat(material, "_ZWrite", 0f);
        SetFloat(material, "_Cull", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.SetOverrideTag("Queue", "Transparent");
        material.renderQueue = additive ? 3100 : 3000;
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        if (additive)
        {
            material.EnableKeyword("_BLENDMODE_ADD");
        }
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material GetParticleMaterial(string path, Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default");
        var material = GetOrCreateMaterial(path, shader);
        SetColor(material, "_BaseColor", color);
        SetColor(material, "_Color", color);
        SetFloat(material, "_Surface", 1f);
        SetFloat(material, "_Blend", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = 3100;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material GetOrCreateMaterial(string path, Shader shader)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
        {
            if (shader != null && material.shader != shader)
            {
                material.shader = shader;
            }
            return material;
        }

        material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static void SetFloat(Material material, string property, float value)
    {
        if (material.HasProperty(property))
        {
            material.SetFloat(property, value);
        }
    }

    private static void SetColor(Material material, string property, Color value)
    {
        if (material.HasProperty(property))
        {
            material.SetColor(property, value);
        }
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        var parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
        var folder = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent))
        {
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder);
        }
    }

    private static void DeleteRequestFile()
    {
        if (File.Exists(RequestFilePath))
        {
            File.Delete(RequestFilePath);
        }

        var metaPath = $"{RequestFilePath}.meta";
        if (File.Exists(metaPath))
        {
            File.Delete(metaPath);
        }
    }

    private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        var t2 = t * t;
        var t3 = t2 * t;
        return 0.5f * ((2f * p1) +
                       (-p0 + p2) * t +
                       (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                       (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    private static float Smooth(float t)
    {
        return t * t * (3f - 2f * t);
    }

    private static Color Hex(string value)
    {
        ColorUtility.TryParseHtmlString($"#{value}", out var color);
        return color;
    }

    private static Color HexAlpha(string value, float alpha)
    {
        var color = Hex(value);
        color.a = alpha;
        return color;
    }

    private struct RiverSample
    {
        public Vector3 Center;
        public Vector3 Tangent;
        public Vector3 Normal;
        public float HalfWidth;
        public float Distance;
    }

    private struct RootPose
    {
        public static RootPose Default => new RootPose
        {
            LocalPosition = Vector3.zero,
            LocalRotation = Quaternion.identity,
            LocalScale = Vector3.one
        };

        public Vector3 LocalPosition;
        public Quaternion LocalRotation;
        public Vector3 LocalScale;
    }

    private readonly struct RockPlacement
    {
        public readonly int SampleIndex;
        public readonly int Side;
        public readonly float BankOffset;
        public readonly float Scale;
        public readonly float Yaw;

        public RockPlacement(int sampleIndex, int side, float bankOffset, float scale, float yaw)
        {
            SampleIndex = sampleIndex;
            Side = side;
            BankOffset = bankOffset;
            Scale = scale;
            Yaw = yaw;
        }
    }
}
