using System;
using UnityEngine;

namespace PWTA
{
    [Serializable]
    public class FoliageInstance_Runtime : IFoliageElement
    {
        public Vector2Int GridCoord { get; set; }
        public int PaletteSlotIdx { get; set; }
        public Vector3 Position { get; set; }
        public float RotationY { get; set; }
        public float UniformScale { get; set; }

        public override bool Equals(object obj)
        {
            if (obj is not FoliageInstance_Runtime other)
                return false;

            return                
                PaletteSlotIdx == other.PaletteSlotIdx &&                
                Mathf.Approximately(Position.x, other.Position.x) &&
                Mathf.Approximately(Position.y, other.Position.y) &&
                Mathf.Approximately(Position.z, other.Position.z);
        }

        public override int GetHashCode()
        {
            int hash = 17;            
            hash = hash * 31 + PaletteSlotIdx.GetHashCode();
            hash = hash * 31 + Position.GetHashCode();            
            return hash;
        }

        public ulong GetChecksum(ulong offset, ulong prime)
        {
            ulong h = offset;
            ulong p = prime;

            void Mix(ulong v) { unchecked { h ^= v; h *= p; } }

            Mix((uint)PaletteSlotIdx);

            // 예: 1cm 단위로 고정
            const float step = 0.01f;
            int qx = Mathf.RoundToInt(Position.x / step);
            int qy = Mathf.RoundToInt(Position.y / step);
            int qz = Mathf.RoundToInt(Position.z / step);

            Mix((uint)qx);
            Mix((uint)qy);
            Mix((uint)qz);

            return h;
        }
    }
}