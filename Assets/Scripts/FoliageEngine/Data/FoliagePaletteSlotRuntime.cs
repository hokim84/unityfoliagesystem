using System;
using UnityEngine;
using UnityEditor;

namespace PWTA
{
    [Serializable]
    public class FoliagePaletteSlotRuntime : FoliagePaletteSlotData
    {
        public string PrefabPath { get; private set; }

        public FoliagePaletteSlotRuntime(FoliagePaletteSlotData palette)
        {
            slotIdx = palette.SlotIdx;
            prefab = palette.Prefab;
            PositionOffset = palette.PositionOffset;
            RotationOffset = palette.RotationOffset;
            ScaleOffset = palette.ScaleOffset;
            _isInitialized = false;
        }

        public FoliagePaletteSlotRuntime(int slotIdx, GameObject prefabObject, string prefabPath, Vector3 positionOffset, Quaternion rotationOffset, Vector3 scaleOffset)
        {
            base.slotIdx = slotIdx;
            prefab = prefabObject;
            PrefabPath = prefabPath;
            PositionOffset = positionOffset;
            RotationOffset = rotationOffset;
            ScaleOffset = scaleOffset;
            _isInitialized = false;
        }

        public FoliagePaletteSlotRuntime(int idx, string assetPath)
        {
            slotIdx = idx;
            PrefabPath = assetPath;
            _isInitialized = false;
        }

        public override void Initialize()
        {
            if (!string.IsNullOrEmpty(PrefabPath) && prefab == null)
                LoadPrefab();

            base.Initialize();
        }

        public void LoadPrefab()
        {
            prefab = Resources.Load<GameObject>(PrefabPath);
#if UNITY_EDITOR
            if (prefab == null)
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
#endif
        }

        public Bounds Bounds => Mesh.bounds;
    }
}