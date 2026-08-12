using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.IO;
using System.Text.RegularExpressions;

namespace PWTA
{
    public static class FoliageUtils
    {
        public static string regexPattern = "_x_(\\d+)__y_(\\d+)_";
        public static void CollectMeshesFrom(Transform root, out List<MeshFilter> meshFilters, out Bounds worldBounds)
        {
            meshFilters = new List<MeshFilter>();
            worldBounds = new Bounds();

            MeshFilter[] meshfilters = root.GetComponentsInChildren<MeshFilter>();
            foreach (var meshFilter in meshfilters)//for (int i = 0; i < root.childCount; i++)
            {
                if (null == meshFilter || null == meshFilter.sharedMesh)
                    continue;

                var match = Regex.Match(meshFilter.name, regexPattern);
                if (!match.Success)
                {
                    Debug.LogError("Invalid name: " + meshFilter.name);
                    continue;
                }
                var mesh = meshFilter.sharedMesh;
                if (worldBounds.size == Vector3.zero)
                    worldBounds = mesh.bounds;
                else
                    worldBounds.Encapsulate(mesh.bounds);

                meshFilters.Add(meshFilter);
            }
        }

        public static string GetSceneSubFolderPath()
        {
            var scene = SceneManager.GetActiveScene();
            var sceneName = scene.name;
            var path = Application.dataPath.Replace("Assets", "") + scene.path;
            var absolutePath = Path.GetDirectoryName(path) + "/" + sceneName;
            if (!Directory.Exists(absolutePath))
            {
                Directory.CreateDirectory(absolutePath);
            }
            return absolutePath.Replace('\\', '/');
        }

        public static string GetDefaultPalettePath()
        {
            var defaultDir = GetSceneSubFolderPath();
            return defaultDir + "/FoliagePaletteAsset.asset";
        }

        public static string GetDefaultFoliagePath()
        {
            var defaultDir = GetSceneSubFolderPath();
            return defaultDir + "/FoliageAssets.bytes";
        }

        static float HALF_DEG2RAD = 0.00872664625f; // Mathf.Deg2Rad * 0.5f

        public static Matrix4x4 ToMatrix(Vector3 position, float rotationY, float uniformScale)
        {
            return Matrix4x4.TRS(position, ToYawQuaternion(rotationY), ToUniformVector(uniformScale));
        }

        public static Matrix4x4 GetMatrix(this IFoliageElement compactData)
        {
            return ToMatrix(compactData.Position, compactData.RotationY, compactData.UniformScale);
        }

        public static Matrix4x4 GetMatrix(this IFoliageElement compactData, FoliagePaletteSlotData palette)
        {
            return GetMatrix(compactData, palette.PositionOffset, palette.RotationOffset, palette.ScaleOffset);
        }

        public static Matrix4x4 GetMatrix(this IFoliageElement compactData, Vector3 posOffset, Quaternion rotOffset, Vector3 scaleOffset)
        {
            return ToMatrix(compactData.Position, compactData.RotationY, compactData.UniformScale) * Matrix4x4.TRS(posOffset, rotOffset, scaleOffset);
        }

        public static Quaternion ToYawQuaternion(float yDgrees)
        {
            float halfRad = yDgrees * HALF_DEG2RAD;
            float sin = Mathf.Sin(halfRad);
            float cos = Mathf.Cos(halfRad);
            return new Quaternion(0f, sin, 0f, cos);
        }

        public static Vector3 ToUniformVector(float uniformScale)
        {
            return Vector3.one * uniformScale;
        }

        public static List<Vector2> PoissonDiskSampleCircle(float radius, float minDist, int attempts = 8, List<Vector2> externalPoints = null)
        {
            if (radius < minDist)
            {
                Debug.LogWarning($"PoissonDiskSampleCircle: radius < minDist: {radius} < {minDist}");
                return null;
            }

            if (radius <= 0f || minDist <= 0f)
                return null;

            //CodeTimer.Measure("Poisson Disk Sampling", () => { 
            float cellSize = minDist / Mathf.Sqrt(2);
            int gridSize = Mathf.CeilToInt((radius * 2) / cellSize);
            int[,] grid = new int[gridSize, gridSize];

            List<Vector2> points = new List<Vector2>();
            List<Vector2> active = new List<Vector2>();

            Vector2 firstPoint = Vector2.zero;
            if (externalPoints != null)
                points.AddRange(externalPoints);

            points.Add(firstPoint);
            active.Add(firstPoint);

            Vector2 gridOrigin = -Vector2.one * radius;
            Vector2Int GridCoord(Vector2 p) => new Vector2Int(
                Mathf.FloorToInt((p.x - gridOrigin.x) / cellSize),
                Mathf.FloorToInt((p.y - gridOrigin.y) / cellSize)
            );

            for (int i = 0; i < points.Count; i++)
            {
                var gx = GridCoord(points[i]).x;
                var gy = GridCoord(points[i]).y;
                grid[gx, gy] = i;
            }

            while (active.Count > 0)
            {
                int idx = Random.Range(0, active.Count);
                Vector2 basePoint = active[idx];
                bool found = false;

                for (int i = 0; i < attempts; i++)
                {
                    float angle = Random.value * Mathf.PI * 2f;
                    float dist = Random.Range(minDist, 2 * minDist);
                    Vector2 newPoint = basePoint + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;

                    if (newPoint.sqrMagnitude <= radius * radius)
                    {
                        var coord = GridCoord(newPoint);
                        if (coord.x < 0 || coord.x >= gridSize || coord.y < 0 || coord.y >= gridSize)
                            continue;

                        bool ok = true;
                        for (int x = Mathf.Max(0, coord.x - 2); x <= Mathf.Min(coord.x + 2, gridSize - 1); x++)
                        {
                            for (int y = Mathf.Max(0, coord.y - 2); y <= Mathf.Min(coord.y + 2, gridSize - 1); y++)
                            {
                                Vector2 nearby = points[grid[x, y]];
                                if ((newPoint - nearby).sqrMagnitude < minDist * minDist)
                                {
                                    ok = false;
                                    break;
                                }
                            }
                            if (!ok) break;
                        }

                        if (ok)
                        {
                            grid[coord.x, coord.y] = points.Count;
                            points.Add(newPoint);
                            active.Add(newPoint);
                            found = true;
                            break;
                        }
                    }
                }

                if (!found)
                    active.RemoveAt(idx);
            }
            if (externalPoints != null)
                points.RemoveRange(0, externalPoints.Count);

            return points;
        }

        private static float NEAREST_DIST = 0.5f;
        private static float FARTHEST_DIST = 1f;
        public static float DensityToMinDist(float density)
        {
            return NEAREST_DIST + (FARTHEST_DIST - NEAREST_DIST) * (1f - Mathf.Min(density, 1f));
        }

        public static float ToSampleDistance(float ratio, float foliageRadius)
        {
            return foliageRadius * ratio;
        }

        public static float ToEraseDensity(float ratio)
        {
            return ratio;
        }

        public static List<Vector2> PoissonDiskSampleCircle(float radius, float minDist, int maxAttempts = 8)
        {
            if (radius <= 0f || minDist <= 0f)
                return null;

            //CodeTimer.Measure("Poisson Disk Sampling", () => { 
            float cellSize = minDist / Mathf.Sqrt(2);
            int gridSize = Mathf.CeilToInt((radius * 2) / cellSize);
            int[,] grid = new int[gridSize, gridSize];

            List<Vector2> points = new List<Vector2>();
            List<Vector2> active = new List<Vector2>();

            Vector2 firstPoint = Vector2.zero;

            points.Add(firstPoint);
            active.Add(firstPoint);

            Vector2 gridOrigin = -Vector2.one * radius;
            Vector2Int GridCoord(Vector2 p) => new Vector2Int(
                Mathf.FloorToInt((p.x - gridOrigin.x) / cellSize),
                Mathf.FloorToInt((p.y - gridOrigin.y) / cellSize)
            );

            for (int i = 0; i < points.Count; i++)
            {
                grid[GridCoord(points[i]).x, GridCoord(points[i]).y] = i;
            }

            while (active.Count > 0)
            {
                int idx = Random.Range(0, active.Count);
                Vector2 basePoint = active[idx];
                bool found = false;

                for (int i = 0; i < maxAttempts; i++)
                {
                    float angle = Random.value * Mathf.PI * 2f;
                    float dist = Random.Range(minDist, 2 * minDist);
                    Vector2 newPoint = basePoint + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;

                    if (newPoint.sqrMagnitude <= radius * radius)
                    {
                        var coord = GridCoord(newPoint);
                        if (coord.x < 0 || coord.x >= gridSize || coord.y < 0 || coord.y >= gridSize)
                            continue;

                        bool ok = true;
                        for (int x = Mathf.Max(0, coord.x - 2); x <= Mathf.Min(coord.x + 2, gridSize - 1); x++)
                        {
                            for (int y = Mathf.Max(0, coord.y - 2); y <= Mathf.Min(coord.y + 2, gridSize - 1); y++)
                            {
                                Vector2 nearby = points[grid[x, y]];
                                if ((newPoint - nearby).sqrMagnitude < minDist * minDist)
                                {
                                    ok = false;
                                    break;
                                }
                            }
                            if (!ok) break;
                        }

                        if (ok)
                        {
                            grid[coord.x, coord.y] = points.Count;
                            points.Add(newPoint);
                            active.Add(newPoint);
                            found = true;
                            break;
                        }
                    }
                }

                if (!found)
                    active.RemoveAt(idx);
            }

            return points;
        }

        public static void GetUvMinMax(
                List<MeshFilter> meshFilters,
                out Vector2 uvMin,
                out Vector2 uvMax)
        {
            uvMin = new Vector2(float.MaxValue, float.MaxValue);
            uvMax = new Vector2(float.MinValue, float.MinValue);

            if (meshFilters == null)
                return;

            foreach (var mf in meshFilters)
            {
                if (mf == null) continue;

                var mesh = mf.sharedMesh;
                if (mesh == null) continue;

                var uvs = mesh.uv;
                if (uvs == null || uvs.Length == 0) continue;

                for (int i = 0; i < uvs.Length; i++)
                {
                    Vector2 uv = uvs[i];

                    uvMin.x = Mathf.Min(uvMin.x, uv.x);
                    uvMin.y = Mathf.Min(uvMin.y, uv.y);

                    uvMax.x = Mathf.Max(uvMax.x, uv.x);
                    uvMax.y = Mathf.Max(uvMax.y, uv.y);
                }
            }
        }

        public static Texture2D CropTextureByUV(Texture2D texture, Vector2 uvMin, Vector2 uvMax, int resolutionX, int resolutionY)
        {
            int srcWidth = texture.width;
            int srcHeight = texture.height;

            int xMin = Mathf.Clamp(Mathf.FloorToInt(uvMin.x * srcWidth), 0, srcWidth - 1);
            int yMin = Mathf.Clamp(Mathf.FloorToInt(uvMin.y * srcHeight), 0, srcHeight - 1);
            int xMax = Mathf.Clamp(Mathf.CeilToInt(uvMax.x * srcWidth), 1, srcWidth);
            int yMax = Mathf.Clamp(Mathf.CeilToInt(uvMax.y * srcHeight), 1, srcHeight);

            int cropWidth = xMax - xMin;
            int cropHeight = yMax - yMin;

            var maxMin = Mathf.Max(cropWidth, cropHeight);
            var offsetX = maxMin - cropWidth;
            var offsetY = maxMin - cropHeight;

            cropWidth += offsetX;
            cropHeight += offsetY;
            xMin -= offsetX / 2;
            yMin -= offsetY / 2;

            // 원본 픽셀
            var srcPixels = texture.GetPixels32();
            var resultPixels = new Color32[resolutionX * resolutionY];

            for (int y = 0; y < resolutionY; y++)
            {
                float v = (float)y / (resolutionY - 1);
                int srcY = yMin + Mathf.Clamp(Mathf.RoundToInt(v * (cropHeight - 1)), 0, cropHeight - 1);

                for (int x = 0; x < resolutionX; x++)
                {
                    float u = (float)x / (resolutionX - 1);
                    int srcX = xMin + Mathf.Clamp(Mathf.RoundToInt(u * (cropWidth - 1)), 0, cropWidth - 1);

                    resultPixels[y * resolutionX + x] =
                        srcPixels[srcY * srcWidth + srcX];
                }
            }

            var result = new Texture2D(resolutionX, resolutionY, TextureFormat.RGBA32, false, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 0,
            };

            result.SetPixels32(resultPixels);
            result.Apply(false, false);

            return result;
        }

        public static Texture2D CaptureTextureByOrthoMap(Bounds bounds, int resolutionX, int resolutionY, int cullingMask)
        {
            DrawBounds(bounds, Color.blue, 20f);
            GameObject cameraGO = new GameObject("OrthoCaptureCam");
            Camera camera = cameraGO.AddComponent<Camera>();

            camera.orthographic = true;
            camera.cullingMask = cullingMask;
            camera.transform.position = bounds.center + new Vector3(0, 1000f, 0);
            camera.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);

            float aspect = (float)resolutionX / resolutionY;
            camera.aspect = aspect;
            camera.orthographicSize = Mathf.Max(bounds.size.z / 2f, bounds.size.x / (2f * aspect));
            camera.useOcclusionCulling = false;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 10000f;

            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;

            RenderTexture rt = new RenderTexture(resolutionX, resolutionY, 8);
            camera.targetTexture = rt;

            float prevLodBias = QualitySettings.lodBias;
            QualitySettings.lodBias = 1000f;
            camera.Render();
            QualitySettings.lodBias = prevLodBias;

            Texture2D result = new Texture2D(resolutionX, resolutionY, TextureFormat.Alpha8, false, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 0,
            };

            RenderTexture.active = rt;
            result.ReadPixels(new Rect(0, 0, resolutionX, resolutionY), 0, 0);
            result.Apply(false, false);

            RenderTexture.active = null;
            Object.DestroyImmediate(cameraGO);
            rt.Release();

            return result;
        }

        public static void SaveTexture(Texture2D texture, string path)
        {
            if (!Directory.Exists(Path.GetDirectoryName(path)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
            }

            if (texture.isReadable)
            {
                var bytes = texture.EncodeToPNG();
                File.WriteAllBytes(path, bytes);
            }
            else
            {
                Debug.LogWarning($"Texture is not readable: {path}");
            }
        }

        public static void DrawBounds(Bounds bounds, Color color, float duration = 20f)
        {
            var minx = bounds.min.x;// + bounds.center.x;
            var miny = bounds.min.y;// + bounds.center.y;
            var minz = bounds.min.z;// + bounds.center.z;
            var maxx = bounds.max.x;// + bounds.center.x;
            var maxy = bounds.max.y;// + bounds.center.y;
            var maxz = bounds.max.z;// + bounds.center.z;

            Debug.DrawLine(new Vector3(minx, miny, minz), new Vector3(maxx, miny, minz), color, duration);
            Debug.DrawLine(new Vector3(maxx, miny, minz), new Vector3(maxx, miny, maxz), color, duration);
            Debug.DrawLine(new Vector3(maxx, miny, maxz), new Vector3(minx, miny, maxz), color, duration);
            Debug.DrawLine(new Vector3(minx, miny, maxz), new Vector3(minx, miny, minz), color, duration);

            Debug.DrawLine(new Vector3(minx, maxy, minz), new Vector3(maxx, maxy, minz), color, duration);
            Debug.DrawLine(new Vector3(maxx, maxy, minz), new Vector3(maxx, maxy, maxz), color, duration);
            Debug.DrawLine(new Vector3(maxx, maxy, maxz), new Vector3(minx, maxy, maxz), color, duration);
            Debug.DrawLine(new Vector3(minx, maxy, maxz), new Vector3(minx, maxy, minz), color, duration);

            Debug.DrawLine(new Vector3(minx, miny, minz), new Vector3(minx, maxy, minz), color, duration);
            Debug.DrawLine(new Vector3(maxx, miny, minz), new Vector3(maxx, maxy, minz), color, duration);
            Debug.DrawLine(new Vector3(maxx, miny, maxz), new Vector3(maxx, maxy, maxz), color, duration);
            Debug.DrawLine(new Vector3(minx, miny, maxz), new Vector3(minx, maxy, maxz), color, duration);
        }

        public static bool HashDensityTest(Vector3 position, float worldDensity, int seed = 0)
        {
            int hash = position.GetHashCode();
            uint hashed = (uint)(hash ^ 0x85ebca6b ^ seed);
            float randomValue = hashed % 10000 / 10000f;

            return randomValue < worldDensity;
        }

        public static bool PerlinDensityTest(Vector3 position, float worldDensity, float scale = 0.1f, int seed = 0)
        {
            var randomValue = Mathf.PerlinNoise((position.x * scale) + seed, (position.z * scale) + seed);
            return randomValue < worldDensity;
        }

        const ulong OFFSET = 14695981039346656037UL;
        const ulong PRIME = 1099511628211UL;
        public static ulong ComputeChecksum(Dictionary<int, HashSet<IFoliageElement>> dict)
        {
            if (dict == null || dict.Count == 0)
                return 0;

            ulong hash = OFFSET;

            // Dictionary 키 정렬
            var keys = new List<int>(dict.Keys);
            keys.Sort();

            for (int i = 0; i < keys.Count; i++)
            {
                int key = keys[i];
                Hash(ref hash, (uint)key);

                var set = dict[key];
                if (set == null || set.Count == 0)
                    continue;

                // HashSet 요소 체크썸 수집
                var elementHashes = new List<ulong>(set.Count);
                foreach (var e in set)
                    elementHashes.Add(e.GetChecksum(OFFSET, PRIME));

                elementHashes.Sort();

                for (int j = 0; j < elementHashes.Count; j++)
                    Hash(ref hash, elementHashes[j]);
            }

            return hash;
        }

        static void Hash(ref ulong hash, ulong v)
        {
            unchecked
            {
                hash ^= v;
                hash *= PRIME;
            }
        }

        static void Hash(ref ulong hash, uint v)
        {
            unchecked
            {
                hash ^= v;
                hash *= PRIME;
            }
        }
    }
}