using UnityEngine;

namespace PWTA
{
    public interface IFoliageElement
    {
        int PaletteSlotIdx {get; set;}
        Vector3 Position {get; set;}
        float RotationY {get; set;}
        float UniformScale {get; set;}
        ulong GetChecksum(ulong offset, ulong prime);
    }
}