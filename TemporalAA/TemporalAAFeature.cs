/*
MIT License

Copyright (c) 2022 Pascal Zwick

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Mathematics;

public class TemporalAAFeature : ScriptableRendererFeature
{
    [Range(0, 1)]
    public float TemporalFade = 0.99f;
    public float MovementBlending = 100;

    public Material TAAMaterial;

    class TemporalAAPass : ScriptableRenderPass
    {

        [Range(0, 1)]
        public float TemporalFade;
        public float MovementBlending;

        public Material TAAMaterial;

        public static RenderTexture temp, temp1;

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

        }


        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            base.OnCameraSetup(cmd, ref renderingData);

            if (!renderingData.cameraData.camera.GetComponent<TemporalAACamera>())
                return;

            ConfigureInput(ScriptableRenderPassInput.Color);
            ConfigureInput(ScriptableRenderPassInput.Depth);

            RenderTextureDescriptor currentCameraDescriptor = renderingData.cameraData.cameraTargetDescriptor;

            if (temp && (currentCameraDescriptor.width != temp.width || currentCameraDescriptor.height != temp.height))
            {
                Debug.Log("Deleting Render Target: " + currentCameraDescriptor.width + " " + temp.width);

                temp.Release();
                temp1.Release();
                temp = null;
            }

            if (!temp)
            {
                temp = new RenderTexture(currentCameraDescriptor.width, currentCameraDescriptor.height, UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat, 0);
                temp1 = new RenderTexture(currentCameraDescriptor.width, currentCameraDescriptor.height, UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat, 0);

                Debug.Log("Allocating new Render Target");
            }
        }

        // Here you can implement the rendering logic.
        // Use <c>ScriptableRenderContext</c> to issue drawing commands or execute command buffers
        // https://docs.unity3d.com/ScriptReference/Rendering.ScriptableRenderContext.html
        // You don't have to call ScriptableRenderContext.submit, the render pipeline will call it at specific points in the pipeline.
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            //Only enable TAA on the camera featuring the TAA Camera script
            if (!renderingData.cameraData.camera.GetComponent<TemporalAACamera>())
                return;

            CommandBuffer cmd = CommandBufferPool.Get("TemporalAAPass");


            TAAMaterial.SetTexture("_TemporalAATexture", temp);


            Matrix4x4 mt = renderingData.cameraData.camera.nonJitteredProjectionMatrix.inverse;
            TAAMaterial.SetMatrix("_invP", mt);

            mt = this.prevViewProjectionMatrix * renderingData.cameraData.camera.cameraToWorldMatrix;
            TAAMaterial.SetMatrix("_FrameMatrix", mt);

            TAAMaterial.SetFloat("_TemporalFade", TemporalFade);
            TAAMaterial.SetFloat("_MovementBlending", MovementBlending);


            Blit(cmd, BuiltinRenderTextureType.CurrentActive, temp1, TAAMaterial);

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
        m_temporalPass.MovementBlending = this.MovementBlending;
        m_temporalPass.TAAMaterial = this.TAAMaterial;

        // Configures where the render pass should be injected.
        m_temporalPass.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(m_temporalPass);
    }
}
