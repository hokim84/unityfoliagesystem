using UnityEngine;

namespace PWTA
{
    public class FoliageSampleData
    {        
        public FoliageSampleData(int patchIdx, Vector3 position)
        {
            this.patchIdx = patchIdx;
            this.Position = position;
        }
        public int patchIdx;
        public Vector3 Position { get; set; }
    }
}