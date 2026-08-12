using UnityEngine;
using System.Collections.Generic;

namespace PWTA
{
    public class TraverseInfo<T>
    {
        public TraverseInfo()
        {            
            bbPassCount = 0;
            traverseCount = 0;            
        }
        
        public int bbPassCount;
        public int traverseCount;    
        public List<NodeBase<T>> hitNodes;
        public List<float> hitDistances;
    }
}