using UnityEngine;

namespace PWTA
{
    public interface IPositionProvider
    {
        Vector3 GetCurrentPosition();
        Vector3 GetCurrentForward();
        Camera GetCurrentCamera();
    }
}