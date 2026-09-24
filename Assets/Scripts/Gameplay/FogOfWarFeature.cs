// Assets/Scripts/Gameplay/FogOfWarFeature.cs
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Как рисуется туман войны (<see cref="FogOfWar"/>).
    ///
    /// Проход по всему кадру: для каждой точки по глубине восстанавливается,
    /// где она в мире, и по карте тумана решается, насколько её темнить.
    /// Так темнеет всё — земля, палатки, реквизит, — как в Warcraft 3, а не
    /// одна земля: плоскость тумана поверх земли оставила бы палатки
    /// торчать из черноты, и карта читалась бы вся, не будучи разведанной.
    ///
    /// Идёт до прозрачного: круги выделения, полоски над головами, частицы
    /// рисуются поверх тумана и им не гасятся. Врагов туман прячет сам,
    /// выключая их целиком, — эта работа их не касается.
    ///
    /// Подключён в настройках рендера (PC_Renderer, Mobile_Renderer).
    /// Тумана в сцене нет — прохода нет: кадр не трогается вовсе.
    /// </summary>
    public class FogOfWarFeature : ScriptableRendererFeature
    {
        [Tooltip("Шейдер прохода. Ссылка здесь держит его в сборке: найденный "
               + "по имени шейдер в собранную игру не попал бы.")]
        [SerializeField] private Shader _shader;

        private Material _material;
        private FogPass _pass;

        public override void Create()
        {
            if (_shader == null) _shader = Shader.Find("Hidden/Sinbinder/FogOfWar");
            if (_shader == null)
            {
                Debug.LogWarning("[ТУМАН] Шейдера тумана нет: карта будет видна вся. "
                               + "Проверьте ссылку в настройках рендера.");
                return;
            }

            _material = CoreUtils.CreateEngineMaterial(_shader);
            _pass = new FogPass(_material)
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingTransparents,
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_pass == null || !FogOfWar.Active) return;

            // Только игровая камера: окно сцены в редакторе и превью
            // туманом не закрываем — работать в них надо со всей картой.
            if (renderingData.cameraData.cameraType != CameraType.Game) return;

            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_material);
        }

        private sealed class FogPass : ScriptableRenderPass
        {
            private readonly Material _material;

            private sealed class PassData
            {
                public TextureHandle Source;
                public Material Material;
            }

            public FogPass(Material material)
            {
                _material = material;

                // Глубина нужна, чтобы узнать, где точка в мире; промежуточная
                // текстура — чтобы читать кадр и писать его заново.
                ConfigureInput(ScriptableRenderPassInput.Depth);
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;

                TextureHandle source = resources.activeColorTexture;

                var desc = renderGraph.GetTextureDesc(source);
                desc.name = "_FogOfWarColor";
                desc.clearBuffer = false;
                TextureHandle destination = renderGraph.CreateTexture(desc);

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Туман войны", out var data))
                {
                    data.Source = source;
                    data.Material = _material;

                    builder.UseTexture(source, AccessFlags.Read);
                    if (resources.cameraDepthTexture.IsValid())
                        builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);

                    // Карта тумана и глубина кадра приходят глобальными.
                    builder.UseAllGlobalTextures(true);
                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);

                    builder.SetRenderFunc((PassData d, RasterGraphContext ctx) =>
                        Blitter.BlitTexture(ctx.cmd, d.Source, new Vector4(1f, 1f, 0f, 0f), d.Material, 0));
                }

                resources.cameraColor = destination;
            }
        }
    }
}
