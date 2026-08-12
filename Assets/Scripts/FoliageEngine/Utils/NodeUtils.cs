using UnityEngine;
using System.Collections.Generic;

namespace PWTA
{
    public static class NodeUtils
    {
        public static float DebugDrawTime = 1f;

        public static bool RayTestNode(this MeshGeometryNodeBase geometryNode, Vector3 inPoint, float height, out Vector3 outPoint, out Vector3 outNormal, out List<RaycastTraceStep> traceSteps)
        {
            var pointRay = new Ray(inPoint + new Vector3(0, height, 0), Vector3.down);
            RaycastHit hit = new RaycastHit();

            traceSteps = new List<RaycastTraceStep>();
            traceSteps.Add(RaycastTraceStep.FromRay(pointRay, false));
            if (geometryNode.TreeRecursionRaycast(pointRay, ref hit, out traceSteps))
            {                
                outPoint = hit.point;
                outNormal = hit.normal;
                return true;
            }
            outPoint = Vector3.zero;
            outNormal = Vector3.up;
            return false;
        }

        public static bool TreeRecursionRaycast(this NodeBase<MeshGeometryData> root, Ray ray, ref RaycastHit retHit, bool debugBB = false)
        {
            return TreeRecursionRaycast(root, ray, ref retHit, out _, debugBB);
        }

        public static bool TreeRecursionRaycast(this NodeBase<MeshGeometryData> root, Ray ray, ref RaycastHit retHit, out List<RaycastTraceStep> traceSteps, bool debugBB = false)
        {
            bool hasHit = false;
            traceSteps = new List<RaycastTraceStep>();
            traceSteps.Add(RaycastTraceStep.FromRay(ray, false));
            if (root != null)
            {
                TraverseInfo<MeshGeometryData> traverseInfo = new TraverseInfo<MeshGeometryData>();
                float closetsDistance = float.MaxValue;
                int triTestCount = 0;
                if (root.TraverseNode(ray, ref traverseInfo, traceSteps, debugBB))
                {
                    foreach (var node in traverseInfo.hitNodes)
                    {
                        for (int i = 0; i < node.geometryData.vertices.Count; i += 3)
                        {
                            Vector3 v1 = node.geometryData.vertices[i];
                            Vector3 v2 = node.geometryData.vertices[i + 1];
                            Vector3 v3 = node.geometryData.vertices[i + 2];

                            var hit = new RaycastHit();
                            bool isHit = RayTriangleIntersection(ray, v1, v2, v3, out hit);
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
            }

            return hasHit;
        }

        public static bool QueryTrianglesInRadius(this NodeBase<MeshGeometryData> rootNode, Vector3 center, float radius, float angleLimit, List<Vector3> output, bool debugBB = false)
        {
            TraverseInfo<MeshGeometryData> traverseInfo = new TraverseInfo<MeshGeometryData>();
            if (rootNode.TraverseNodeInRadius(center, radius, ref traverseInfo, debugBB))
            {
                if (traverseInfo.hitNodes.Count == 0)
                    return false;

                foreach (var node in traverseInfo.hitNodes)
                {
                    for (int i = 0; i < node.geometryData.vertices.Count; i += 3)
                    {
                        var v1 = node.geometryData.vertices[i];
                        var v2 = node.geometryData.vertices[i + 1];
                        var v3 = node.geometryData.vertices[i + 2];

                        //삼각형의 외접원과 그 지름으로 fail case 처리.
                        // var AB = v2 - v1;
                        // var BC = v3 - v2;
                        // var CA = v1 - v3;
                        // var a = AB.magnitude;
                        // var b = BC.magnitude;
                        // var c = CA.magnitude;
                        // float area2 = Vector3.Cross(AB, BC).magnitude;
                        // if (area2 < float.Epsilon)
                        //     continue;
                        // var triRadius = (a * b * c) / area2;
                        // if(triRadius < radius)
                        // 반지름 계산 무의미미

                        if (SegmentIntersectsSphere(v1, v2, center, radius)
                            || SegmentIntersectsSphere(v2, v3, center, radius)
                            || SegmentIntersectsSphere(v3, v1, center, radius))
                        {
                            if (GetNormalAngle(v1, v2, v3) >= angleLimit)
                            {
                                output.Add(v1);
                                output.Add(v2);
                                output.Add(v3);
                            }
                        }
                    }
                }

                //radius 영역에 삼각형 교차가 없는 경우 대체 알고리즘 실행. 
                if (output.Count == 0)
                {
                    return TrianglesInRadius_Alternate(traverseInfo.hitNodes, center, radius, angleLimit, output, debugBB);
                }
            }
            return false;
        }

        private static bool TrianglesInRadius_Alternate(List<NodeBase<MeshGeometryData>> candidateNodes, Vector3 center, float radius, float angleLimit, List<Vector3> output, bool debugBB = false)
        {
            foreach (var node in candidateNodes)
            {
                for (int i = 0; i < node.geometryData.vertices.Count; i += 3)
                {
                    var v1 = node.geometryData.vertices[i];
                    var v2 = node.geometryData.vertices[i + 1];
                    var v3 = node.geometryData.vertices[i + 2];
                    if (RayTriangleIntersection(new Ray(center + Vector3.up, Vector3.down * 2f), v1, v2, v3, out RaycastHit hit))
                    {
                        output.Add(v1);
                        output.Add(v2);
                        output.Add(v3);
                        return true;
                    }
                }
            }
            return false;
        }

        public static void RaycastRadius_Debug(this NodeBase<MeshGeometryData> rootNode, Ray ray, float radius, float angleLimit, bool debugBB = false)
        {
            if (debugBB)
                DrawIntersectedBounds(rootNode, ray);

            //Debug.DrawRay(ray.origin, ray.direction * 1000f, Color.magenta, DebugDrawTime);
            var hit = new RaycastHit();
            if (TreeRecursionRaycast(rootNode, ray, ref hit))
            {
                var nodeType = rootNode.GetType().Name;
                var raycastHit = hit;
                CodeTimer.Measure($"{nodeType} Hit", () =>
                {
                    List<Vector3> outList = new List<Vector3>();
                    QueryTrianglesInRadius(rootNode, raycastHit.point, radius, angleLimit, outList);
                    for (int j = 0; j < 1000; j++)
                    {
                        RayTriangleIntersection(ray, outList, out RaycastHit hit);
                    }
                });
            }
        }

        public static float GetNormalAngle(Vector3 v1, Vector3 v2, Vector3 v3)
        {
            Vector3 e1 = v2 - v1;
            Vector3 e2 = v3 - v1;
            return Vector3.Dot(Vector3.Cross(e1, e2).normalized, Vector3.up);
        }

        public static bool RayTriangleIntersection(Ray ray, Vector3 v1, Vector3 v2, Vector3 v3, out RaycastHit hit,
            bool useDebug = false)
        {
            if (useDebug)
            {
                Debug.DrawLine(v1, v2, Color.blue, DebugDrawTime); // 첫 번째 변
                Debug.DrawLine(v2, v3, Color.green, DebugDrawTime); // 두 번째 변
                Debug.DrawLine(v3, v1, Color.red, DebugDrawTime); // 세 번째 변
            }

            hit = new RaycastHit();

            Vector3 e1 = v2 - v1;
            Vector3 e2 = v3 - v1;
            Vector3 h = Vector3.Cross(ray.direction, e2);
            float a = Vector3.Dot(e1, h);

            if (Mathf.Abs(a) < 0.0001f)
                return false;

            float f = 1.0f / a;
            Vector3 s = ray.origin - v1;
            float u = f * Vector3.Dot(s, h);

            if (u < 0.0f || u > 1.0f)
                return false;

            Vector3 q = Vector3.Cross(s, e1);
            float v = f * Vector3.Dot(ray.direction, q);

            if (v < 0.0f || u + v > 1.0f)
                return false;

            float distance = f * Vector3.Dot(e2, q);
            if (distance <= 0.0001f)
                return false;

            hit.point = ray.GetPoint(distance);
            hit.normal = Vector3.Cross(e1, e2).normalized;
            hit.distance = distance;

            return true;
        }

        public static bool RayTriangleIntersection(Ray ray, List<Vector3> vertices, out RaycastHit raycastHit,
            bool useDebug = false)
        {
            raycastHit = new RaycastHit();
            float closestDistance = float.MaxValue;
            bool hasHit = false;

            for (int i = 0; i < vertices.Count; i += 3)
            {
                if (i + 2 >= vertices.Count) break;

                Vector3 v1 = vertices[i];
                Vector3 v2 = vertices[i + 1];
                Vector3 v3 = vertices[i + 2];

                if (RayTriangleIntersection(ray, v1, v2, v3, out RaycastHit rayIntersect, useDebug))
                {
                    if (rayIntersect.distance < closestDistance)
                    {
                        raycastHit = rayIntersect;
                        closestDistance = rayIntersect.distance;
                        hasHit = true;
                    }
                }
            }

            return hasHit;
        }

        private static bool RayTriangleIntersection(Ray ray, Vector3 v1, Vector3 v2, Vector3 v3, out float distance)
        {
            distance = float.MaxValue;
            Vector3 e1 = v2 - v1;
            Vector3 e2 = v3 - v1;
            Vector3 h = Vector3.Cross(ray.direction, e2);
            float a = Vector3.Dot(e1, h);

            if (Mathf.Abs(a) < 0.0001f)
                return false;

            float f = 1.0f / a;
            Vector3 s = ray.origin - v1;
            float u = f * Vector3.Dot(s, h);

            if (u < 0.0f || u > 1.0f)
                return false;

            Vector3 q = Vector3.Cross(s, e1);
            float v = f * Vector3.Dot(ray.direction, q);

            if (v < 0.0f || u + v > 1.0f)
                return false;

            distance = f * Vector3.Dot(e2, q);
            return distance > 0.0001f;
        }

        public static bool SegmentIntersectsSphere(Vector3 v1, Vector3 v2, Vector3 center, float radius)
        {
            Vector3 d = v2 - v1;
            Vector3 m = center - v1;

            float t = Vector3.Dot(m, d) / d.sqrMagnitude;
            t = Mathf.Clamp01(t);

            Vector3 closest = v1 + t * d;
            float distSq = (closest - center).sqrMagnitude;

            return distSq <= radius * radius;
        }

        public static void DrawNodeVertices(NodeBase<MeshGeometryData> rootNode)
        {
            var leafNodes = GetLeafNodes(rootNode);
            foreach (var leaf in leafNodes)
            {
                var vertices = leaf.geometryData.vertices;
                for (int i = 0; i < vertices.Count; i += 3)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawLine(vertices[i], vertices[i + 1]);
                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(vertices[i + 1], vertices[i + 2]);
                    Gizmos.color = Color.blue;
                    Gizmos.DrawLine(vertices[i + 2], vertices[i]);
                }
            }
        }

        public static IEnumerable<NodeBase<T>> GetLeafNodes<T>(NodeBase<T> rootNode)
        {
            foreach (var node in rootNode.children)
            {
                if (node.IsLeaf)
                    yield return node;
                else
                    foreach (var leafNode in GetLeafNodes(node))
                        yield return leafNode;
            }
        }

        public static void DrawIntersectedBounds<T>(NodeBase<T> node, Ray ray)
        {
            if (node == null) return;

            if (node.bounds.IntersectRay(ray))
            {
                DrawDebugBB(node);

                if (!node.IsLeaf && node.children != null)
                {
                    foreach (var child in node.children)
                    {
                        DrawIntersectedBounds(child, ray);
                    }
                }
            }
        }

        public static void DrawDebugBB<T>(NodeBase<T> node)
        {
            if (node == null) return;

            var lineColor = Color.Lerp(Color.blue, Color.green, node.depth * 0.1f);
            Vector3 min = node.bounds.min;
            Vector3 max = node.bounds.max;
            Vector3 size = node.bounds.size;

            // 바운드의 12개 엣지를 그립니다
            Debug.DrawLine(min, min + new Vector3(size.x, 0, 0), lineColor, DebugDrawTime);
            Debug.DrawLine(min, min + new Vector3(0, size.y, 0), lineColor, DebugDrawTime);
            Debug.DrawLine(min, min + new Vector3(0, 0, size.z), lineColor, DebugDrawTime);

            Debug.DrawLine(max, max - new Vector3(size.x, 0, 0), lineColor, DebugDrawTime);
            Debug.DrawLine(max, max - new Vector3(0, size.y, 0), lineColor, DebugDrawTime);
            Debug.DrawLine(max, max - new Vector3(0, 0, size.z), lineColor, DebugDrawTime);

            Debug.DrawLine(min + new Vector3(size.x, 0, 0), min + new Vector3(size.x, size.y, 0), lineColor,
                DebugDrawTime);
            Debug.DrawLine(min + new Vector3(size.x, 0, 0), min + new Vector3(size.x, 0, size.z), lineColor,
                DebugDrawTime);
            Debug.DrawLine(min + new Vector3(0, size.y, 0), min + new Vector3(size.x, size.y, 0), lineColor,
                DebugDrawTime);
            Debug.DrawLine(min + new Vector3(0, size.y, 0), min + new Vector3(0, size.y, size.z), lineColor,
                DebugDrawTime);
            Debug.DrawLine(min + new Vector3(0, 0, size.z), min + new Vector3(size.x, 0, size.z), lineColor,
                DebugDrawTime);
            Debug.DrawLine(min + new Vector3(0, 0, size.z), min + new Vector3(0, size.y, size.z), lineColor,
                DebugDrawTime);

        }
    }
}