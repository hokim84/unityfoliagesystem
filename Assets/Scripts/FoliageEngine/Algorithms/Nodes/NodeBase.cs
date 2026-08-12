using System.Collections.Generic;
using UnityEngine;

namespace PWTA
{
    public class MeshGeometryData
    {
        public MeshFilter mesh;
        public List<Vector3> vertices = new List<Vector3>();
    }

    public class FoliageInstanceData
    {
        public Bounds bounds;
        public HashSet<FoliageInstance_Editor> foliageInstances;
    }

    public abstract class NodeBase<T>
    {
        public int nodeID = 0;
        public int depth = 0;
        public Bounds bounds;
        public T geometryData;
        public bool IsLeaf = false;
        public NodeBase<T>[] children;
        public Dictionary<Vector2Int, NodeBase<T>> leafNodes;
        public int maxDepth = 0;

        public void CacheLeafNodes()
        {
            CacheLeafNodes(this, leafNodes, ref maxDepth);
        }

        public static void CacheLeafNodes(NodeBase<T> node, Dictionary<Vector2Int, NodeBase<T>> nodeMap, ref int maxDepth)
        {
            if (node.IsLeaf)
            {
                var index = node.GetLeafIndex(node.bounds.center);
                if (null == nodeMap)
                    nodeMap = new Dictionary<Vector2Int, NodeBase<T>>();
                nodeMap[index] = node;
            }
            else
            {
                ++maxDepth;
                foreach (var child in node.children)
                    CacheLeafNodes(child, nodeMap, ref maxDepth);
            }
        }

        public Vector2Int GetLeafIndex(Vector3 position)
        {
            var locPos = position - bounds.center;
            float cellSizeX = bounds.size.x / (1 << maxDepth);
            float cellSizeZ = bounds.size.z / (1 << maxDepth);
            int x = Mathf.FloorToInt(locPos.x / cellSizeX);
            int z = Mathf.FloorToInt(locPos.z / cellSizeZ);
            return new Vector2Int(x, z);
        }

        public virtual NodeBase<T> GetLeafNode(Vector2Int index)
        {
            if (leafNodes == null || !leafNodes.ContainsKey(index))
                return null;

            return leafNodes[index];
        }

        public bool TraverseNode(Ray ray, ref TraverseInfo<T> traverseInfo, bool debugBB = false)
        {
            return TraverseNode(ray, ref traverseInfo, null, debugBB);
        }

        public bool TraverseNode(Ray ray, ref TraverseInfo<T> traverseInfo, List<RaycastTraceStep> traceSteps, bool debugBB = false)
        {
            ++traverseInfo.traverseCount;
            if (!bounds.IntersectRay(ray, out float distance))
                return false;

            ++traverseInfo.bbPassCount;
            if (traceSteps != null)
                traceSteps.Add(RaycastTraceStep.FromBounds(bounds, true));

            if (debugBB)
                NodeUtils.DrawDebugBB(this);

            if (IsLeaf)
            {
                if (traverseInfo.hitNodes == null)
                    traverseInfo.hitNodes = new List<NodeBase<T>>();
                traverseInfo.hitNodes.Add(this);

                if (traverseInfo.hitDistances == null)
                    traverseInfo.hitDistances = new List<float>();
                traverseInfo.hitDistances.Add(distance);
            }
            else if (children != null)
            {
                foreach (var child in children)
                {
                    if (null != child)
                        child.TraverseNode(ray, ref traverseInfo, traceSteps, debugBB);
                }
            }

            return null != traverseInfo.hitNodes;
        }

        public bool TraverseNodeInRadius(Vector3 center, float radius, ref TraverseInfo<T> traverseInfo, bool debugBB = false)
        {
            ++traverseInfo.traverseCount;
            if (!TestBBSphere(center, radius, out float sqrDist))
                return false;
            ++traverseInfo.bbPassCount;

            if (debugBB)
                NodeUtils.DrawDebugBB(this);

            if (IsLeaf)
            {
                if (traverseInfo.hitNodes == null)
                    traverseInfo.hitNodes = new List<NodeBase<T>>();
                traverseInfo.hitNodes.Add(this);                

                if (traverseInfo.hitDistances == null)
                    traverseInfo.hitDistances = new List<float>();
                traverseInfo.hitDistances.Add(sqrDist);
            }
            else if (children != null)
            {
                foreach (var child in children)
                {
                    if (null != child)
                        child.TraverseNodeInRadius(center, radius, ref traverseInfo, debugBB);
                }
            }

            return null != traverseInfo.hitNodes;
        }

        public bool TestBBSphere(Vector3 sphereCenter, float radius, out float sqrDist)
        {
            sqrDist = 0f;

            if (sphereCenter.x < bounds.min.x)
                sqrDist += (bounds.min.x - sphereCenter.x) * (bounds.min.x - sphereCenter.x);
            else if (sphereCenter.x > bounds.max.x)
                sqrDist += (sphereCenter.x - bounds.max.x) * (sphereCenter.x - bounds.max.x);

            if (sphereCenter.y < bounds.min.y)
                sqrDist += (bounds.min.y - sphereCenter.y) * (bounds.min.y - sphereCenter.y);
            else if (sphereCenter.y > bounds.max.y)
                sqrDist += (sphereCenter.y - bounds.max.y) * (sphereCenter.y - bounds.max.y);

            if (sphereCenter.z < bounds.min.z)
                sqrDist += (bounds.min.z - sphereCenter.z) * (bounds.min.z - sphereCenter.z);
            else if (sphereCenter.z > bounds.max.z)
                sqrDist += (sphereCenter.z - bounds.max.z) * (sphereCenter.z - bounds.max.z);

            return sqrDist <= radius * radius;
        }
    }

    public class MeshGeometryNodeBase : NodeBase<MeshGeometryData>
    {
        public MeshGeometryNodeBase()
        {
            geometryData = new MeshGeometryData();
        }

        public bool TreeRecursionRaycast(Ray ray, ref RaycastHit retHit, out List<RaycastTraceStep> traceSteps, bool debugBB = false)
        {
            bool hasHit = false;
            traceSteps = new List<RaycastTraceStep>();

            TraverseInfo<MeshGeometryData> traverseInfo = new TraverseInfo<MeshGeometryData>();
            float closetsDistance = float.MaxValue;
            int triTestCount = 0;
            if (TraverseNode(ray, ref traverseInfo, traceSteps))
            {
                var hitNodes = traverseInfo.hitNodes;
                var hitDistances = traverseInfo.hitDistances;
                if (hitNodes == null || hitNodes.Count == 0)
                    return false;

                List<int> nodeOrder = null;
                bool hasSortedOrder = false;
                if (hitDistances != null && hitDistances.Count == hitNodes.Count && hitNodes.Count > 1)
                {
                    nodeOrder = new List<int>(hitNodes.Count);
                    for (int i = 0; i < hitNodes.Count; i++)
                        nodeOrder.Add(i);
                    nodeOrder.Sort((a, b) => hitDistances[a].CompareTo(hitDistances[b]));
                    hasSortedOrder = true;
                }

                int nodeCount = hitNodes.Count;
                for (int orderIdx = 0; orderIdx < nodeCount; orderIdx++)
                {
                    int nodeIndex = nodeOrder != null ? nodeOrder[orderIdx] : orderIdx;
                    var node = hitNodes[nodeIndex];
                    if (hasSortedOrder && closetsDistance < float.MaxValue)
                    {
                        if (hitDistances[nodeIndex] > closetsDistance)
                            break; // farther than current closest (sorted)
                    }

                    for (int i = 0; i < node.geometryData.vertices.Count; i += 3)
                    {
                        Vector3 v1 = node.geometryData.vertices[i];
                        Vector3 v2 = node.geometryData.vertices[i + 1];
                        Vector3 v3 = node.geometryData.vertices[i + 2];

                        var hit = new RaycastHit();
                        bool isHit = NodeUtils.RayTriangleIntersection(ray, v1, v2, v3, out hit);
                        traceSteps.Add(RaycastTraceStep.FromTriangle(v1, v2, v3, isHit, hit));

                        if (isHit)
                        {
                            if (hit.distance < closetsDistance)
                            {
                                retHit = hit;
                                closetsDistance = hit.distance;
                                hasHit = true;
                            }
                        }
                        ++triTestCount;
                    }
                }
            }

            if (debugBB)
                Debug.LogWarning($"[Traverse]:{traverseInfo.traverseCount} [BB]:{traverseInfo.bbPassCount} [Ray]:{triTestCount}");

            return hasHit;
        }
    }
}