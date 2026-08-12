using System.Collections.Generic;
using UnityEngine;

namespace PWTA
{
    [System.Serializable]
    public class BVHFoliageNode : NodeBase<FoliageInstanceData>
    {
        public BVHFoliageNode() : base()
        {
            geometryData = new FoliageInstanceData();
        }

        public bool TreeRecursionRaycast(Ray ray, out FoliageInstance_Editor hitInstance)
        {
            var traverseInfo = new TraverseInfo<FoliageInstanceData>();
            if (TraverseNode(ray, ref traverseInfo))
            {
                var nearestDistance = float.MaxValue;
                FoliageInstance_Editor nearestInstance = null;

                foreach (var node in traverseInfo.hitNodes)
                {
                    if (node.geometryData.foliageInstances == null)
                        continue;

                    foreach (var instance in node.geometryData.foliageInstances)
                    {
                        if (instance.Bounds.IntersectRay(ray, out float distance))
                        {
                            if (distance < nearestDistance)
                            {
                                nearestInstance = instance;
                            }
                        }
                    }
                }
                if (nearestInstance != null)
                {
                    hitInstance = nearestInstance;
                    return true;
                }
            }
            hitInstance = null;
            return false;
        }

        public HashSet<FoliageInstance_Editor> GetInstancesInRadius(Vector3 center, float radius)
        {
            var result = new HashSet<FoliageInstance_Editor>();
            var sqrRadius = radius * radius;

            TraverseInfo<FoliageInstanceData> traverseInfo = new TraverseInfo<FoliageInstanceData>();
            TraverseNodeInRadius(center, radius, ref traverseInfo);
            foreach (var node in traverseInfo.hitNodes)
            {
                foreach (var instance in node.geometryData.foliageInstances)
                {
                    if ((center - instance.Position).sqrMagnitude < sqrRadius)
                    {
                        result.Add(instance);
                    }
                }
            }
            return result;
        }

        public List<Vector2> GetInstancePositionInDisc(Vector3 center, float radius, bool useLocalPosition = false)
        {
            var result = new List<Vector2>();
            var sqrRadius = radius * radius;

            TraverseInfo<FoliageInstanceData> traverseInfo = new TraverseInfo<FoliageInstanceData>();
            TraverseNodeInRadius(center, radius, ref traverseInfo);
            foreach (var node in traverseInfo.hitNodes)
            {
                if(node.geometryData.foliageInstances == null)
                    continue;

                foreach (var instance in node.geometryData.foliageInstances)
                {
                    if ((center - instance.Position).sqrMagnitude < sqrRadius)
                    {
                        result.Add(useLocalPosition ? new Vector2(instance.Position.x - center.x, instance.Position.z - center.z) : new Vector2(instance.Position.x, instance.Position.z));
                    }
                }
            }
            return result;
        }
    }
}