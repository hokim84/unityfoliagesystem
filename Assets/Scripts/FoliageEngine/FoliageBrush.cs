using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PWTA
{
    public class FoliageBrush
    {
        public static readonly float MinRadius = 0.1f;
        public static readonly float MaxRadius = 5f;
        public static readonly float MinOverwrapRatio = 0.1f;
        public static readonly float MaxOverwrapRatio = 1f;
        public static readonly float MinEraseDensity = 0.1f;
        public static readonly float MaxEraseDensity = 1f;

        protected int _patchIdx = 0;
        protected MeshGeometryNodeBase _geometryNode;
        protected QuadTreeFoliageNode _foliageNode;
        public float splitSize = 10f;

        protected float _angleLimit = 0.5f;
        public float AngleLimit { get => _angleLimit; set => _angleLimit = value; }
        protected Vector2 _scaleRandomRange = new Vector2(1f, 1f);
        public Vector2  ScaleRandomRange { get => _scaleRandomRange; set => _scaleRandomRange = value; }
        protected Vector2 _rotationRandomRange = new Vector2(0f, 360f);
        public Vector2 RotationRandomRange { get => _rotationRandomRange; set => _rotationRandomRange = value; }

        protected float _radius = 5f;
        public float Radius { get => _radius; set => _radius = value; }

        protected float _overwrapRatio = 0.5f;
        public float OverwrapRatio { get => _overwrapRatio; set => _overwrapRatio = value; }

        protected float _eraseDensity = 0.5f;
        public float EraseDensity { get => _eraseDensity; set => _eraseDensity = value; }

        protected float _mixtureRate = 0.5f;
        public float MixtureRate { get => _mixtureRate; set => _mixtureRate = value; }

        protected FoliageEngine _foliageEngine;
        protected FoliagePaletteAsset _foliagePaletteAsset;

        protected HashSet<FoliageInstance_Editor> _allFoliages = new HashSet<FoliageInstance_Editor>();
        protected HashSet<FoliageInstance_Editor> _cachedFoliages = new HashSet<FoliageInstance_Editor>();

        protected Bounds _worldBounds;

        public bool OnPress = false;

        public FoliageBrush(FoliageManager foliageManager)
        {
            _worldBounds = foliageManager.worldBounds;
            _foliageEngine = foliageManager.FoliageEngine;
            _foliagePaletteAsset = foliageManager.PaletteAsset;

            RefreshFoliages(foliageManager.GeometryNode);
        }

        public void RefreshFoliages(MeshGeometryNodeBase geometryNode)
        {
            _geometryNode = geometryNode;
            _allFoliages.Clear();
            if (null != _foliagePaletteAsset)
            {
                foreach (var foliage in _foliagePaletteAsset.GetAllFoliages())
                {
                    _allFoliages.Add(new FoliageInstance_Editor(foliage.PaletteSlotIdx, -1, foliage.Position, foliage.RotationY, foliage.UniformScale, new Bounds(foliage.Position, Vector3.one * 0.1f)));
                }
            }
            InitFoliageNode();
        }

        private void InitFoliageNode()
        {
            _foliageNode = NodeBuilder.CreateQuadTree(ref _worldBounds, splitSize, _allFoliages);
        }

        private void UpdateFoliageNode()
        {
            CodeTimer.Measure($"UpdateFoliageNode", () =>
            {
                foreach (var instance in CachingData)
                {
                    _foliageNode.Insert(instance, splitSize);
                }
                foreach (var instance in RemoveCandiates)
                {
                    _foliageNode.Remove(instance);
                }
            });
        }

        public HashSet<FoliageInstance_Editor> CachingData = new HashSet<FoliageInstance_Editor>();
        public HashSet<FoliageInstance_Editor> RemoveCandiates = new HashSet<FoliageInstance_Editor>();

        public bool IsMoving = false;
        public Vector3 MovePoint = Vector3.zero;
        public Vector3 MoveNormal = Vector3.zero;

        public bool UpdateMosueMove(Ray ray)
        {
            RaycastHit hit = new RaycastHit();
            if (_geometryNode.TreeRecursionRaycast(ray, ref hit))
            {
                IsMoving = true;
                MovePoint = hit.point;
                MoveNormal = hit.normal;
                return true;
            }
            IsMoving = false;
            MovePoint = Vector3.zero;
            return false;
        }

        public List<RaycastTraceStep> TraceSteps = new List<RaycastTraceStep>();

        public bool Paint(FoliagePaletteSlotData paletteSlot, bool useDebug = false)
        {
            CachingData.Clear();
            RemoveCandiates.Clear();

            var overwrapRadius = paletteSlot.Radius * _overwrapRatio;
            if (overwrapRadius >= _radius)
            {
                if (RaycastPaintPinPoint(MovePoint, paletteSlot, _angleLimit, out TraceSteps))
                {                    
                    Validate();
                    return true;
                }
            }
            else
            {
                var sampleDist = FoliageUtils.ToSampleDistance(_overwrapRatio, paletteSlot.Radius);
                if (RaycastPaintRadius(MovePoint, paletteSlot, _radius, sampleDist, _mixtureRate, _angleLimit, useDebug))
                {
                    Validate();
                    return true;
                }
            }
            return false;
        }

        public bool Erase()
        {
            CachingData.Clear();
            RemoveCandiates.Clear();

            if (RaycastErase(MovePoint, _radius, _eraseDensity, Random.Range(0, 10000)))
            {
                Validate();
                return true;
            }
            return false;
        }

        public void Validate()
        {
            _allFoliages.UnionWith(CachingData);
            _allFoliages.ExceptWith(RemoveCandiates);
            UpdateFoliageNode();
        }

        Vector3 lastMovePoint = Vector3.zero;
        public bool UpdateMouseDragPlant(Ray ray, FoliagePaletteSlotData paletteSlot)
        {
            if ((lastMovePoint - MovePoint).sqrMagnitude > ((_radius * _radius) * 0.5f))//반지름 제곱의 절반.
            {
                if (Paint(paletteSlot))
                {
                    lastMovePoint = MovePoint;
                    return true;
                }
            }
            return false;
        }

        public bool UpdateMouseDragErase(Ray ray)
        {
            if ((lastMovePoint - MovePoint).sqrMagnitude > ((_radius * _radius) * 0.5f))
            {
                if (Erase())
                {
                    lastMovePoint = MovePoint;
                    return true;
                }
            }
            return false;
        }

        public bool Pick(Ray ray)
        {
            CachingData.Clear();

            if (_foliageNode.TreeRecursionRaycast(ray, out FoliageInstance_Editor hitInstance))
            {
                CachingData.Add(hitInstance);
                _allFoliages.Add(hitInstance);
                return true;
            }
            return false;
        }

        private float GetRandomRotation()
        {
            return Random.Range(_rotationRandomRange.x, _rotationRandomRange.y);
        }

        private float GetRandomScale()
        {
            return Random.Range(_scaleRandomRange.x, _scaleRandomRange.y);
        }

        public float RayHeight = 100f;
        public bool RaycastPaintRadius(Vector3 point, FoliagePaletteSlotData paletteSlot, float brushRadius, float sampleDist, float mixtureRate, float angleLimit, bool useDebug = false)
        {
            if (null == paletteSlot)
            {
                return false;
            }

            CodeTimer.Measure("브러시 페인팅", () =>{
            List<Vector3> queryResult = new List<Vector3>();
            _geometryNode.QueryTrianglesInRadius(point, brushRadius, angleLimit, queryResult, useDebug);
            if (queryResult.Count == 0)
            {
                Debug.LogWarning($"RayTest: No triangles found in radius: {brushRadius} at point: {point}");
               
            }

            var sampleRadius = brushRadius - sampleDist;//너무 삐져나가지 않게.            
            sampleRadius = sampleRadius < sampleDist ? brushRadius : sampleRadius;//최소 반지름 보장.

            var overwarpInstances = _foliageNode.GetInstancePositionInDisc(point, sampleRadius);
            var thinoutOverwraps = Thinout(overwarpInstances, mixtureRate, 0, RemoveCandiates);
            var externalPoints = _foliageNode.ToVector2List(thinoutOverwraps, point);

            var samplePoints = FoliageUtils.PoissonDiskSampleCircle(sampleRadius, sampleDist, 8, externalPoints);
            for (int i = 0; i < samplePoints.Count; i++)
            {
                var pointRay = new Ray(point + new Vector3(samplePoints[i].x, RayHeight, samplePoints[i].y), Vector3.down);
                if (NodeUtils.RayTriangleIntersection(pointRay, queryResult, out RaycastHit hit))
                {
                    CachingData.Add(new FoliageInstance_Editor(paletteSlot, _patchIdx, hit.point, GetRandomRotation(), GetRandomScale()));
                }
            }
            Debug.Log($"추가된 식생: {CachingData.Count}");
            });

            //Debug.Log($"RaycastPaint Added: {CachingData.Count}");
            return CachingData.Count > 0;
        }

        public bool RaycastPaintPinPoint(Vector3 point, FoliagePaletteSlotData paletteSlot, float angleLimit, out List<RaycastTraceStep> traceSteps)
        {
            traceSteps = new List<RaycastTraceStep>();
            if (_geometryNode.RayTestNode(point, RayHeight, out Vector3 outPoint, out Vector3 outNormal, out traceSteps))
            {
                var angle = Vector3.Dot(outNormal, Vector3.up);
                if (angle >= angleLimit)
                {
                    CachingData.Add(new FoliageInstance_Editor(paletteSlot, _patchIdx, outPoint, GetRandomRotation(), GetRandomScale()));
                    return true;
                }
            }
            return false;
        }

        public bool RaycastErase(Vector3 point, float radius, float densityRatio, int seed = 0)
        {
            var eraseDensity = FoliageUtils.ToEraseDensity(densityRatio);
            var sqrRadius = radius * radius;
            CodeTimer.Measure($"RayTest Erase", () =>
            {
                TraverseInfo<FoliageInstanceData> traverseInfo = new TraverseInfo<FoliageInstanceData>();
                var instances = _foliageNode.GetInstancesInRadius(point, radius);
                RemoveCandiates = Thinout(instances, eraseDensity, seed, null);
            });
            return RemoveCandiates.Count > 0;
        }

        public static HashSet<FoliageInstance_Editor> Thinout(IEnumerable<FoliageInstance_Editor> instances, float eraseDensity, int seed, HashSet<FoliageInstance_Editor> outFailsList)
        {
            if (eraseDensity == 1f)
            {
                return instances.ToHashSet();
            }
            else if (eraseDensity == 0f)
            {
                outFailsList.UnionWith(instances);
                return new HashSet<FoliageInstance_Editor>();
            }

            HashSet<FoliageInstance_Editor> passesList = new HashSet<FoliageInstance_Editor>();
            foreach (var instance in instances)
            {
                if (FoliageUtils.HashDensityTest(instance.Position, eraseDensity, seed))
                {
                    passesList.Add(instance);
                }
                else
                {
                    if (null != outFailsList)
                        outFailsList.Add(instance);
                }
            }
            return passesList;
        }
    }
}