using System.Collections.Generic;
using UnityEngine;

namespace PWTA
{
    public interface IFoliageRenderGroup
    {
        void RefreshBuffer();

        void DrawIndirect(Vector3 cameraPosition, Vector3 cameraForward, Bounds bounds);

        void Dispose();
    }
}   