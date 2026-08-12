using UnityEngine;

namespace PWTA
{
    public enum RaycastTraceShape
    {
        Ray,
        Bounds,
        Triangle
    }

    [System.Serializable]
    public sealed class RaycastTraceStep
    {
        public RaycastTraceShape Shape;
        public Bounds Bounds;
        public Vector3[] Polygon;
        public Vector3 V1;
        public Vector3 V2;
        public Vector3 V3;
        public Ray Ray;
        public bool Hit;
        public float Distance;
        public Vector3 HitPoint;
        public Vector3 Normal;

        public static RaycastTraceStep FromRay(Ray ray, bool hit)
        {
            return new RaycastTraceStep
            {
                Shape = RaycastTraceShape.Ray,
                Ray = ray,
                Hit = hit
            };
        }

        public static RaycastTraceStep FromTriangle(Vector3 v1, Vector3 v2, Vector3 v3, bool hit, RaycastHit hitInfo)
        {
            return new RaycastTraceStep
            {
                Shape = RaycastTraceShape.Triangle,
                Polygon = new[] { v1, v2, v3 },
                V1 = v1,
                V2 = v2,
                V3 = v3,
                Hit = hit,
                Distance = hit ? hitInfo.distance : 0f,
                HitPoint = hit ? hitInfo.point : Vector3.zero,
                Normal = hit ? hitInfo.normal : Vector3.up
            };
        }

        public static RaycastTraceStep FromBounds(Bounds bounds, bool hit)
        {
            return new RaycastTraceStep
            {
                Shape = RaycastTraceShape.Bounds,
                Bounds = bounds,
                Hit = hit
            };
        }

        public bool TryGetTriangle(out Vector3 v1, out Vector3 v2, out Vector3 v3)
        {
            v1 = V1;
            v2 = V2;
            v3 = V3;
            return Shape == RaycastTraceShape.Triangle;
        }

        public bool TryGetPolygon(out Vector3[] polygon)
        {
            polygon = Polygon;
            return Shape == RaycastTraceShape.Triangle && polygon != null && polygon.Length >= 3;
        }
    }
}
