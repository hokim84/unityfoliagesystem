using System;
using UnityEngine;

namespace PWTA
{
    [Serializable]
    public class FoliageInstance_Editor : FoliageInstance_Runtime
    {        
        public int PatchIDX = 0;        
        public Matrix4x4 Matrix;
        public Bounds Bounds;

        public FoliageInstance_Editor(FoliagePaletteSlotData palette, int patchIdx, FoliageInstance_Editor runtimeInstance)
        {
            PaletteSlotIdx = palette.SlotIdx;
            PatchIDX = patchIdx;
            Position = runtimeInstance.Position;
            RotationY = runtimeInstance.RotationY;
            UniformScale = runtimeInstance.UniformScale;
            Matrix = FoliageUtils.GetMatrix(runtimeInstance, palette);
            Bounds = new Bounds(Position, palette.Bounds.size);
        }
        
        public FoliageInstance_Editor(FoliagePaletteSlotData palette, int patchIdx, Vector3 position, float rotationY, float uniformScale)
        {
            PaletteSlotIdx = palette.SlotIdx;
            PatchIDX = patchIdx;            
            Position = position;
            RotationY = rotationY;
            UniformScale = uniformScale;
            Matrix = FoliageUtils.GetMatrix(this);
            Bounds = new Bounds(Position, palette.Bounds.size);
        }

        public FoliageInstance_Editor(FoliagePaletteSlotData palette, int patchIdx, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            PaletteSlotIdx = palette.SlotIdx;
            PatchIDX = patchIdx;            
            Position = position;
            RotationY = rotation.y;
            UniformScale = (scale.x + scale.y + scale.z) / 3f;
            Matrix = FoliageUtils.GetMatrix(this);
            Bounds = new Bounds(Position, palette.Bounds.size);
        }
        
        public FoliageInstance_Editor(int paletteID, int patchIdx, Vector3 position, float rotationY, float uniformScale, Bounds bounds)
        {
            PaletteSlotIdx = paletteID;
            PatchIDX = patchIdx;            
            Position = position;
            RotationY = rotationY;
            UniformScale = uniformScale;            
            Matrix = FoliageUtils.GetMatrix(this);
            Bounds = new Bounds(Position, bounds.size);
        }
    }
} 