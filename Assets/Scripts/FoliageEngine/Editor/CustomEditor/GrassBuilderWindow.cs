using System.Collections.Generic;
using System.IO;
using UnityEditor.SceneManagement;
using UnityEditor;
using UnityEngine;
using UnityEditorInternal;

namespace PWTA
{
    public class GrassBuilderWindow : EditorWindow
    {
        public static readonly int SamplingResX = 512;
        public static readonly int SamplingResY = 512;
        public static readonly int DefaultPaletteID = 0;
        public static readonly int DefaultSDFRadius = 10;
        public static readonly float DefaultSDFThreshold = 0.3f;

        private Transform _terrainRoot;
        private FoliageManager _foliageManager;
        private BVHGeometryNode _gemetryNode;
        private GameObject _overlayMeshObject;
        private Material _overlayMaterial;
        public Texture2D splatTexture;
        public Texture2D cropedTexture;
        public Texture2D cropedSdfTexture;
        public Texture2D maskTexture1;
        public Texture2D maskTextureSDF;
        public Texture2D maskTexture2;
        public Texture2D noiseTexture;
        public Texture2D densityTexture;
        public LayerMask maskLayer1;
        public LayerMask maskLayer2;

        public float distance = 0f;
        public float sampleRatio_R = 0f;
        public bool inverse_R = false;
        public float sampleRatio_G = 0f;
        public bool inverse_G = false;
        public float sampleRatio_B = 0f;
        public bool inverse_B = false;
        public float sampleRatio_A = 0f;
        public bool inverse_A = false;
        public float cutoff = 0.5f;
        public float smoothness = 0.5f;
        public float overlayAlpha = 1f;
        public float noiseRotation = 0f;
        public float sdfMaskDistance = 2f;
        public float sdfMaskBias = 0.5f;
        public Vector2 noiseScale = Vector2.one;
        public Vector2 noiseOffset = Vector2.zero;

        public float densityThreshold = 0.1f;
        public float brushDensity = 0.8f;

        public Vector2 randomScaleOffset = new Vector2(0.8f, 1.2f);
        public Vector2 randomPositionOffset = new Vector2(-0.2f, 0.2f);
        public float limtSlopeAngle = 45f;

        public Bounds _worldBounds;
        public List<MeshFilter> _meshFilters;
        Vector2 _scroll;

        [MenuItem("Playwith/TA/Grass Builder")]
        public static void ShowWindow()
        {
            var w = GetWindow<GrassBuilderWindow>("Grass Builder");
            w.minSize = new Vector2(400, 200);
            w.OpenWindow();
        }

        private void OnDestroy()
        {
            DestroyImmediate(_overlayMaterial);
            DestroyImmediate(_overlayMeshObject);
            DestroyImmediate(cropedTexture);
            DestroyImmediate(cropedSdfTexture);
            DestroyImmediate(maskTexture1);
            DestroyImmediate(maskTextureSDF);
            DestroyImmediate(maskTexture2);
            DestroyImmediate(densityTexture);
        }

        private bool GUIAssertion(object obj)
        {
            if (null == obj)
            {
                EditorGUILayout.EndScrollView();
                return true;
            }
            return false;
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("터레인 루트", EditorStyles.boldLabel);
                DrawTerrainRoot();
                EditorGUILayout.Space(6);
                if (GUIAssertion(_terrainRoot)) return;

                EditorGUILayout.LabelField("FoliageManager", EditorStyles.boldLabel);
                DrawFoliageManager();
                EditorGUILayout.Space(6);
                if (GUIAssertion(_foliageManager)) return;
                if (GUIAssertion(_overlayMeshObject)) return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("스플랫 & 노이즈 맵", EditorStyles.boldLabel);
                DrawTerrainTexture();
                EditorGUILayout.Space(6);
                if (GUIAssertion(splatTexture) || GUIAssertion(cropedTexture) || GUIAssertion(cropedSdfTexture)) return;

                DrawSplatProperties();
                EditorGUILayout.Space(6);
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("마스크 맵", EditorStyles.boldLabel);
                DrawMaskTextures();
                EditorGUILayout.Space(6);
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("오버레이", EditorStyles.boldLabel);
                DrawOverlay();
                DrawDensityMap();
                EditorGUILayout.Space(6);
            }
            EditorGUILayout.EndScrollView();
        }

        private void OpenWindow()
        {
            Show();

            _foliageManager = FoliageManager.FindOrCreate();
            if (null != _foliageManager && null != _foliageManager.TerrainRoot)
            {
                _terrainRoot = _foliageManager.TerrainRoot;
                InitRoot(_terrainRoot);
            }
        }

        private void Refresh()
        {
            InitRoot(_terrainRoot);
        }

        private void DrawTerrainRoot()
        {
            var height = null == _terrainRoot ? 40 : 20;
            var root = (Transform)EditorGUILayout.ObjectField(_terrainRoot, typeof(Transform), true, GUILayout.Height(height));
            if (root != _terrainRoot)
            {
                _terrainRoot = root;
                Refresh();
            }

            if (null == _terrainRoot)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Refresh", GUILayout.Width(100)))
                {
                    Refresh();
                }
                if (GUILayout.Button("Show Bounds", GUILayout.Width(100)))
                {
                    FoliageUtils.DrawBounds(_worldBounds, Color.blue, 10f);
                    UnityEngine.Debug.Log($"World Bounds: {_worldBounds.ToString()}");
                }
            }
        }

        private void DrawFoliageManager()
        {
            _foliageManager = (FoliageManager)EditorGUILayout.ObjectField(_foliageManager, typeof(FoliageManager), true);
            if (_foliageManager == null)
            {
                if (GUILayout.Button("FoliageManager 만들기", GUILayout.Width(180)))
                {
                    _foliageManager = FoliageManager.FindOrCreate();
                }
            }
            else
            {
                var paletteAsset = (FoliagePaletteAsset)EditorGUILayout.ObjectField("", _foliageManager.PaletteAsset, typeof(FoliagePaletteAsset), true);
                if (paletteAsset == null)
                {
                    if (GUILayout.Button("팔레트 만들기", GUILayout.Width(180)))
                    {
                        _foliageManager.CreatePaletteAsset();
                    }
                }
                else if (paletteAsset != _foliageManager.PaletteAsset)
                {
                    _foliageManager.SetPaletteAsset(paletteAsset);
                }
            }
        }

        private void DrawTerrainTexture()
        {
            EditorGUI.BeginChangeCheck();
            using (new EditorGUILayout.HorizontalScope())
            {
                splatTexture = DrawTexturePreview("SplatMap", splatTexture);
                cropedTexture = DrawTexturePreview("Croped", cropedTexture);
                cropedSdfTexture = DrawTexturePreview("Croped_SDF", cropedSdfTexture);
                noiseTexture = DrawTexturePreview("Noise", noiseTexture);
            }
            if (EditorGUI.EndChangeCheck())
            {
                ApplyOverlayMaterialTextures(_overlayMaterial);
            }
        }

        private void DrawMaskTextures()
        {
            EditorGUI.BeginChangeCheck();
            using (new EditorGUILayout.HorizontalScope())
            {
                maskTexture2 = DrawTexturePreview("Mask", maskTexture2);
                maskTextureSDF = DrawTexturePreview("Mask_SDF", maskTextureSDF);
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                maskLayer2 = DrawLayerMaskField("Mask Layer", maskLayer2);
                if (GUILayout.Button("Capture Mask", GUILayout.Width(150)))
                {
                    maskTexture2 = BakeMaskMap(maskLayer2);
                }
                EditorGUILayout.Space(3);
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                maskLayer1 = DrawLayerMaskField("SDF Mask Layer", maskLayer1);
                sdfMaskDistance = EditorGUILayout.Slider("SDF Mask Distance", sdfMaskDistance, 0f, 10f);
                sdfMaskBias = EditorGUILayout.Slider("SDF Mask Bias", sdfMaskBias, 0f, 1f);
                if (GUILayout.Button("Capture SDF Mask", GUILayout.Width(150)))
                {
                    maskTexture1 = BakeMaskMap(maskLayer1);
                    byte[] pixels = new byte[maskTexture1.width * maskTexture1.height];
                    SDFTexutreBaker.BakeToPngAsset(maskTexture1, DefaultSDFRadius, DefaultSDFThreshold, true, ref pixels);
                    maskTextureSDF.SetPixelData(pixels, 0);
                    maskTextureSDF.Apply(false, false);
                }
                EditorGUILayout.Space(3);
            }

            if (GUILayout.Button("Save Textures", GUILayout.Width(160)))
            {
                var sceneName = EditorSceneManager.GetActiveScene().name;
                if (string.IsNullOrEmpty(sceneName))
                    sceneName = "UntitledScene";

                var folderPath = Path.Combine(Application.temporaryCachePath, "Procedurals");
                Directory.CreateDirectory(folderPath);

                FoliageUtils.SaveTexture(cropedTexture, Path.Combine(folderPath, $"{sceneName}_Croped.png"));
                FoliageUtils.SaveTexture(cropedSdfTexture, Path.Combine(folderPath, $"{sceneName}_Croped_SDF.png"));
                FoliageUtils.SaveTexture(maskTexture1, Path.Combine(folderPath, $"{sceneName}_Mask1.png"));
                FoliageUtils.SaveTexture(maskTextureSDF, Path.Combine(folderPath, $"{sceneName}_Mask1_SDF.png"));
                FoliageUtils.SaveTexture(maskTexture2, Path.Combine(folderPath, $"{sceneName}_Mask2.png"));

                EditorUtility.RevealInFinder(folderPath);
            }
            if (EditorGUI.EndChangeCheck())
            {
                ApplyOverlayMaterialTextures(_overlayMaterial);
                SceneView.RepaintAll();
            }
        }

        private Texture2D BakeSDFTexture(Texture2D source)
        {
            var sdfTexture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, false)
            {
                filterMode = FilterMode.Point,
                anisoLevel = 0,
                wrapMode = TextureWrapMode.Clamp,
            };
            Color32[] pixels = new Color32[source.width * source.height];
            SDFTexutreBaker.BakeToPngAsset(source, SDFTexutreBaker.TargetChannel.A, DefaultSDFRadius, DefaultSDFThreshold, ref pixels);
            SDFTexutreBaker.BakeToPngAsset(source, SDFTexutreBaker.TargetChannel.R, DefaultSDFRadius, DefaultSDFThreshold, ref pixels);
            SDFTexutreBaker.BakeToPngAsset(source, SDFTexutreBaker.TargetChannel.G, DefaultSDFRadius, DefaultSDFThreshold, ref pixels);
            SDFTexutreBaker.BakeToPngAsset(source, SDFTexutreBaker.TargetChannel.B, DefaultSDFRadius, DefaultSDFThreshold, ref pixels);
            sdfTexture.SetPixels32(pixels);
            sdfTexture.Apply();
            return sdfTexture;
        }

        private LayerMask DrawLayerMaskField(string label, LayerMask mask)
        {
            int maskValue = InternalEditorUtility.LayerMaskToConcatenatedLayersMask(mask);
            maskValue = EditorGUILayout.MaskField(label, maskValue, InternalEditorUtility.layers);
            return InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(maskValue);
        }

        private void DrawSplatProperties()
        {
            EditorGUI.BeginChangeCheck();
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    sampleRatio_R = EditorGUILayout.Slider(" R", sampleRatio_R, 0f, 1f);
                    inverse_R = EditorGUILayout.Toggle(GUIContent.none, inverse_R, GUILayout.Width(18f));
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    sampleRatio_G = EditorGUILayout.Slider(" G", sampleRatio_G, 0f, 1f);
                    inverse_G = EditorGUILayout.Toggle(GUIContent.none, inverse_G, GUILayout.Width(18f));
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    sampleRatio_B = EditorGUILayout.Slider(" B", sampleRatio_B, 0f, 1f);
                    inverse_B = EditorGUILayout.Toggle(GUIContent.none, inverse_B, GUILayout.Width(18f));
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    sampleRatio_A = EditorGUILayout.Slider(" A", sampleRatio_A, 0f, 1f);
                    inverse_A = EditorGUILayout.Toggle(GUIContent.none, inverse_A, GUILayout.Width(18f));
                }
            }
            EditorGUILayout.Space(3);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                distance = EditorGUILayout.Slider("Distance", distance, -DefaultSDFRadius, DefaultSDFRadius);
                cutoff = EditorGUILayout.Slider("Cutoff", cutoff, 0f, 1f);
                smoothness = EditorGUILayout.Slider("Smooth", smoothness, 0f, 0.5f);
            }
            if (EditorGUI.EndChangeCheck())
            {
                ApplyMaerialProperties(_overlayMaterial);
                SceneView.RepaintAll();
            }
        }

        private bool isDensityTextureDirty = false;

        private void DrawOverlay()
        {
            EditorGUI.BeginChangeCheck();
            overlayAlpha = EditorGUILayout.Slider("Overlay", overlayAlpha, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                ApplyMaerialProperties(_overlayMaterial);
            }
        }

        private void DrawNoiseProperties()
        {
            if (null == noiseTexture)
                return;

            EditorGUI.BeginChangeCheck();
            noiseRotation = EditorGUILayout.Slider("Rotation", noiseRotation, 0f, 360f);
            noiseScale.x = EditorGUILayout.Slider("Tilling X", noiseScale.x, 0f, 10f);
            noiseScale.y = EditorGUILayout.Slider("Tilling Y", noiseScale.y, 0f, 10f);
            noiseOffset.x = EditorGUILayout.Slider("Offset X", noiseOffset.x, 0f, 10f);
            noiseOffset.y = EditorGUILayout.Slider("Offset Y", noiseOffset.y, 0f, 10f);
            if (EditorGUI.EndChangeCheck())
            {
                ApplyMaerialProperties(_overlayMaterial);
            }

        }

        private void DrawDensityMap()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                densityTexture = DrawTexturePreview("Density Map", densityTexture);
                if (GUILayout.Button("Density Map 생성", GUILayout.Width(160)))
                {
                    densityTexture = SaveDensityTexture();
                    isDensityTextureDirty = false;
                }
                if (null == densityTexture)
                    return;
            }
            EditorGUILayout.Space(3);
            brushDensity = EditorGUILayout.Slider("브러시 밀도", brushDensity, 0f, 5f);
            limtSlopeAngle = EditorGUILayout.Slider("최대 경사각", limtSlopeAngle, 0f, 90f);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("랜덤 스케일 범위", GUILayout.Width(120));
                Vector2 scaleOffset = randomScaleOffset;
                EditorGUILayout.MinMaxSlider(ref scaleOffset.x, ref scaleOffset.y, 0f, 1f);
                scaleOffset.x = EditorGUILayout.FloatField(scaleOffset.x, GUILayout.Width(32));
                EditorGUILayout.LabelField("~", GUILayout.Width(10));
                scaleOffset.y = EditorGUILayout.FloatField(scaleOffset.y, GUILayout.Width(30));
                if (scaleOffset != randomScaleOffset)
                {
                    randomScaleOffset = scaleOffset;
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("랜덤 포지션 범위", GUILayout.Width(120));
                Vector2 positionOffset = randomPositionOffset;
                EditorGUILayout.MinMaxSlider(ref positionOffset.x, ref positionOffset.y, -1f, 1f);
                positionOffset.x = EditorGUILayout.FloatField(positionOffset.x, GUILayout.Width(32));
                EditorGUILayout.LabelField("~", GUILayout.Width(10));
                positionOffset.y = EditorGUILayout.FloatField(positionOffset.y, GUILayout.Width(30));
                if (randomPositionOffset != positionOffset)
                {
                    randomPositionOffset = positionOffset;
                }
            }
            EditorGUILayout.Space(6);
            if (GUILayout.Button("Bake Grass", GUILayout.Width(160)))
            {
                if (_foliageManager.GetPaletteCount() == 0)
                {
                    if (EditorUtility.DisplayDialog("Grass Baking", "팔레트가 설정되어 있지 않습니다.", "확인"))
                    {
                        EditorGUIUtility.PingObject(_foliageManager.PaletteAsset);
                    }
                    return;
                }
                if (EditorUtility.DisplayDialog("Grass Baking", "이전 식생은 모두 제거 됩니다.\n계속 진행 하시겠습니까?", "Yes", "No"))
                {
                    if (isDensityTextureDirty)
                    {
                        densityTexture = SaveDensityTexture();
                        isDensityTextureDirty = false;
                    }
                    BakeGrass();
                }
            }
        }

        private Texture2D DrawTexturePreview(string label, Texture2D texture)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(80)))
            {
                GUILayout.Label(label, EditorStyles.miniLabel);
                return EditorGUILayout.ObjectField(texture, typeof(Texture2D), false, GUILayout.Width(80), GUILayout.Height(80)) as Texture2D;
            }
        }

        public void InitRoot(Transform terrainRoot)
        {
            if (Application.isPlaying)
                return;

            _terrainRoot = terrainRoot;
            _foliageManager.SetTerrainRoot(_terrainRoot);

            InitGeomentry();
            InitSplatMap();
        }

        private void InitGeomentry()
        {
            if (null == _terrainRoot)
                return;

            FoliageUtils.CollectMeshesFrom(_terrainRoot, out _meshFilters, out var bounds);
            if (null == _meshFilters || _meshFilters.Count == 0)
                return;

            _worldBounds = NormalizeBoundsSquare(bounds);
            _worldBounds.center = _terrainRoot.position + _worldBounds.center;

            var combinedMesh = BakeCombinedMesh(_meshFilters, _worldBounds);
            MeshFilter mf = null;
            if (null != combinedMesh)
            {
                _overlayMeshObject = GameObject.Find("OverlayMesh");
                if (null == _overlayMeshObject)
                    _overlayMeshObject = new GameObject("OverlayMesh");

                var mr = _overlayMeshObject.GetComponent<MeshRenderer>();
                if (null == mr)
                    mr = _overlayMeshObject.AddComponent<MeshRenderer>();
                mf = _overlayMeshObject.GetComponent<MeshFilter>();
                if (null == mf)
                    mf = _overlayMeshObject.AddComponent<MeshFilter>();

                if (mr.sharedMaterial == null || mr.sharedMaterial.shader.name != "FoliageEngine/DensityOverlayURP")
                    _overlayMaterial = new Material(Shader.Find("FoliageEngine/DensityOverlayURP"));
                mr.sharedMaterial = _overlayMaterial;
                mf.sharedMesh = combinedMesh;
            }
            _overlayMeshObject.transform.position = _worldBounds.min;

            combinedMesh.RecalculateBounds();
            _gemetryNode = NodeBuilder.CreateBVH(new MeshFilter[] { mf }, 256, out var outBounds);
        }

        private Bounds NormalizeBoundsSquare(Bounds bounds)
        {
            Vector3 size = bounds.size;
            float maxSide = Mathf.Max(size.x, size.z);
            Vector3 newSize = new Vector3(maxSide, size.y, maxSide);

            return new Bounds(bounds.center, newSize);
        }

        private void InitSplatMap()
        {
            if (null == _terrainRoot)
                return;

            var mr = _terrainRoot.GetComponentInChildren<MeshRenderer>();
            if (null == mr)
                return;

            splatTexture = mr.sharedMaterial.GetTexture("_SplatMap") as Texture2D;
            if (null == splatTexture)
                return;

            TextureImporter textureImporter = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(splatTexture)) as TextureImporter;
            var wasReadable = textureImporter.isReadable;
            textureImporter.isReadable = true;
            textureImporter.SaveAndReimport();
            FoliageUtils.GetUvMinMax(_meshFilters, out var uvMin, out var uvMax);
            cropedTexture = FoliageUtils.CropTextureByUV(splatTexture, uvMin, uvMax, SamplingResX, SamplingResY);
            cropedSdfTexture = BakeSDFTexture(cropedTexture);
            textureImporter.isReadable = wasReadable;
            textureImporter.SaveAndReimport();

            if (null == noiseTexture)
            {
                noiseTexture = Texture2D.whiteTexture;
            }
            if (null == maskTexture1)
            {
                //maskTexture1 = InitMaskTexture(SamplingResX, SamplingResY, 0);
                maskTextureSDF = InitMaskTexture(SamplingResX, SamplingResY, 0);
            }
            if (null == maskTexture2)
            {
                maskTexture2 = InitMaskTexture(SamplingResX, SamplingResY, 0);
            }

            ApplyOverlayMaterialTextures(_overlayMaterial);
        }

        private Texture2D InitMaskTexture(int width, int height, int initialValue)
        {
            var texture = new Texture2D(width, height, TextureFormat.R8, false, true)
            {
                filterMode = FilterMode.Point,
                anisoLevel = 0,
                wrapMode = TextureWrapMode.Clamp,
            };
            var data = texture.GetRawTextureData<byte>();
            for (int i = 0; i < data.Length; i++)
                data[i] = (byte)initialValue;
            texture.Apply(false, false);
            return texture;
        }

        private Texture2D BakeMaskMap(LayerMask layer)
        {
            var rtTexture = FoliageUtils.CaptureTextureByOrthoMap(_worldBounds, SamplingResX, SamplingResY, layer);
            var retTexutre = new Texture2D(rtTexture.width, rtTexture.height, TextureFormat.R8, false, false)
            {
                filterMode = FilterMode.Point,
                anisoLevel = 0,
                wrapMode = TextureWrapMode.Clamp,
            };
            retTexutre.SetPixelData(rtTexture.GetRawTextureData<byte>(), 0);
            retTexutre.Apply(false, false);
            return retTexutre;
        }

        private void ApplyOverlayMaterialTextures(Material mat)
        {
            if (mat == null) return;
            mat.SetTexture("_BaseMap", cropedTexture);
            mat.SetTexture("_BaseSDF", cropedSdfTexture);
            mat.SetTexture("_SDFMaskTex", maskTextureSDF);
            mat.SetTexture("_MaskTex", maskTexture2);
            mat.SetTexture("_NoiseTex", noiseTexture);
            ApplyMaerialProperties(mat);
            AssetPreview.SetPreviewTextureCacheSize(0);
            AssetPreview.SetPreviewTextureCacheSize(128);
            Repaint();
        }

        private void ApplyMaerialProperties(Material mat)
        {
            if (mat == null) return;

            mat.SetFloat("_Distance", distance);
            mat.SetFloat("_Sampling_R", sampleRatio_R);
            mat.SetFloat("_Inverse_R", inverse_R ? 1f : 0f);
            mat.SetFloat("_Sampling_G", sampleRatio_G);
            mat.SetFloat("_Inverse_G", inverse_G ? 1f : 0f);
            mat.SetFloat("_Sampling_B", sampleRatio_B);
            mat.SetFloat("_Inverse_B", inverse_B ? 1f : 0f);
            mat.SetFloat("_Sampling_A", sampleRatio_A);
            mat.SetFloat("_Inverse_A", inverse_A ? 1f : 0f);
            mat.SetFloat("_Cutoff", cutoff);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_NoiseRotation", noiseRotation);
            mat.SetVector("_NoiseScale", noiseScale);
            mat.SetVector("_NoiseOffset", noiseOffset);
            mat.SetFloat("_OverlayAlpha", overlayAlpha);
            mat.SetFloat("_SDFMaskDistance", sdfMaskDistance);
            mat.SetFloat("_SDFMaskBias", sdfMaskBias);

            isDensityTextureDirty = true;
        }

        private Texture2D SaveDensityTexture()
        {
            if (_overlayMaterial == null)
            {
                UnityEngine.Debug.LogWarning("OverlayMesh 또는 머티리얼을 찾지 못했습니다.");
                return null;
            }

            ApplyOverlayMaterialTextures(_overlayMaterial);

            var sceneName = EditorSceneManager.GetActiveScene().name;
            if (string.IsNullOrEmpty(sceneName))
                sceneName = "UntitledScene";
            var fileName = $"{sceneName}_DensityOverlay.png";
            var path = Path.Combine(Path.GetTempPath(), fileName);

            var prevRT = RenderTexture.active;
            var rt = RenderTexture.GetTemporary(SamplingResX, SamplingResY, 0, RenderTextureFormat.ARGB32);

            Graphics.Blit(Texture2D.whiteTexture, rt, _overlayMaterial);

            var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false, true)
            {
                filterMode = FilterMode.Point,
                anisoLevel = 0,
                wrapMode = TextureWrapMode.Clamp,
            };
            RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            texture.Apply();

            RenderTexture.active = prevRT;
            RenderTexture.ReleaseTemporary(rt);

            FoliageUtils.SaveTexture(texture, path);
            UnityEngine.Debug.Log($"Density overlay saved to: {path}");

            return texture;
        }

        public Mesh BakeCombinedMesh(List<MeshFilter> meshFilters, Bounds bounds)
        {
            if (meshFilters == null || meshFilters.Count == 0)
                return null;

            var combine = new List<CombineInstance>();
            for (int i = 0; i < meshFilters.Count; i++)
            {
                if (meshFilters[i] == null || meshFilters[i].sharedMesh == null)
                    continue;
                combine.Add(new CombineInstance
                {
                    mesh = meshFilters[i].sharedMesh,
                    transform = meshFilters[i].transform.localToWorldMatrix
                });
            }

            var mesh = new Mesh();
            mesh.name = "OverlayMesh";
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.CombineMeshes(combine.ToArray(), true, true);
            mesh.RecalculateBounds();

            Vector3[] vertices = mesh.vertices;
            Vector2[] uvs = mesh.uv;
            for (int i = 0; i < vertices.Length; i++)
            {
                var correctedVertex = vertices[i] - bounds.min;
                vertices[i] = correctedVertex;
                uvs[i] = new Vector2(correctedVertex.x / bounds.size.x, correctedVertex.z / bounds.size.z);
            }
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.RecalculateBounds();
            return mesh;
        }

        public Vector3 GetTexelWorldPosition(int x, int y, int resolutionX, int resolutionY)
        {
            return new Vector3((x / (float)resolutionX) * _worldBounds.size.x, 0, (y / (float)resolutionY) * _worldBounds.size.z) + _worldBounds.min;
        }

        public bool IsValidNode()
        {
            return null != _gemetryNode && _gemetryNode.leafNodes != null && _gemetryNode.leafNodes.Count > 0;
        }

        public bool RayTest(Vector3 pos, out Vector3 outPoint, float angeLimite = 0.7f)//약 45도
        {
            if (NodeUtils.RayTestNode(_gemetryNode, pos, 10f, out outPoint, out var outNormal, out List<RaycastTraceStep> traceSteps))
            {
                if (Vector3.Dot(outNormal, Vector3.up) > angeLimite)
                {
                    return true;
                }
            }
            return false;
        }

        public Vector3 UVToWorldPosition(Vector2 uv, Bounds bounds)
        {
            var px = uv.x * bounds.size.x;
            var pz = uv.y * bounds.size.z;
            return new Vector3(bounds.min.x + px, bounds.max.y, bounds.min.z + pz);
        }

        public void BakeGrass()
        {
            if (null == densityTexture)
                return;

            if (null == _foliageManager.PaletteAsset)
            {
                _foliageManager.CreatePaletteAsset();
                EditorUtility.DisplayDialog("Grass Baking", "팔레트가 비어있습니다. 식생을 추가해 주세요.", "확인");
                return;
            }

            var foliageManager = FoliageManager.FindOrCreate();
            if (!foliageManager.IsInitialized)
                foliageManager.Initialize();

            foliageManager.PaletteAsset.Initialize(true);
            foliageManager.ClearFoliages();

            var radius = GetBrushRadius();
            var slotData = GetFoliagePaletteSlotData(DefaultPaletteID);
            if (null == slotData)
                return;

            var width = densityTexture.width;
            var height = densityTexture.height;
            var texels = SampleTexel.Create(densityTexture, new Vector2Int(width, height), SampleTexel.SampleChannel.A, Vector2.zero, Vector2.one);
            var slopeLimit = Mathf.Cos(limtSlopeAngle * Mathf.Deg2Rad);

            var total = Mathf.Max(1, width * height);
            var processed = 0;
            var progressStep = Mathf.Max(1, total / 100);

            int totalGrassCount = 0;
            var startTime = EditorApplication.timeSinceStartup;
            EditorUtility.DisplayProgressBar("Grass Baking", "준비 중...", 0f);
            try
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        var densityValue = texels.GetArea9WeightedAverage(x, y);
                        if (densityValue > densityThreshold)
                        {
                            var foliageRadius = slotData.Radius;
                            var sampleDist = FoliageUtils.ToSampleDistance(brushDensity, foliageRadius);
                            var position = GetTexelWorldPosition(x, y, width, height);
                            var scale = new Vector2(randomScaleOffset.x, randomScaleOffset.y);

                            var addFoliages = BakeGrass(position, DefaultPaletteID, radius, sampleDist, slopeLimit, scale, randomPositionOffset, 4);
                            foliageManager.AddFoliages(DefaultPaletteID, addFoliages);
                            totalGrassCount += addFoliages.Count;
                        }

                        processed++;
                        if (processed % progressStep == 0 || processed >= total)
                        {
                            var progress = processed / (float)total;
                            EditorUtility.DisplayProgressBar("Grass Baking.", $"베이크 중... {processed}/{total}", progress);
                        }
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                //SaveAssetEvent.HandleSave();
                foliageManager.Update();
                var elapsedTime = EditorApplication.timeSinceStartup - startTime;
                var fps = totalGrassCount / elapsedTime;
                var cpuScore = (fps / 800f) * 10000f;
                EditorUtility.DisplayDialog("베이킹 완료!",
                $"생성된 수: {totalGrassCount:N0}개" +
                $"\n걸린 시간: {elapsedTime:F0}초" +
                //"\n 초당 {fps}:F0}개" +
                $"\n\n당신의 CPU 점수: {cpuScore:N0}", "확인");

                SaveAssetEvent.HandleSave();
            }
        }

        public List<FoliageInstance_Editor> BakeGrass(Vector3 position, int paletteID, float radius, float sampleDist, float slopeLimit, Vector2 randScaleOffset, Vector2 randPosOffset, int maxAttempts)
        {
            var samplePoints = FoliageUtils.PoissonDiskSampleCircle(radius, sampleDist, maxAttempts);
            var addFoliages = new List<FoliageInstance_Editor>();
            foreach (var samplePoint in samplePoints)
            {
                var worldPoint = position + new Vector3(samplePoint.x, 100f, samplePoint.y);
                if (RayTest(worldPoint, out Vector3 outPoint, slopeLimit))
                {
                    var randomRotY = Random.Range(0f, 360f);
                    var randomScale = Random.Range(randScaleOffset.x, randScaleOffset.y);
                    if (randomScale < 0.1f)
                        continue;
                    var randomPos = Random.Range(randPosOffset.x, randPosOffset.y);
                    var randomOffset = new Vector3(randomPos, 0, randomPos);
                    var foliage = new FoliageInstance_Editor(paletteID, 0, outPoint + randomOffset, randomRotY, randomScale, new Bounds(outPoint, Vector3.one * 0.5f));
                    addFoliages.Add(foliage);
                }
            }
            return addFoliages;
        }

        private float GetBrushRadius()
        {
            var x = _worldBounds.size.x;
            var z = _worldBounds.size.z;
            var px = x / SamplingResX;
            var pz = z / SamplingResY;
            return (px + pz) * 0.5f;
        }

        private FoliagePaletteSlotData GetFoliagePaletteSlotData(int paletteID)
        {
            return _foliageManager.GetPalette(paletteID);
        }
    }
}
