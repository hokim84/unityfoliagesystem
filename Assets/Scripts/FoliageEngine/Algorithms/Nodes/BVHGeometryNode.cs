using UnityEngine;
using System.Collections.Generic;

namespace PWTA
{
    [System.Serializable]
    public class BVHGeometryNode : MeshGeometryNodeBase
    {
        public BVHGeometryNode()
        {
            
        }        

        public NodeBase<MeshGeometryData> Left
        {
            get { return children[0]; } 
        }
        
        public NodeBase<MeshGeometryData> Right
        {
            get { return children[1]; }
        }

    }    
}