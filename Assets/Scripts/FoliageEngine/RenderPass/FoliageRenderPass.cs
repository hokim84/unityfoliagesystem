using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;

namespace PWTA
{
    public class FoliageRenderPass : ScriptableRenderPass
    {
        List<FoliageRenderGroup> paletteList;
        ComputeShader computeShader;
        int kernel;

        public FoliageRenderPass(ComputeShader cs)
        {
            computeShader = cs;
            kernel = cs.FindKernel("CSMain");
            renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
            paletteList = new List<FoliageRenderGroup>();
        }

        public void AddPalette(FoliageRenderGroup renderGroup)
        {
            paletteList.Add(renderGroup);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var camPos = renderingData.cameraData.camera.transform.position;

            var cmd = CommandBufferPool.Get("FoliagePass");

            // foreach (var p in paletteList)
            // {
            //     if (p.matrixCount == 0 || p.mesh == null || p.material == null)
            //         continue;

            //     p.visibleMatrixBuffer.SetCounterValue(0);

            //     cmd.SetComputeVectorParam(computeShader, "_CameraPos", camPos);
            //     cmd.SetComputeFloatParam(computeShader, "_CullDistance", 100f);
            //     cmd.SetComputeIntParam(computeShader, "_MatrixCount", p.matrixCount);

            //     cmd.SetComputeBufferParam(computeShader, kernel, "_AllMatrices", p.allMatrixBuffer);
            //     cmd.SetComputeBufferParam(computeShader, kernel, "_VisibleMatrices", p.visibleMatrixBuffer);
            //     cmd.SetComputeBufferParam(computeShader, kernel, "_ArgsBuffer", p.argsBuffer);

            //     int tg = Mathf.CeilToInt(p.matrixCount / 64f);
            //     cmd.DispatchCompute(computeShader, kernel, tg, 1, 1);

            //     cmd.CopyCounterValue(p.visibleMatrixBuffer, p.argsBuffer, sizeof(uint));
            //     p.material.SetBuffer("_InstanceTransforms", p.visibleMatrixBuffer);
            //     cmd.DrawMeshInstancedIndirect(p.mesh, 0, p.material, 0, p.argsBuffer);
            // }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);

            paletteList.Clear(); // 프레임마다 갱신 필요
        }
    }
}