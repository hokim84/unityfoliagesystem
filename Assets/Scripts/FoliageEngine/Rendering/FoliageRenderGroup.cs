using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace PWTA
{
    public class FoliageRenderGroup : IFoliageRenderGroup
    {
        public uint visibleCount = 0;
        public int allCount = 0;
        public int paletteSlotIdx = 0;
        public FoliagePaletteAsset paletteAsset;
        public Mesh mesh;
        public Material[] materials;

        public class RenderUnit : IDisposable
        {
            private Mesh _mesh;
            private Material[] _materials;
            private ComputeBuffer[] _argsBuffer;
            private uint[][] _indices;
            public uint visibleCount = 0;

            public void Initialize(Mesh mesh, Material[] materials, ComputeBuffer visibleMatrixBuffer)
            {
                if (null == mesh || null == materials)
                {
                    Debug.LogError("FoliageRenderGroup.RenderUnit.Initialize: mesh or materials is null");
                    return;
                }

                this._mesh = mesh;
                this._materials = materials;

                for (int i = 0; i < _materials.Length; i++)
                {
                    _materials[i].SetBuffer("_InstanceTransforms", visibleMatrixBuffer);
                    _materials[i].EnableKeyword("INDIRECT_INSTANCING_ON");
                }

                int subMeshCount = mesh.subMeshCount;
                _argsBuffer = new ComputeBuffer[subMeshCount];
                _indices = new uint[subMeshCount][];
                for (int i = 0; i < subMeshCount; ++i)
                {
                    _argsBuffer[i] = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
                    _indices[i] = new uint[5]
                    {
                         mesh.GetIndexCount(i),
                        0, // ComputeShader에서 채움
                        mesh.GetIndexStart(i),
                        mesh.GetBaseVertex(i),
                        0
                    };
                    _argsBuffer[i].SetData(_indices[i]);
                }
            }

            public void CopyCount(ComputeBuffer visibleMatrixBuffer)
            {
                if (null == _argsBuffer)
                    return;

                for (int i = 0; i < _argsBuffer.Length; ++i)
                {
                    ComputeBuffer.CopyCount(visibleMatrixBuffer, _argsBuffer[i], sizeof(uint));
                }
            }
            float elapsedTime = 0f;
            public void Draw(Bounds bounds, bool shadowOn = false)
            {
                if (null == _argsBuffer)
                    return;

                var matCount = _materials.Length;
                for (int i = 0; i < _argsBuffer.Length; ++i)
                {
                    Graphics.DrawMeshInstancedIndirect(_mesh, i, _materials[i % matCount], bounds, _argsBuffer[i], 0,
                    null, shadowOn ? ShadowCastingMode.On : ShadowCastingMode.Off, false, 0);
                    if (FoliageEngine.ShowCount)
                    {
                        if (elapsedTime >= 1f)
                        {
                            uint[] debugArgs = new uint[5];
                            _argsBuffer[i].GetData(debugArgs);
                            visibleCount = debugArgs[1];
                            elapsedTime = 0f;
                        }
                        else
                        {
                            elapsedTime += Time.deltaTime;
                        }
                    }
                }
            }

            public void Release()
            {
                if (null == _argsBuffer)
                    return;

                for (int i = 0; i < _argsBuffer.Length; ++i)
                {
                    _argsBuffer[i]?.Release();
                }
            }

            public void Dispose()
            {
                if (null != _argsBuffer)
                    for (int i = 0; i < _argsBuffer.Length; ++i)
                        _argsBuffer[i] = null;
                if (null != _materials)
                    for (int i = 0; i < _materials.Length; ++i)
                        _materials[i] = null;

                _argsBuffer = null;
                _materials = null;
                _mesh = null;
            }
        }

        private RenderUnit renderUnit;
        public bool isDirty = true;
        private FoliageRenderGroup() { }

        public static FoliageRenderGroup CreateFoliageRenderGroup(int slotIdx, FoliagePaletteAsset paletteAsset, ComputeShader computeShader)
        {
            var slot = paletteAsset.GetSlot(slotIdx);
            var renderGroup = new FoliageRenderGroup();
            renderGroup.paletteSlotIdx = slotIdx;
            renderGroup.paletteAsset = paletteAsset;
            renderGroup.csCulling = computeShader;
            renderGroup.mesh = null != slot ? slot.Mesh : null;
            renderGroup.materials = null != slot ? slot.Materials : null;

            return renderGroup;
        }

        public void SetDirty()
        {
            isDirty = true;
        }

        public ComputeShader csCulling;
        private ComputeBuffer allMatrixBuffer;
        private ComputeBuffer visibleMatrixBuffer;
        private Matrix4x4[] allMatrices;

        private int kernel;
        private readonly int _defaultInstanceCapacity = 320000;
        private readonly int _size_matrix44 = 64;

        public void RefreshRenderData()
        {
            if (null == paletteAsset)
                return;

            var slot = paletteAsset.GetSlot(paletteSlotIdx);
            mesh = null != slot ? slot.Mesh : null;
            materials = null != slot ? slot.Materials : null;
            SetDirty();
        }

        public void InitializeBuffer(bool forceRelease = false)
        {
            if (forceRelease)
            {
                ReleaseBuffer();
            }

            if (null == allMatrixBuffer)
            {
                allMatrixBuffer = new ComputeBuffer(_defaultInstanceCapacity, _size_matrix44);
            }

            if (null == visibleMatrixBuffer)
            {
                visibleMatrixBuffer = new ComputeBuffer(_defaultInstanceCapacity, _size_matrix44, ComputeBufferType.Append);
            }
        }

        public void ReleaseBuffer()
        {
            allMatrixBuffer?.Release();
            visibleMatrixBuffer?.Release();
            allMatrixBuffer = null;
            visibleMatrixBuffer = null;
        }

        public void RefreshBuffer()
        {
            if (renderUnit != null)
                renderUnit.Release();
            else
                renderUnit = new RenderUnit();

            InitializeBuffer();

            allMatrices = paletteAsset.GetMatrices(paletteSlotIdx);
            if (allMatrices == null || allMatrices.Length == 0)
            {
                ReleaseBuffer();
                return;
            }
            
            if (allMatrixBuffer.count < allMatrices.Length)
            {
                allMatrixBuffer.Release();
                allMatrixBuffer = new ComputeBuffer(allMatrices.Length, _size_matrix44);
            }

            CodeTimer.Measure($"RefreshBuffer", () =>
            {
                /* SetData에는 병목이 없다는 거임? */
                allMatrixBuffer.SetData(allMatrices);
                csCulling.SetBuffer(kernel, "_AllMatrices", allMatrixBuffer);
                csCulling.SetBuffer(kernel, "_VisibleMatrices", visibleMatrixBuffer);
            });

            renderUnit.Initialize(mesh, materials, visibleMatrixBuffer);
            kernel = csCulling.FindKernel("CullDistance");
        }

        public void DrawIndirect(Vector3 cameraPosition, Vector3 cameraForward, Bounds bounds)
        {
            allCount = 0;
            visibleCount = 0;

            if (isDirty)
            {
                RefreshBuffer();
                isDirty = false;
            }

            if (allMatrices == null || allMatrices.Length == 0)
                return;

            if (allMatrixBuffer == null || visibleMatrixBuffer == null)
                return;

            var allMatrixCount = allMatrices.Length;
            visibleMatrixBuffer.SetCounterValue(0);
            csCulling.SetVector("_CameraPos", cameraPosition);
            csCulling.SetVector("_CameraForward", cameraForward);
            csCulling.SetFloat("_SqrCullDistance", FoliageEngine.DRAW_DISTANCE * FoliageEngine.DRAW_DISTANCE);
            csCulling.SetInt("_MatrixCount", allMatrixCount);
            csCulling.SetBuffer(kernel, "_AllMatrices", allMatrixBuffer);
            csCulling.SetBuffer(kernel, "_VisibleMatrices", visibleMatrixBuffer);

            int threadGroups = Mathf.CeilToInt(allMatrixCount / 64f);
            csCulling.Dispatch(kernel, threadGroups, 1, 1);

            renderUnit.CopyCount(visibleMatrixBuffer);
            allCount += allMatrixCount;
            visibleCount += renderUnit.visibleCount;

            renderUnit.Draw(bounds);
        }

        public void Dispose()
        {
            renderUnit.Dispose();
            allMatrices = null;
        }
    }
}