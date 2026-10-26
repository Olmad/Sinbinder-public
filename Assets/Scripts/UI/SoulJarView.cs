// Assets/Scripts/UI/SoulJarView.cs
using UnityEngine;
using UnityEngine.UI;
using Sinbinder.Core;
using Sinbinder.Gameplay;

namespace Sinbinder.UI
{
    /// <summary>
    /// Огонь души выделенного — живой и объёмный. Автор, 26 октября: «огонёк
    /// души в интерфейсе должен гореть, вероятно в 3D» — про каплю на панели
    /// выделенного (<see cref="SelectedUnitPanelUI"/>): плоская, в два цвета,
    /// она не горела.
    ///
    /// Теперь на её месте — банка его души, та же модель, что на полке склепа
    /// (<c>Props/SoulJarFull</c>), и та же краска (<see cref="SoulJarGlow"/>):
    /// ядро — громкий грех, пламя — второй, свет дышит. Банка медленно
    /// поворачивается. Снимает её своя камера в текстуру; банка стоит далеко
    /// под миром, а камера видит на три метра — больше ей ничего не попасть.
    ///
    /// Дёшево: камера выключена и снимает сама по <see cref="Fps"/> раз
    /// в секунду, и только пока панель видна (автор: «не забывай про оптимизацию»).
    /// Нет модели — остаётся плоская капля, как прежде.
    /// </summary>
    public class SoulJarView : MonoBehaviour
    {
        private const float Fps = 15f;
        private const int Size = 192;
        private static readonly Vector3 Stage = new Vector3(0f, -1000f, 0f);

        private RawImage _image;
        private Camera _camera;
        private RenderTexture _texture;
        private GameObject _full, _empty, _stage;
        private SoulJarGlow _glow;
        private float _nextShot;
        private string _shownFor;

        /// <summary>Поставить банку поверх капли. Нет модели — null, капля остаётся.</summary>
        public static SoulJarView Over(Image drop)
        {
            if (drop == null) return null;
            var full = Resources.Load<GameObject>("Props/SoulJarFull");
            if (full == null) return null;

            var go = new GameObject("Банка души (вид)", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(drop.transform.parent, false);
            var src = drop.rectTransform;
            rt.anchorMin = src.anchorMin; rt.anchorMax = src.anchorMax; rt.pivot = src.pivot;
            rt.anchoredPosition = src.anchoredPosition;
            // Банка выше капли: чуть шире и выше, чтобы читалась целиком.
            rt.sizeDelta = new Vector2(Mathf.Max(src.sizeDelta.x, src.sizeDelta.y) * 1.25f,
                                       Mathf.Max(src.sizeDelta.x, src.sizeDelta.y) * 1.25f);
            rt.SetSiblingIndex(drop.transform.GetSiblingIndex() + 1);

            var view = go.AddComponent<SoulJarView>();
            view.Build(full);
            return view;
        }

        private void Build(GameObject full)
        {
            _image = gameObject.AddComponent<RawImage>();
            _image.raycastTarget = false;

            _texture = new RenderTexture(Size, Size, 16, RenderTextureFormat.ARGB32) { name = "Банка души" };
            _image.texture = _texture;

            // Отдельным объектом, не под холстом: масштаб холста исказил бы банку.
            // Уходит вместе с панелью (OnDestroy).
            var stage = new GameObject("Сцена банки души (интерфейс)");
            stage.transform.position = Stage;
            _stage = stage;

            _full = Jar(full, stage.transform);
            _glow = _full.AddComponent<SoulJarGlow>();
            var emptyModel = Resources.Load<GameObject>("Props/SoulJar");
            if (emptyModel != null) { _empty = Jar(emptyModel, stage.transform); _empty.SetActive(false); }

            var camGo = new GameObject("Камера банки");
            camGo.transform.SetParent(stage.transform, false);
            // 1,15 м: при 0,95 крышку банки срезало верхним краем (кадр 26.10).
            camGo.transform.localPosition = new Vector3(0f, 0.21f, -1.15f);
            camGo.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
            _camera = camGo.AddComponent<Camera>();
            _camera.enabled = false;                 // снимаем сами, по счёту
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _camera.fieldOfView = 26f;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 3f;               // дальше — ничего, мир в километре
            _camera.targetTexture = _texture;
            _camera.allowHDR = false;
            _camera.allowMSAA = false;

            // Свой тусклый свет спереди — банка не в темноте, когда душа тлеет.
            var key = new GameObject("Свет банки").AddComponent<Light>();
            key.transform.SetParent(stage.transform, false);
            key.transform.localPosition = new Vector3(0.4f, 0.6f, -0.6f);
            key.type = LightType.Point;
            key.range = 2f;
            key.intensity = 0.6f;
            key.color = new Color(0.85f, 0.80f, 0.75f);
        }

        private static GameObject Jar(GameObject model, Transform stage)
        {
            var root = new GameObject("Банка");
            root.transform.SetParent(stage, false);
            var body = Object.Instantiate(model, root.transform, false);
            // Корень модели несёт масштаб и поворот импорта — умножаем (как SoulShelf).
            body.transform.localScale = model.transform.localScale * 1.8f;
            foreach (var r in body.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return root;
        }

        /// <summary>Показать душу; null — пустая банка (Греховод, без души).</summary>
        public void Show(SoulData soul)
        {
            string key = soul != null ? soul.Name + soul.Sin : "";
            if (key == _shownFor) return;
            _shownFor = key;

            bool has = soul != null;
            if (_full != null) _full.SetActive(has);
            if (_empty != null) _empty.SetActive(!has);
            if (has && _glow != null) _glow.Show(soul);
            _nextShot = 0f;
        }

        void LateUpdate()
        {
            if (_camera == null || !isActiveAndEnabled) return;
            if (Time.unscaledTime < _nextShot) return;
            _nextShot = Time.unscaledTime + 1f / Fps;

            // Медленно поворачивается: объём читается движением.
            float yaw = Time.unscaledTime * 18f;
            if (_full != null) _full.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            if (_empty != null) _empty.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            _camera.Render();
        }

        void OnDestroy()
        {
            if (_texture != null) { _texture.Release(); Destroy(_texture); }
            if (_stage != null) Destroy(_stage);
        }
    }
}
