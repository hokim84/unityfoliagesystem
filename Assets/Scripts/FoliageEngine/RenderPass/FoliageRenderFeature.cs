using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PWTA
{
    public class FoliageRenderFeature : ScriptableRendererFeature
    {
        public ComputeShader computeShader;
        public Mesh instanceMesh;
        public Material instanceMaterial;

        private FoliageRenderPass foliagePass;

        public override void Create()
        {
            // 나중에 외부에서 buffer 등 전달 필요
        }

        public void SetupAndInject()
        {
            foliagePass = new FoliageRenderPass(computeShader);
            //foliagePass.AddPalette(instanceMesh, instanceMaterial, all, visible, args, count, bounds);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (foliagePass != null)
                renderer.EnqueuePass(foliagePass as ScriptableRenderPass);
        }
    }
}
