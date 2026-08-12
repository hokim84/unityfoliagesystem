using UnityEngine;
using System.Collections.Generic;
using System;
using UnityEngine.Rendering;

namespace PWTA
{
    public class FoliageEngine : IDisposable
    {
        public static uint AllCount = 0;
        public static uint VisCount = 0;
        public static bool ShowCount = false;        
        public static float DRAW_DISTANCE = 100f;
        private FoliageEngine() { }
        private static FoliageEngine _instance;
        public static FoliageEngine Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new FoliageEngine();
                }

                return _instance;
            }
        }
        private FoliagePaletteAsset _palettesAsset = new FoliagePaletteAsset();
        private List<FoliageRenderGroup> _foliageRenderGroups = new List<FoliageRenderGroup>();
        private FoliageGrid _foliageGrid = new FoliageGrid();
        private bool isInitialized = false;
        private float _boundScale = 1000f;
        public float BoundScale
        {
            get => _boundScale;
            set => _boundScale = value;
        }
        private IPositionProvider _positionProvider;
        private ComputeShader _csCulling;
        public ComputeShader csCulling => _csCulling;
        public bool enabled = true;
        public void SetCullingShader(ComputeShader computeShader)
        {
            _csCulling = computeShader;
            foreach (var pair in _foliageRenderGroups)
            {
                pair.csCulling = _csCulling;
                pair.RefreshBuffer();
            }
        }

        public void Initialize(FoliagePaletteAsset paletteAsset, IPositionProvider positionProvider, ComputeShader computeShader, bool isForced)
        {
            if (isInitialized && !isForced)
                return;

            isInitialized = true;
            _foliageRenderGroups.Clear();

            _positionProvider = positionProvider;
            _csCulling = computeShader;
            SetPaletteAsset(paletteAsset);

            //_foliageGrid.OnActiveGridChanged += OnActiveGridChanged;
            RenderPipelineManager.beginCameraRendering -= OnCameraRender;
            RenderPipelineManager.beginCameraRendering += OnCameraRender;
            Debug.Log("FoliageEngine Initialized");
        }

        public void SetPaletteAsset(FoliagePaletteAsset paletteAsset)
        {
            _palettesAsset = paletteAsset;
            for (int i = 0; i < _palettesAsset.Count; i++)
            {
                var slot = _palettesAsset.GetSlot(i);
                if (!IsExistRenderGroup(slot.SlotIdx))
                {
                    CreateRenderGroup(slot);
                }
                else
                {
                    _foliageRenderGroups[slot.SlotIdx].paletteAsset = _palettesAsset;
                    _foliageRenderGroups[slot.SlotIdx].SetDirty();
                }
            }
            Refresh();
        }

        private void OnActiveGridChanged(HashSet<Vector2Int> activeGridCell)
        {
            Refresh();
        }

        public void Refresh()
        {
            foreach (var pair in _foliageRenderGroups)
            {
                pair.RefreshBuffer();
            }
        }

        public void InitCullingProcess()
        {

        }

        public void AddPaletteSlot(FoliagePaletteSlotData paletteSlot)
        {            
            CreateRenderGroup(paletteSlot);
            _foliageRenderGroups[paletteSlot.slotIdx].SetDirty();
        }

        public void RemovePaletteSlot(int paletteSlotIdx)
        {         
            RemoveRenderGroup(paletteSlotIdx);            
        }

        public void RefreshRenderGroup(int paletteSlotIdx)
        {            
            if (IsExistRenderGroup(paletteSlotIdx))
            {                
                _foliageRenderGroups[paletteSlotIdx].RefreshRenderData();
            }
        }

        public void SetDirtyRenderGroup(int paletteSlotIdx)
        {
            if (IsExistRenderGroup(paletteSlotIdx))
                _foliageRenderGroups[paletteSlotIdx].SetDirty();
        }

        public void ApplyToGrid(List<IFoliageElement> foliages)
        {
            _foliageGrid.ApplyCoord(foliages);
        }

        public bool IsExistRenderGroup(int paletteSlotIdx)
        {
            return _foliageRenderGroups.Count > paletteSlotIdx;
        }

        public FoliageRenderGroup CreateRenderGroup(FoliagePaletteSlotData palette)
        {
            if (IsExistRenderGroup(palette.SlotIdx))
            {
                Debug.LogWarning($"[CreateRenderGroup]RenderGroup already exists for paletteID: {palette.SlotIdx}, Overwriting...");
                _foliageRenderGroups.RemoveAt(palette.SlotIdx);
            }

            var renderGroup = FoliageRenderGroup.CreateFoliageRenderGroup(palette.SlotIdx, _palettesAsset, _csCulling);
            renderGroup.SetDirty();
            _foliageRenderGroups.Add(renderGroup);

            return renderGroup;
        }

        public void RemoveRenderGroup(int paletteSlotIdx)
        {
            if (IsExistRenderGroup(paletteSlotIdx))
            {
                _foliageRenderGroups.RemoveAt(paletteSlotIdx);                
            }
        }

        private Vector3 _lastCameraPos;
        private float _frameRate = 1f / 30f;
        private float _elapsedTime = 0;
        public void OnManualDraw(Camera renderCamera)
        {
            if (!isInitialized)
                return;

            if (!Application.isPlaying && renderCamera.cameraType != CameraType.SceneView)
                return;

            var cameraPosition = renderCamera.transform.position;
            var cameraForward = renderCamera.transform.forward;

            _foliageGrid.Update(cameraPosition);

            AllCount = 0;
            VisCount = 0;
            var bounds = new Bounds(cameraPosition, Vector3.one * _boundScale);
            foreach (var group in _foliageRenderGroups)
            {
                group.DrawIndirect(cameraPosition, cameraForward, bounds);
                AllCount += (uint)group.allCount;
                VisCount += group.visibleCount;
            }

            _lastCameraPos = cameraPosition;
            //SceneView.RepaintAll();
        }

        public void OnCameraRender(ScriptableRenderContext context, Camera renderCamera)
        {
            if (!isInitialized || !enabled)
                return;

            if (!Application.isPlaying || renderCamera.cameraType != CameraType.Game)
                return;

            //Debug.Log("OnCameraRender");
            //var camera = renderCamera;// _positionProvider.GetCurrentCamera();
            var cameraPosition = _positionProvider.GetCurrentPosition();
            var cameraForward = _positionProvider.GetCurrentForward();
            var bounds = new Bounds(cameraPosition, Vector3.one * _boundScale);

            _foliageGrid.Update(cameraPosition);

            AllCount = 0;
            VisCount = 0;

            foreach (var group in _foliageRenderGroups)
            {
                group.DrawIndirect(cameraPosition, cameraForward, bounds);
                AllCount += (uint)group.allCount;
                VisCount += group.visibleCount;
            }
        }

        public void ResetRenderGroup()
        {
            foreach (var group in _foliageRenderGroups)
            {
                group.Dispose();
            }
            _foliageRenderGroups.Clear();
        }

        public void RefreshRenderGroup()
        {
            foreach (var group in _foliageRenderGroups)
            {
                group.RefreshBuffer();
            }
        }

        public void ResetPalette()
        {
            _palettesAsset.ClearPalette();
        }

        public void Dispose()
        {
            Debug.Log("FoliageEngine Dispose");
            RenderPipelineManager.beginCameraRendering -= OnCameraRender;
            foreach (var group in _foliageRenderGroups)
            {
                group.Dispose();
            }
            _foliageRenderGroups.Clear();
        }
    }
}