using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Mathematics;

public class TemporalAAFeature : ScriptableRendererFeature
{
    [Range(0, 1)]
    public float TemporalFade = 0.8f;

    class TemporalAAPass : ScriptableRenderPass
    {

        [Range(0, 1)]
        public float TemporalFade = 0;

        public static RenderTexture temp, temp1;

        private Material mat;

        private Matrix4x4 prevViewProjectionMatrix;

        public TemporalAAPass() : base()
        {
            if (temp)
            {
                temp.Release();
                temp1.Release();
                temp = null;
            }
        }

        // This method is called before executing the render pass.
        // It can be used to configure render targets and their clear state. Also to create temporary render target textures.
        // When empty this render pass will render to the active camera render target.
        // You should never call CommandBuffer.SetRenderTarget. Instead call <c>ConfigureTarget</c> and <c>ConfigureClear</c>.
        // The render pipeline will ensure target setup and clearing happens in an performance manner.
        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            if (temp && (cameraTextureDescriptor.width != temp.width || cameraTextureDescriptor.height != temp.height))
            {
                Debug.Log("Deleting Render Target: " + cameraTextureDescriptor.width + " " + temp.width);

                temp.Release();
                temp1.Release();
                temp = null;
            }

            if (!temp)
            {
                temp = new RenderTexture(cameraTextureDescriptor);
                temp1 = new RenderTexture(cameraTextureDescriptor);

                Debug.Log("Allocating new Render Target");
            }
        }


        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            base.OnCameraSetup(cmd, ref renderingData);

            ConfigureInput(ScriptableRenderPassInput.Color);
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        // Here you can implement the rendering logic.
        // Use <c>ScriptableRenderContext</c> to issue drawing commands or execute command buffers
        // https://docs.unity3d.com/ScriptReference/Rendering.ScriptableRenderContext.html
        // You don't have to call ScriptableRenderContext.submit, the render pipeline will call it at specific points in the pipeline.
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get("TemporalAAPass");

            if (!mat)
            {
                mat = Resources.Load<Material>("Graphics/TemporalAA/TemporalAAMaterial");
            }



            mat.SetTexture("_TemporalAATexture", temp);


            Matrix4x4 mt = renderingData.cameraData.camera.nonJitteredProjectionMatrix.inverse;
            mat.SetMatrix("_invP", mt);

            mt = this.prevViewProjectionMatrix * renderingData.cameraData.camera.cameraToWorldMatrix;
            mat.SetMatrix("_FrameMatrix", mt);

            mat.SetFloat("_TemporalFade", TemporalFade);


            Blit(cmd, BuiltinRenderTextureType.CurrentActive, temp1, mat);

            Blit(cmd, temp1, renderingData.cameraData.renderer.cameraColorTarget);


            //Ping pong
            RenderTexture temp2 = temp;
            temp = temp1;
            temp1 = temp2;

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);


            this.prevViewProjectionMatrix = renderingData.cameraData.camera.nonJitteredProjectionMatrix * renderingData.cameraData.camera.worldToCameraMatrix;

            renderingData.cameraData.camera.ResetProjectionMatrix();
        }

        /// Cleanup any allocated resources that were created during the execution of this render pass.
        public override void FrameCleanup(CommandBuffer cmd)
        {
            //temp.Release();
        }
    }

    TemporalAAPass m_temporalPass;

    public override void Create()
    {
        m_temporalPass = new TemporalAAPass();
        m_temporalPass.TemporalFade = this.TemporalFade;

        // Configures where the render pass should be injected.
        m_temporalPass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(m_temporalPass);
    }
}
