using UnityEngine;
using System.Collections.Generic;
using System.Linq;


namespace PWTA
{
    public static class NodeBuilder
    {
        public static int SAFE_MAX_DEPTH = 32;
        public static int MIN_PRIMITIVES = 8;

        public static BVHGeometryNode CreateBVH(int targetLayer, float minCount, out Bounds wolrdBounds)
        {
            BuildWorldMeshes(targetLayer, out wolrdBounds, out List<Vector3> vertices, out List<int> triangles, true);
            return CreateBVHInternal(wolrdBounds, vertices, triangles, minCount, 0);
        }

        public static BVHGeometryNode CreateBVH(IEnumerable<MeshFilter> meshFilters, float minCount, out Bounds wolrdBounds)
        {
            BuildWorldMeshes(meshFilters, out wolrdBounds, out List<Vector3> vertices, out List<int> triangles, true);
            return CreateBVHInternal(wolrdBounds, vertices, triangles, minCount, 0);
        }

        public static OctreeGeometryNode CreateOctree(IEnumerable<MeshFilter> meshFilters, float minSize, float minHeight, out Bounds wolrdBounds)
        {
            BuildWorldMeshes(meshFilters, out wolrdBounds, out List<Vector3> vertices, out List<int> triangles, true);
            return CreateOctreeInternal(wolrdBounds, vertices, triangles, minSize, minHeight, 0);
        }

        public static QuadTreeGeometryNode CreateQuadTree(IEnumerable<MeshFilter> meshFilters, float minSize, float minHeight, out Bounds wolrdBounds)
        {
            BuildWorldMeshes(meshFilters, out wolrdBounds, out List<Vector3> vertices, out List<int> triangles, true);
            return CreateQuadTreeInternal(wolrdBounds, vertices, triangles, minSize, minHeight, 0);
        }

        static void DrawBounds(Bounds bounds)
        {
            FoliageUtils.DrawBounds(bounds, Color.red, 1f);
        }

        public static BVHFoliageNode CreateBVH(ref Bounds wolrdBounds, float minSize, IEnumerable<FoliageInstance_Editor> instances)
        {
            return CreateBVHInternal(wolrdBounds, instances.ToList(), minSize, 0);
        }

        public static OctreeGeometryNode CreateOctree(int targetLayer, float minSize)
        {
            Bounds terrainBounds;
            BuildWorldMeshes(targetLayer, out terrainBounds, out List<Vector3> vertices, out List<int> triangles, false);
            return CreateOctreeInternal(terrainBounds, vertices, triangles, minSize, minSize, 0);
        }

        public static QuadTreeFoliageNode CreateQuadTree(ref Bounds wolrdBounds, float minSize, IEnumerable<FoliageInstance_Editor> instances)
        {
            var node = CreateQuadTreeInternal(wolrdBounds, instances.ToList(), minSize, 0);
            node.CacheLeafNodes();
            return node;
        }

        private static BVHGeometryNode CreateBVHInternal(Bounds bounds, List<Vector3> vertices, List<int> triangles, float minCount, int depth)
        {
            BVHGeometryNode node = new BVHGeometryNode { bounds = bounds, depth = depth };
            if (depth <= SAFE_MAX_DEPTH && triangles.Count > minCount)//&& bounds.size.x > minSize && bounds.size.y > minHeight && bounds.size.z > minSize)
            {
                node.IsLeaf = false;

                // 가장 긴 축을 기준으로 분할
                Vector3 size = bounds.size;
                int splitAxis = 0;
                if (size.y > size.x && size.y > size.z) splitAxis = 1;
                else if (size.z > size.x && size.z > size.y) splitAxis = 2;

                // 삼각형들을 분할 축을 기준으로 정렬                
                List<int> sortedTriangles = new List<int>();
                List<Vector3> centers = new List<Vector3>();

                for (int i = 0; i < triangles.Count; i += 3)
                {
                    Vector3 v1 = vertices[triangles[i]];
                    Vector3 v2 = vertices[triangles[i + 1]];
                    Vector3 v3 = vertices[triangles[i + 2]];
                    Vector3 center = (v1 + v2 + v3) / 3f;
                    centers.Add(center);
                }

                // 중심점을 기준으로 정렬
                var sortedIndices = new List<int>();
                for (int i = 0; i < centers.Count; i++) sortedIndices.Add(i);
                sortedIndices.Sort((a, b) => centers[a][splitAxis].CompareTo(centers[b][splitAxis]));

                foreach (int idx in sortedIndices)
                {
                    int baseIdx = idx * 3;
                    sortedTriangles.Add(triangles[baseIdx]);
                    sortedTriangles.Add(triangles[baseIdx + 1]);
                    sortedTriangles.Add(triangles[baseIdx + 2]);
                }

                // 중간 지점에서 분할
                int midPoint = sortedTriangles.Count / 6; // 3개의 인덱스가 하나의 삼각형이므로 6으로 나눔
                midPoint = midPoint * 3; // 3의 배수로 맞춤

                // 왼쪽과 오른쪽 바운드 계산
                Bounds leftBounds = new Bounds();
                Bounds rightBounds = new Bounds();
                bool leftInitialized = false;
                bool rightInitialized = false;

                for (int i = 0; i < sortedTriangles.Count; i += 3)
                {
                    Vector3 v1 = vertices[sortedTriangles[i]];
                    Vector3 v2 = vertices[sortedTriangles[i + 1]];
                    Vector3 v3 = vertices[sortedTriangles[i + 2]];

                    if (i < midPoint)
                    {
                        if (!leftInitialized)
                        {
                            leftBounds = new Bounds(v1, Vector3.zero);
                            leftInitialized = true;
                        }

                        leftBounds.Encapsulate(v1);
                        leftBounds.Encapsulate(v2);
                        leftBounds.Encapsulate(v3);
                    }
                    else
                    {
                        if (!rightInitialized)
                        {
                            rightBounds = new Bounds(v1, Vector3.zero);
                            rightInitialized = true;
                        }

                        rightBounds.Encapsulate(v1);
                        rightBounds.Encapsulate(v2);
                        rightBounds.Encapsulate(v3);
                    }
                }

                var leftTriangles = sortedTriangles.GetRange(0, midPoint);
                var rightTriangles = sortedTriangles.GetRange(midPoint, sortedTriangles.Count - midPoint);
                node.children = new BVHGeometryNode[2];
                node.children[0] = CreateBVHInternal(leftBounds, vertices, leftTriangles, minCount, depth + 1);
                node.children[1] = CreateBVHInternal(rightBounds, vertices, rightTriangles, minCount, depth + 1);
            }
            else
            {
                node.IsLeaf = true;
                for (int i = 0; i < triangles.Count; i += 3)
                {
                    int i1 = triangles[i];
                    int i2 = triangles[i + 1];
                    int i3 = triangles[i + 2];

                    Vector3 v1 = vertices[i1];
                    Vector3 v2 = vertices[i2];
                    Vector3 v3 = vertices[i3];

                    //if (bounds.Contains(v1) || bounds.Contains(v2) || bounds.Contains(v3))
                    {
                        node.geometryData.vertices.AddRange(new[] { v1, v2, v3 });
                    }
                }
            }

            return node;
        }

        private static BVHFoliageNode CreateBVHInternal(Bounds bounds, List<FoliageInstance_Editor> instances, float minSize, int depth)
        {
            var node = new BVHFoliageNode { bounds = bounds };
            if (depth <= SAFE_MAX_DEPTH && instances.Count > MIN_PRIMITIVES && bounds.size.x > minSize && bounds.size.y > minSize && bounds.size.z > minSize)
            {
                node.IsLeaf = false;

                // 가장 긴 축을 기준으로 분할
                Vector3 size = bounds.size;
                int splitAxis = 0;
                if (size.y > size.x && size.y > size.z) splitAxis = 1;
                else if (size.z > size.x && size.z > size.y) splitAxis = 2;

                int midPoint = instances.Count / 2;
                var sortedIndices = new List<int>();
                for (int i = 0; i < instances.Count; i++)
                    sortedIndices.Add(i);

                sortedIndices.Sort((a, b) => instances[a].Position[splitAxis].CompareTo(instances[b].Position[splitAxis]));

                // 왼쪽과 오른쪽 바운드 계산
                Bounds leftBounds = new Bounds();
                Bounds rightBounds = new Bounds();

                bool leftInitialized = false;
                bool rightInitialized = false;

                List<FoliageInstance_Editor> leftInstances = new List<FoliageInstance_Editor>();
                List<FoliageInstance_Editor> rightInstances = new List<FoliageInstance_Editor>();

                for (int i = 0; i < sortedIndices.Count; ++i)
                {
                    var idx = sortedIndices[i];
                    var instance = instances[idx];
                    var position = instance.Position;

                    if (i < midPoint)
                    {
                        if (!leftInitialized)
                        {
                            leftInitialized = true;
                            leftBounds = new Bounds(position, Vector3.zero);
                        }
                        leftBounds.Encapsulate(instance.Bounds);
                        leftInstances.Add(instance);
                    }
                    else
                    {
                        if (!rightInitialized)
                        {
                            rightInitialized = true;
                            rightBounds = new Bounds(position, Vector3.zero);
                        }
                        rightBounds.Encapsulate(instance.Bounds);
                        rightInstances.Add(instance);
                    }
                }

                node.children = new BVHFoliageNode[2];
                node.children[0] = leftInstances.Count > 0 ? CreateBVHInternal(leftBounds, leftInstances, minSize, depth + 1) : null;
                node.children[1] = rightInstances.Count > 0 ? CreateBVHInternal(rightBounds, rightInstances, minSize, depth + 1) : null;
            }
            else
            {
                node.IsLeaf = true;
                for (int i = 0; i < instances.Count; ++i)
                {
                    var instance = instances[i];
                    if (bounds.Contains(instance.Position))
                    {
                        if (node.geometryData.foliageInstances == null)
                            node.geometryData.foliageInstances = new HashSet<FoliageInstance_Editor>();
                        node.geometryData.foliageInstances.Add(instance);
                    }
                }
            }

            return node;
        }

        private static OctreeGeometryNode CreateOctreeInternal(Bounds bounds, List<Vector3> vertices, List<int> triangles, float minSize, float minHeight, int depth)
        {
            OctreeGeometryNode node = new OctreeGeometryNode { bounds = bounds, depth = depth };

            if (depth <= SAFE_MAX_DEPTH &&
                bounds.size.x > minSize || bounds.size.y > minHeight || bounds.size.z > minSize)
            {
                node.IsLeaf = false;
                node.children = new OctreeGeometryNode[8];
                Vector3 center = bounds.center;
                Vector3 size = bounds.size * 0.5f;

                // 8개의 영역으로 분할
                Bounds[] childBounds = new Bounds[8];
                List<int>[] childIndice = new List<int>[8];
                int index = 0;
                for (int x = -1; x <= 1; x += 2)
                {
                    for (int y = -1; y <= 1; y += 2)
                    {
                        for (int z = -1; z <= 1; z += 2)
                        {
                            if (index >= 8) break;
                            Vector3 offset = new Vector3(x, y, z) * 0.5f;
                            childBounds[index] = new Bounds(center + Vector3.Scale(offset, size), size);
                            childIndice[index] = new List<int>();
                            for (int tri = 0; tri < triangles.Count; tri += 3)
                            {
                                int i1 = triangles[tri];
                                int i2 = triangles[tri + 1];
                                int i3 = triangles[tri + 2];

                                Vector3 v1 = vertices[i1];
                                Vector3 v2 = vertices[i2];
                                Vector3 v3 = vertices[i3];

                                if (childBounds[index].Contains(v1) || childBounds[index].Contains(v2) || childBounds[index].Contains(v3))
                                {
                                    childIndice[index].AddRange(new[] { i1, i2, i3 });
                                }
                            }
                            index++;
                        }
                    }
                }

                for (int i = 0; i < 8; i++)
                {
                    node.children[i] = CreateOctreeInternal(childBounds[i], vertices, childIndice[i], minSize, minHeight, depth + 1);
                }
            }
            else
            {
                node.IsLeaf = true;
                for (int i = 0; i < triangles.Count; i += 3)
                {
                    int i1 = triangles[i];
                    int i2 = triangles[i + 1];
                    int i3 = triangles[i + 2];

                    Vector3 v1 = vertices[i1];
                    Vector3 v2 = vertices[i2];
                    Vector3 v3 = vertices[i3];

                    node.geometryData.vertices.AddRange(new[] { v1, v2, v3 });
                }
            }

            return node;
        }

        private static QuadTreeGeometryNode CreateQuadTreeInternal(Bounds bounds, List<Vector3> vertices, List<int> triangles, float minSize, float minHeight, int depth)
        {
            QuadTreeGeometryNode node = new QuadTreeGeometryNode { bounds = bounds };

            // 최소 크기에 도달했거나 삼각형이 없으면 종료
            if (depth <= SAFE_MAX_DEPTH && bounds.size.x > minSize && bounds.size.y > minHeight && bounds.size.z > minSize)
            {
                node.IsLeaf = false;
                node.children = new QuadTreeGeometryNode[4];
                Vector3 center = bounds.center;
                Vector3 size = new Vector3(bounds.size.x * 0.5f, bounds.size.y, bounds.size.z * 0.5f);

                // 4개의 영역으로 분할
                Bounds[] childBounds = new Bounds[4];
                childBounds[0] = new Bounds(new Vector3(center.x - size.x * 0.5f, center.y, center.z - size.z * 0.5f),
                    size);
                childBounds[1] = new Bounds(new Vector3(center.x + size.x * 0.5f, center.y, center.z - size.z * 0.5f),
                    size);
                childBounds[2] = new Bounds(new Vector3(center.x - size.x * 0.5f, center.y, center.z + size.z * 0.5f),
                    size);
                childBounds[3] = new Bounds(new Vector3(center.x + size.x * 0.5f, center.y, center.z + size.z * 0.5f),
                    size);

                for (int i = 0; i < 4; i++)
                {                    
                    var subIndices = new List<int>();
                    for (int j = 0; j < triangles.Count; j += 3)
                    {
                        int i1 = triangles[j];
                        int i2 = triangles[j + 1];
                        int i3 = triangles[j + 2];

                        Vector3 v1 = vertices[i1];
                        Vector3 v2 = vertices[i2];
                        Vector3 v3 = vertices[i3];

                        if (childBounds[i].Contains(v1) || childBounds[i].Contains(v2) || childBounds[i].Contains(v3))
                        {
                            subIndices.AddRange(new[] { i1, i2, i3 });
                        }
                    }
                    node.children[i] = CreateQuadTreeInternal(childBounds[i], vertices, subIndices, minSize, minHeight, depth + 1);
                }
            }
            else
            {
                node.IsLeaf = true;
                for (int i = 0; i < triangles.Count; i += 3)
                {
                    int i1 = triangles[i];
                    int i2 = triangles[i + 1];
                    int i3 = triangles[i + 2];

                    Vector3 v1 = vertices[i1];
                    Vector3 v2 = vertices[i2];
                    Vector3 v3 = vertices[i3];

                    node.geometryData.vertices.AddRange(new[] { v1, v2, v3 });
                }
            }

            return node;
        }

        private static void BuildWorldMeshes(IEnumerable<MeshFilter> meshFilters, out Bounds wolrdBounds, out List<Vector3> vertices, out List<int> triangles, bool drawBounds)
        {
            vertices = new List<Vector3>();
            triangles = new List<int>();

            ProcessWorldMeshes(meshFilters, out wolrdBounds, vertices, triangles);
            if (drawBounds)
                DrawBounds(wolrdBounds);
        }

        private static void BuildWorldMeshes(int targetLayer, out Bounds wolrdBounds, out List<Vector3> vertices, out List<int> triangles, bool drawBounds)
        {
            vertices = new List<Vector3>();
            triangles = new List<int>();

            ProcessWorldMeshes(targetLayer, out wolrdBounds, vertices, triangles);
            if (drawBounds)
                DrawBounds(wolrdBounds);
        }

        private static QuadTreeFoliageNode CreateQuadTreeInternal(Bounds bounds, List<FoliageInstance_Editor> instances, float minSize, int depth)
        {
            var node = new QuadTreeFoliageNode { bounds = bounds, depth = depth };

            if (depth <= SAFE_MAX_DEPTH && bounds.size.x > minSize && bounds.size.z > minSize && instances.Count > 0)
            {
                node.IsLeaf = false;
                node.children = new QuadTreeFoliageNode[4];
                Vector3 center = bounds.center;
                Vector3 size = new Vector3(bounds.size.x * 0.5f, bounds.size.y, bounds.size.z * 0.5f);

                Bounds[] childBounds = new Bounds[4];
                childBounds[0] = new Bounds(new Vector3(center.x - size.x * 0.5f, center.y, center.z - size.z * 0.5f), size);
                childBounds[1] = new Bounds(new Vector3(center.x + size.x * 0.5f, center.y, center.z - size.z * 0.5f), size);
                childBounds[2] = new Bounds(new Vector3(center.x - size.x * 0.5f, center.y, center.z + size.z * 0.5f), size);
                childBounds[3] = new Bounds(new Vector3(center.x + size.x * 0.5f, center.y, center.z + size.z * 0.5f), size);

                for (int i = 0; i < 4; i++)
                {
                    var subList = new List<FoliageInstance_Editor>();
                    foreach (var instance in instances)
                    {
                        if (childBounds[i].Contains(instance.Position))
                            subList.Add(instance);
                    }
                    node.children[i] = CreateQuadTreeInternal(childBounds[i], subList, minSize, depth + 1);
                }
            }
            else
            {
                node.IsLeaf = true;
                node.geometryData.foliageInstances = new HashSet<FoliageInstance_Editor>(instances);
            }

            return node;
        }

        public static void Insert(this QuadTreeFoliageNode node, FoliageInstance_Editor instance, float minSize)
        {
            if (!node.bounds.Contains(instance.Position))
                return;

            InsertInternal(node, instance, minSize);
        }

        private static void InsertInternal(
            QuadTreeFoliageNode node,
            FoliageInstance_Editor instance,
            float minSize)
        {
            if (node.IsLeaf)
            {
                // 아직 leaf에 데이터가 없음
                if (node.geometryData == null)
                {
                    node.geometryData = new FoliageInstanceData()
                    {
                        foliageInstances = new HashSet<FoliageInstance_Editor>()
                    };
                }

                node.geometryData.foliageInstances.Add(instance);

                // split 조건 체크
                bool canSplit =
                    node.bounds.size.x > minSize &&
                    node.bounds.size.z > minSize;

                if (canSplit)
                {
                    Split(node, minSize);
                }

                return;
            }

            // 내부 노드면 자식으로 내려보냄
            int childIndex = GetChildIndex(node, instance.Position);
            InsertInternal((QuadTreeFoliageNode)node.children[childIndex], instance, minSize);
        }

        private static void Split(QuadTreeFoliageNode node, float minSize)
        {
            node.IsLeaf = false;
            node.children = new QuadTreeFoliageNode[4];

            Bounds parentBounds = node.bounds;
            Vector3 center = parentBounds.center;
            Vector3 size = new Vector3(
                parentBounds.size.x * 0.5f,
                parentBounds.size.y,
                parentBounds.size.z * 0.5f);

            Bounds[] childBounds = new Bounds[4];
            childBounds[0] = new Bounds(
                new Vector3(center.x - size.x * 0.5f, center.y, center.z - size.z * 0.5f), size);
            childBounds[1] = new Bounds(
                new Vector3(center.x + size.x * 0.5f, center.y, center.z - size.z * 0.5f), size);
            childBounds[2] = new Bounds(
                new Vector3(center.x - size.x * 0.5f, center.y, center.z + size.z * 0.5f), size);
            childBounds[3] = new Bounds(
                new Vector3(center.x + size.x * 0.5f, center.y, center.z + size.z * 0.5f), size);

            // 자식 노드 생성
            for (int i = 0; i < 4; i++)
            {
                node.children[i] = new QuadTreeFoliageNode
                {
                    bounds = childBounds[i],
                    depth = node.depth + 1,
                    IsLeaf = true,
                    geometryData = new FoliageInstanceData
                    {
                        foliageInstances = new HashSet<FoliageInstance_Editor>()
                    }
                };
            }

            // 기존 인스턴스 재분배
            foreach (var inst in node.geometryData.foliageInstances)
            {
                int idx = GetChildIndex(node, inst.Position);
                node.children[idx].geometryData.foliageInstances.Add(inst);
            }

            // 부모 leaf 데이터 제거
            node.geometryData = null;
        }

        public static bool Remove(this QuadTreeFoliageNode node, FoliageInstance_Editor instance)
        {
            if (!node.bounds.Contains(instance.Position))
                return false;

            return RemoveInternal(node, instance);
        }

        private static bool RemoveInternal(QuadTreeFoliageNode node, FoliageInstance_Editor instance)
        {
            if (node.IsLeaf)
            {
                if (node.geometryData == null)
                    return false;

                return node.geometryData.foliageInstances.Remove(instance);
            }

            int childIndex = GetChildIndex(node, instance.Position);
            bool removed = RemoveInternal((QuadTreeFoliageNode)node.children[childIndex], instance);

            if (!removed)
                return false;

            TryMerge(node);
            return true;
        }

        private static void TryMerge(QuadTreeFoliageNode node)
        {
            if (node.IsLeaf)
                return;

            int totalCount = 0;
            for (int i = 0; i < 4; i++)
            {
                if (!node.children[i].IsLeaf)
                    return; // 자식 중 하나라도 내부 노드면 merge 금지

                totalCount += node.children[i].geometryData?.foliageInstances?.Count ?? 0;
            }

            // merge 실행
            node.IsLeaf = true;
            node.geometryData = new FoliageInstanceData
            {
                foliageInstances = new HashSet<FoliageInstance_Editor>(totalCount)
            };

            for (int i = 0; i < 4; i++)
            {
                var set = node.children[i].geometryData?.foliageInstances;
                if (set != null)
                {
                    foreach (var inst in set)
                        node.geometryData.foliageInstances.Add(inst);
                }
            }

            node.children = null;
        }

        private static int GetChildIndex(QuadTreeFoliageNode node, Vector3 position)
        {
            Vector3 center = node.bounds.center;

            bool right = position.x >= center.x;
            bool top = position.z >= center.z;

            // 0: LB, 1: RB, 2: LT, 3: RT
            if (!right && !top) return 0;
            if (right && !top) return 1;
            if (!right && top) return 2;
            return 3;
        }

        public static void ProcessWorldBounds(int targetLayer, out Bounds wholeBound)
        {
            wholeBound = new Bounds();
            MeshFilter[] meshFilters = Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.InstanceID);

            foreach (MeshFilter meshFilter in meshFilters)
            {
                if (((1 << meshFilter.gameObject.layer) & targetLayer) != 0)
                {
                    Mesh mesh = meshFilter.sharedMesh;
                    if (mesh != null)
                    {
                        wholeBound.Encapsulate(mesh.bounds);
                    }
                }
            }
        }

        public static void ProcessWorldMeshes(int targetLayer, out Bounds wholeBound, List<Vector3> allVertices, List<int> allTriangles)
        {
            Debug.Log($"Process World Meshes, targetLayer: [{LayerMask.LayerToName(targetLayer)}]");

            wholeBound = new Bounds();
            MeshFilter[] meshFilters = Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.InstanceID);
            var targetMeshFilters = meshFilters.Where(mf => ((1 << mf.gameObject.layer) & targetLayer) != 0);
            ProcessWorldMeshes(targetMeshFilters, out wholeBound, allVertices, allTriangles);
        }

        public static void ProcessWorldMeshes(IEnumerable<MeshFilter> meshFilters, out Bounds wholeBound, List<Vector3> allVertices, List<int> allTriangles)
        {
            wholeBound = new Bounds();
            foreach (var meshFilter in meshFilters)
            {
                if (null == meshFilter)
                {
                    Debug.LogWarning($"MeshFilter is null: {meshFilter.gameObject.name}");
                    continue;
                }
                Mesh mesh = meshFilter.sharedMesh;
                if (null == mesh)
                {
                    Debug.LogWarning($"Mesh is null: {mesh.name}");
                    continue;
                }

                //Debug.Log($"Process World Meshes... {mesh.name}");
                int vertexOffset = allVertices.Count;
                Vector3[] localVertices = mesh.vertices;

                for (int i = 0; i < localVertices.Length; i++)
                {
                    var worldVertex = meshFilter.transform.TransformPoint(localVertices[i]);
                    allVertices.Add(worldVertex);
                    wholeBound.Encapsulate(worldVertex);
                }

                int[] triangles = mesh.triangles;
                for (int i = 0; i < triangles.Length; i++)
                {
                    allTriangles.Add(triangles[i] + vertexOffset);
                }
            }
        }
    }
}