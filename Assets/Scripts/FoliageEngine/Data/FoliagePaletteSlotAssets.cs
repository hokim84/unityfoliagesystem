using System;
using UnityEngine;

namespace PWTA
{
    [Serializable]
    public class FoliagePaletteSlotData
    {
        public bool enable = true;
        public int slotIdx = 0;
        public GameObject prefab;
        public Vector3 positionOffset = Vector3.zero;
        public Quaternion rotationOffset = Quaternion.identity;
        public Vector3 scaleOffset = Vector3.one;
        private float _radius = -1f;
        public float Radius
        {
            get
            {
                if (_radius < 0f)
                    _radius = Mathf.Max(Bounds.size.x, Bounds.size.z);
                return _radius;
            }
        }

        public virtual void Initialize()
        {
            if (prefab != null)
            {
                var mf = prefab.GetComponentInChildren<MeshFilter>();
                if (null == mf)
                    return;

                _mesh = mf.sharedMesh;
                if (mf.transform == prefab.transform)
                {
                    PositionOffset = Vector3.zero;
                    RotationOffset = Quaternion.identity;
                    ScaleOffset = Vector3.one;
                }
                else
                {
                    PositionOffset = mf.transform.localPosition;
                    RotationOffset = mf.transform.localRotation;
                    ScaleOffset = mf.transform.localScale;
                }
                ScaleOffset = mf.transform.localScale;
                var mats = prefab.GetComponentInChildren<MeshRenderer>().sharedMaterials;
                var matCount = mats.Length;
                _materials = new Material[matCount];
                for (int i = 0; i < mats.Length; i++)
                {
                    _materials[i] = new Material(mats[i]);
                }
            }
            else
            {
                Debug.LogWarning($"prefab not set");
            }

            _isInitialized = true;
        }

        protected bool _isInitialized = false;
        public bool IsInitialized => _isInitialized;
        public int SlotIdx => slotIdx;
        protected Mesh _mesh;
        public Mesh Mesh => _mesh;
        protected Material[] _materials;
        public Material[] Materials => _materials;
        public Bounds Bounds => Mesh.bounds;
        public GameObject Prefab { get => prefab; set => prefab = value; }
        public Vector3 PositionOffset { get => positionOffset; set => positionOffset = value; }
        public Quaternion RotationOffset { get => rotationOffset; set => rotationOffset = value; }
        public Vector3 ScaleOffset { get => scaleOffset; set => scaleOffset = value; }
    }
}