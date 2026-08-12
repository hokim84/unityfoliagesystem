using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace PWTA
{
#if UNITY_EDITOR
    public class SaveAssetEvent : AssetModificationProcessor
    {
        static bool _queued;
        public static string[] OnWillSaveAssets(string[] paths)
        {
            if (_queued)
                return paths;

            _queued = true;

            EditorApplication.delayCall += () =>
            {
                _queued = false;
                HandleSave();
            };

            return paths;
        }

        public static void HandleSave()
        {
            var foliageManager = FoliageManager.Find();
            if (foliageManager == null || foliageManager.PaletteAsset == null)
                return;

            var initialChecksum = foliageManager.PaletteAsset.InitialChecksum;
            var currentChecksum = foliageManager.PaletteAsset.GetChecksum();

            if (initialChecksum == currentChecksum)
            {
                Debug.Log("FoliagePaletteAsset.Save: No changes to save");
                return;
            }

            var initalCount = foliageManager.PaletteAsset.InitialFoliageCount;
            var updatedCount = foliageManager.GetFoliageCount();

            if (EditorUtility.DisplayDialog(
                "식생 정보 저장",
                $"수정 전 개수: {initalCount}\n수정 후 개수: {updatedCount}\n\n저장하시겠습니까?(취소해도 씬은 저장됩니다.)",
                "Save",
                "Cancel"))
            {
                foliageManager.Save();
            }
        }
    }

    [InitializeOnLoad]
    public static class DomainReloadHook
    {
        static DomainReloadHook()
        {
            var instance = FoliageManager.FindOrCreate();
            if (null != instance)
                instance.Initialize(true);
        }
    }
#endif

    [ExecuteAlways]
    public class FoliageManager : MonoBehaviour, IPositionProvider
    {
        public enum GeometryNodeType
        {
            BVH,
            QuadTree,
            Octree,
        }
        public enum RenderMode
        {
            None,
            Prefab,
            Instance,
        }

        private RenderMode renderMode = RenderMode.Instance;

        public enum RunningMode
        {
            Edit,
            Play,
        }
        public RunningMode runningMode = RunningMode.Edit;

        public static float[] WORLD_DENSITY_TABLE = {0f, 0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.8f, 0.9f, 1f};        
        public static int WORLD_DENSITY_LEVEL = 10;

        public static float DrawDistance
        {
            get => FoliageEngine.DRAW_DISTANCE;
            set => FoliageEngine.DRAW_DISTANCE = value;
        }

        public ComputeShader _foliageComputeShader;
        private List<GameObject> _foliagesAsGameObject = new List<GameObject>();

        [SerializeField]
        private FoliageEngine _foliageEngine;
        public FoliageEngine FoliageEngine => _foliageEngine;
        private MeshGeometryNodeBase _geometryNode;
        public MeshGeometryNodeBase GeometryNode => _geometryNode;
        private MeshGeometryNodeBase _bvhGeometryNode;
        private MeshGeometryNodeBase _quadTreeGeometryNode;
        private MeshGeometryNodeBase _octreeGeometryNode;
        private QuadTreeFoliageNode _foliageNode;
        public QuadTreeFoliageNode FoliageNode => _foliageNode;

        [SerializeField]
        private Transform _terrainRoot;
        public Transform TerrainRoot => _terrainRoot;
        public void SetTerrainRoot(Transform terrainRoot)
        {
            _terrainRoot = terrainRoot;
        }

        public Camera _camera;
        private bool _isInitialized = false;
        public bool IsInitialized => _isInitialized;

        [SerializeField]
        private FoliagePaletteAsset _paletteAsset;
        public FoliagePaletteAsset PaletteAsset => _paletteAsset;
        public Bounds worldBounds = new Bounds();
        public float splitSize = 10f;
        public float splitHeight = 10f;        
        private GeometryNodeType _geometryNodeType = GeometryNodeType.BVH;
        public GeometryNodeType GeometryNodeSelection
        {
            get => _geometryNodeType;
            set
            {
                if (_geometryNodeType == value)
                    return;

                _geometryNodeType = value;
                ApplyGeometryNodeSelection();
            }
        }
        public event Action OnFoliageChange
        {
            add
            {
                OnFoliageChange += value;
            }
            remove
            {
                OnFoliageChange -= value;
            }
        }

        public static FoliageManager Find()
        {
            return FindFirstObjectByType<FoliageManager>(FindObjectsInactive.Include);
        }

        public static FoliageManager FindOrCreate()
        {
            var instance = Find();
            if (instance == null)
            {
                var go = new GameObject("FoliageManager");
                instance = go.AddComponent<FoliageManager>();
                var couputeShader = Resources.Load<ComputeShader>("FoliageDistanceCull");
                instance._foliageComputeShader = couputeShader;
            }
            return instance;
        }

        public void Start()
        {
            runningMode = Application.isPlaying ? RunningMode.Play : RunningMode.Edit;
            Initialize(true);
        }

        public void Initialize(bool force = false)
        {
            if (_isInitialized && !force)
            {
                Debug.LogWarning("FoliageManager is already initialized");
                return;
            }

            if (null == _paletteAsset)
            {
                Debug.LogWarning("PaletteAsset is null");
                return;
            }

            if (runningMode == RunningMode.Edit)
                InitializeNodes();

            _paletteAsset.Initialize(true);
            _paletteAsset.LoadFoliages(WORLD_DENSITY_TABLE[WORLD_DENSITY_LEVEL]);

            _foliageEngine = FoliageEngine.Instance;
            _foliageEngine.Initialize(_paletteAsset, this, _foliageComputeShader, true);

            _isInitialized = true;
        }

        public void SetPaletteAsset(FoliagePaletteAsset paletteAsset)
        {
            _paletteAsset = paletteAsset;
            Initialize(true);
        }

        public void CreatePaletteAsset()
        {
            _paletteAsset = CreateFoliagePaletteAsset();
            SetPaletteAsset(_paletteAsset);
        }

#if UNITY_EDITOR
        public static FoliagePaletteAsset CreateFoliagePaletteAsset()
        {
            var paletteAsset = ScriptableObject.CreateInstance<FoliagePaletteAsset>();
            var defaultPalettePath = FoliageUtils.GetDefaultPalettePath();
            var assetPath = defaultPalettePath.Replace(Application.dataPath, "Assets");
            AssetDatabase.CreateAsset(paletteAsset, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return paletteAsset;
        }
#endif
        public void InitializeNodes()
        {
#if UNITY_EDITOR
            if (null == _terrainRoot)
            {
                Debug.LogError("TerrainRoot is not null");
                return;
            }
            FoliageUtils.CollectMeshesFrom(_terrainRoot, out List<MeshFilter> meshFilters, out worldBounds);
            var importers = GetImporter(meshFilters);
            var importer = importers[0];

            bool wasReadable = false;
            SetReadable(importer, out wasReadable);

            // CodeTimer.Measure("QuadTree 생성", () =>
            // {
            //     _quadTreeGeometryNode = NodeBuilder.CreateQuadTree(meshFilters, splitSize, splitHeight, out worldBounds);
            // });
            // CodeTimer.Measure("Octree 생성", () =>
            // {
            //     _octreeGeometryNode = NodeBuilder.CreateOctree(meshFilters, splitSize, splitHeight, out worldBounds);
            // });
            // CodeTimer.Measure("BVH 생성", () =>
            // {
                _bvhGeometryNode = NodeBuilder.CreateBVH(meshFilters, 128, out worldBounds);
            // });
            ApplyGeometryNodeSelection();

            RestoreReadable(importer, wasReadable);
#endif
        }

        public bool CheckGeometryNodeValid()
        {
            if (null == _geometryNode || null == _geometryNode.leafNodes || _geometryNode.leafNodes.Count == 0)
                return false;

            return true;
        }

        public void ApplyGeometryNodeSelection()
        {
            if (_bvhGeometryNode == null && _quadTreeGeometryNode == null && _octreeGeometryNode == null)
                return;

            switch (_geometryNodeType)
            {
                case GeometryNodeType.QuadTree:
                    _geometryNode = _quadTreeGeometryNode ?? _bvhGeometryNode ?? _octreeGeometryNode;
                    break;
                case GeometryNodeType.Octree:
                    _geometryNode = _octreeGeometryNode ?? _bvhGeometryNode ?? _quadTreeGeometryNode;
                    break;
                default:
                    _geometryNode = _bvhGeometryNode ?? _quadTreeGeometryNode ?? _octreeGeometryNode;
                    break;
            }
        }

#if UNITY_EDITOR
        private List<ModelImporter> GetImporter(IEnumerable<MeshFilter> meshFilters)
        {
            List<ModelImporter> meshImporters = new List<ModelImporter>();
            foreach (var meshFilter in meshFilters)
            {
                var modelPath = AssetDatabase.GetAssetPath(meshFilter.sharedMesh);
                var meshImporter = AssetImporter.GetAtPath(modelPath) as ModelImporter;
                if (null == meshImporter)
                    continue;
                if (meshImporters.Contains(meshImporter))
                    continue;
                meshImporters.Add(meshImporter);
            }
            return meshImporters;
        }

        private void SetReadable(ModelImporter meshImporter, out bool wasReadable)
        {
            wasReadable = meshImporter.isReadable;
            meshImporter.isReadable = true;
            meshImporter.SaveAndReimport();
        }

        private void RestoreReadable(ModelImporter meshImporter, bool wasReadable)
        {
            meshImporter.isReadable = wasReadable;
            meshImporter.SaveAndReimport();
        }
#endif
        public bool CheckPaletteSlotExists(GameObject prefab)
        {
            return _paletteAsset.Slots.Any(slot => slot.prefab == prefab);
        }

        public void SetPaletteSlot(int slotIdx, FoliagePaletteSlotData slot)
        {
            if (null != _paletteAsset)
                _paletteAsset.SetSlot(slotIdx, slot);
            if (null != _foliageEngine)
                _foliageEngine.RefreshRenderGroup(slotIdx);
        }

        public void AddPaletteSlot(FoliagePaletteSlotData slot)
        {
            if (slot == null)
                return;

            if (!slot.IsInitialized)
                slot.Initialize();

            if (null != _paletteAsset)
                _paletteAsset.Add(slot);

            if (null != _foliageEngine)
                _foliageEngine.AddPaletteSlot(slot);

            Update();
        }

        public void RemovePaletteSlot(int slotIdx)
        {
            if (null != _paletteAsset)
                _paletteAsset.Remove(slotIdx);
            if (null != _foliageEngine)
                _foliageEngine.RemovePaletteSlot(slotIdx);

            Update();
        }

        public void AddFoliage(int slotIdx, FoliageInstance_Editor foliage)
        {
            if (null == foliage)
                return;
            if (null != _paletteAsset)
                _paletteAsset.AddFoliage(slotIdx, foliage, true);
            if (null != _foliageEngine)
                _foliageEngine.SetDirtyRenderGroup(slotIdx);
        }

        public void AddFoliages(int slotIdx, IEnumerable<FoliageInstance_Editor> foliages)
        {
            if (null != _paletteAsset)
                _paletteAsset.AddFoliages(slotIdx, foliages, true);
            if (null != _foliageEngine)
                _foliageEngine.SetDirtyRenderGroup(slotIdx);
        }

        public void RemoveFoliages(int slotIdx, IEnumerable<FoliageInstance_Editor> foliages)
        {
            _paletteAsset.RemoveFoliages(slotIdx, foliages);
            _foliageEngine.SetDirtyRenderGroup(slotIdx);
        }

        public void RemoveEachFoliages(IEnumerable<FoliageInstance_Editor> foliages)
        {
            foreach (var foliage in foliages)
            {
                _paletteAsset.RemoveFoliage(foliage.PaletteSlotIdx, foliage);
                _foliageEngine.SetDirtyRenderGroup(foliage.PaletteSlotIdx);
            }
        }

        public int GetPaletteCount()
        {
            return _paletteAsset?.Count ?? 0;
        }

        public int GetFoliageCount()
        {
            return _paletteAsset?.GetFoliageCount() ?? 0;
        }

        public FoliagePaletteSlotData GetPalette(int paletteID)
        {
            return _paletteAsset.GetSlot(paletteID);
        }

        public FoliagePaletteSlotData GetPalette(GameObject prefab)
        {
            return _paletteAsset.Slots.FirstOrDefault(slot => slot.prefab == prefab);
        }

        public Vector3 GetCurrentPosition()
        {
            var cam = GetCurrentCamera();
            return null == cam ? Vector3.zero : cam.transform.position;
        }

        public Vector3 GetCurrentForward()
        {
            var cam = GetCurrentCamera();
            return null == cam ? Vector3.zero : cam.transform.forward;
        }

        public Camera GetCurrentCamera()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                return SceneView.lastActiveSceneView.camera;
            }
#endif
            if (null != _camera)
            {
                return _camera;
            }
            return Camera.main;
        }

#if UNITY_EDITOR
        Camera _lastCam;
        void OnRenderObject()
        {
            if (Application.isPlaying)
                return;

            var cam = Camera.current;
            if (cam == null) return;

            // SceneView 카메라만 필터링
            if (cam.cameraType != CameraType.SceneView &&
                cam.cameraType != CameraType.Game)
                return;

            // 중복 카메라 방지
            if (_lastCam == cam)
                return;

            _lastCam = cam;

            DrawFoliage(SceneView.lastActiveSceneView.camera);
        }

        void LateUpdate()
        {
            _lastCam = null;
        }
#endif

        public bool reloadTrigger = false;
        public void Update()
        {
            if (reloadTrigger)
            {
                reloadTrigger = false;
                Initialize(true);
            }

            if (Application.isPlaying)
                return;

#if UNITY_EDITOR
            if (null == SceneView.lastActiveSceneView || null == SceneView.lastActiveSceneView.camera)
                return;
            var camera = SceneView.lastActiveSceneView.camera;
#else
            var camera = Camera.main;
#endif
            //Debug.Log("Update");
            DrawFoliage(camera);
        }

        private void DrawFoliage(Camera camera)
        {
            if (null != _foliageEngine)
                _foliageEngine.OnManualDraw(camera);
        }

        public void SetRenderMode(RenderMode mode)
        {
            if (renderMode == mode)
                return;

            if (renderMode != RenderMode.Prefab && mode == RenderMode.Prefab)
            {
                ClearGameObjects();
                _foliagesAsGameObject.Clear();
                GenerateGameObject();
            }
            else if (renderMode != RenderMode.Instance && mode == RenderMode.Instance)
            {
                ClearGameObjects();
            }

            renderMode = mode;
        }

        private void ClearGameObjects()
        {
            for (int i = 0; i < _foliagesAsGameObject.Count; ++i)
            {
                if (Application.isPlaying)
                    Destroy(_foliagesAsGameObject[i]);
                else
                    DestroyImmediate(_foliagesAsGameObject[i]);
            }
        }

        public void GenerateGameObject()
        {
            CodeTimer.Measure("Generate GameObject", () =>
            {
                foreach (var f in _paletteAsset.GetFoliages())
                {
                    var slot = _paletteAsset.GetSlot(f.PaletteSlotIdx);
                    var prefabName = slot.Prefab.name;
                    var inst = Instantiate(slot.Prefab);

                    var parent = this.transform.Find(prefabName);
                    if (parent == null)
                    {
                        var go = new GameObject(prefabName);
                        go.transform.parent = this.transform;
                        go.transform.localPosition = Vector3.zero;
                        go.transform.localRotation = Quaternion.identity;
                        go.transform.localScale = Vector3.one;
                        parent = go.transform;
                    }

                    inst.transform.parent = parent;
                    inst.transform.localPosition = f.Position;
                    inst.transform.localRotation = Quaternion.Euler(0f, f.RotationY, 0f);
                    inst.transform.localScale = Vector3.one * f.UniformScale;
                    _foliagesAsGameObject.Add(inst);
                }
            });
        }

        public void Save()
        {
            if (null != _paletteAsset)
                _paletteAsset.SaveFoliages();
        }

        public void Reset()
        {
            if (null != _paletteAsset)
            {
                _paletteAsset.ClearFoliages();
            }

            if (_foliageEngine != null)
            {
                _foliageEngine.ResetRenderGroup();
            }

            ClearGameObjects();
            GC.Collect();
        }

        public void ClearFoliages()
        {
            if (null != _paletteAsset)
                _paletteAsset.ClearFoliages();

            _foliageEngine.RefreshRenderGroup();
        }

        public ulong GetFoliageChecksum()
        {
            return _paletteAsset.GetChecksum();
        }

        private void OnDestroy()
        {
            _foliageEngine?.Dispose();
            _paletteAsset?.Dispose();
            _isInitialized = false;
        }
    }
}