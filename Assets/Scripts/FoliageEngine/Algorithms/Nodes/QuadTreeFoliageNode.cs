using System.Collections.Generic;
using UnityEngine;

namespace PWTA
{
    public class QuadTreeFoliageNode : NodeBase<FoliageInstanceData>
    {
        public QuadTreeFoliageNode() : base()
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

        public List<FoliageInstance_Editor> GetInstancePositionInDisc(Vector3 center, float radius)
        {
            var resultList = new List<FoliageInstance_Editor>();
            var sqrRadius = radius * radius;

            TraverseInfo<FoliageInstanceData> traverseInfo = new TraverseInfo<FoliageInstanceData>();
            TraverseNodeInRadius(center, radius, ref traverseInfo);

            if (null == traverseInfo.hitNodes)
                return resultList;

            foreach (var node in traverseInfo.hitNodes)
            {
                if (null == node.geometryData.foliageInstances)
                    continue;

                foreach (var instance in node.geometryData.foliageInstances)
                {
                    var sqrDistance = (center - instance.Position).sqrMagnitude;
                    if (sqrDistance < sqrRadius)
                    {                        
                        resultList.Add(instance);
                    }
                }
            }
            return resultList;
        }

        public List<Vector2> ToVector2List(HashSet<FoliageInstance_Editor> instances, Vector3 center)
        {
            var result = new List<Vector2>();
            foreach (var instance in instances)
            {
                result.Add(new Vector2(instance.Position.x - center.x, instance.Position.z - center.z));
            }
            return result;
        }

        public void AddInstances(IEnumerable<FoliageInstance_Editor> instances)
        {
            Dictionary<Vector2Int, List<FoliageInstance_Editor>> instanceMap = new Dictionary<Vector2Int, List<FoliageInstance_Editor>>();
            foreach (var instance in instances)
            {
                var index = GetLeafIndex(instance.Position);
                if (instanceMap.ContainsKey(index))
                    instanceMap[index].Add(instance);
                else
                    instanceMap[index] = new List<FoliageInstance_Editor> { instance };
            }

            foreach (var item in instanceMap)
            {
                var leafNode = GetLeafNode(item.Key);
                if (null != leafNode)
                    leafNode.geometryData.foliageInstances.UnionWith(item.Value);
            }
        }

        public void RemoveInstances(List<FoliageInstance_Editor> instances)
        {
            Dictionary<Vector2Int, List<FoliageInstance_Editor>> instanceMap = new Dictionary<Vector2Int, List<FoliageInstance_Editor>>();
            foreach (var instance in instances)
            {
                var index = GetLeafIndex(instance.Position);
                if (instanceMap.ContainsKey(index))
                    instanceMap[index].Add(instance);
                else
                    instanceMap[index] = new List<FoliageInstance_Editor> { instance };
            }

            foreach (var item in instanceMap)
            {
                var leafNode = GetLeafNode(item.Key);
                leafNode.geometryData.foliageInstances.ExceptWith(item.Value);
            }
        }
    }
}