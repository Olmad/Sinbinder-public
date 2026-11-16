// Assets/Scripts/Gameplay/SoulInHandView.cs
// Перевод: текста нет
using UnityEngine;
using Sinbinder.Core;
using Sinbinder.Crypt;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Банка с душой в руке — видна (план 66, сцена 1: «душа — свет»).
    ///
    /// До 16.11 R клал душу в руку, а на экране ничего не менялось. 16.11 над ладонью встал
    /// огонёк — автор: «руки выглядят так себе. почему не использовать руку самого Греховода»,
    /// «непонятно зачем брать душу в руку — почему бы не брать банку с душой?». Теперь в руке —
    /// банка, та же, что стояла на камне: светится цветом души, под крышкой. Держит её кисть
    /// самого Греховода (<see cref="HeroArm"/>), раскрытой ладонью снизу.
    ///
    /// Глазами — внизу справа, над полосой выделенного. Сверху и сбоку — банка в его правой
    /// руке, крупнее, чтобы читалась с высоты. В сценке, на паузе руки и у мёртвого — не видна.
    /// </summary>
    public class SoulInHandView : MonoBehaviour
    {
        private static SoulInHandView _one;

        private Transform _root, _jar, _halo;
        private HeroArm _arm;
        private Transform _bone;
        private Material _soulMat, _haloMat;
        private Light _glow;
        private SoulData _soul;
        private SinbinderPlayer _builtFor;
        private Color _tint = Color.white;
        private float _shown;

        // Оси камеры: x вправо, y вверх, z вперёд. Банка — над полосой выделенного (она
        // занимает нижнюю пятую экрана): при y −0,24 ладонь ушла под неё (кадр 16.11).
        private static readonly Vector3 JarAt = new Vector3(0.17f, -0.08f, 0.52f);
        private static readonly Vector3 Elbow = new Vector3(0.40f, -0.55f, 0.18f);
        // Кисть держит банку сбоку, со стороны глаз: ладонь к банке, пальцы вверх вдоль неё —
        // видны тыл кисти и пальцы. Ладонью вверх под банкой (первый заход 16.11) кисть с высоты
        // глаз видна ребром — зонд HandProbe: три чёрточки под банкой.
        private static readonly Vector3 PalmToJar = new Vector3(-0.25f, 0.08f, 1f);
        private static readonly Vector3 FingersUp = new Vector3(-0.10f, 1f, 0.15f);
        private const float JarRadius = 0.04f, JarHeight = 0.10f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() { _one = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_one != null) return;
            var go = new GameObject("Банка в руке");
            DontDestroyOnLoad(go);
            _one = go.AddComponent<SoulInHandView>();
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            var view = cam != null ? cam.GetComponent<RTS_Camera>() : null;
            var hero = SinbinderPlayer.Instance;
            bool want = view != null && hero != null && !hero.IsDead && CryptHands.HasSoul
                     && view.Lying < 0.01f && !DreamTutorialDirector.InputLocked && !TalkShot();
            if (!want)
            {
                _shown = 0f;
                if (_root != null && _root.gameObject.activeSelf) _root.gameObject.SetActive(false);
                return;
            }
            // Новая сцена — новый Греховод: рука и сукно — с него, старые ушли со сценой.
            if (_root != null && _builtFor != hero) Clear();
            if (_root == null) { Build(hero); _builtFor = hero; }
            if (CryptHands.Soul != _soul) Tint(CryptHands.Soul);
            if (!_root.gameObject.activeSelf) _root.gameObject.SetActive(true);

            _shown = Mathf.MoveTowards(_shown, 1f, Time.deltaTime * 4f);
            float s = Smooth(_shown);
            float t = Time.time;
            var c = cam.transform;

            // Глазами — кисть Греховода держит банку; со стороны рука своя, у тела, — только банка.
            bool eyes = view.FirstPersonNow;
            if (_arm != null) _arm.Show(eyes);
            if (eyes)
            {
                var at = JarAt;
                at.y -= (1f - s) * 0.3f;                               // входит снизу
                at.y += Mathf.Sin(t * 1.3f) * 0.004f;                  // рука живая, не прибита к кадру
                Hold(_arm, _jar, c, at, s);
            }
            else
            {
                // Сверху — в его правой кисти, крупнее: с высоты банка с ноготь не читалась бы.
                var h = hero.transform;
                var where = _bone != null ? _bone.position - Vector3.up * 0.06f
                                          : h.position + h.right * 0.36f + h.forward * 0.30f + Vector3.up * 0.98f;
                _jar.SetPositionAndRotation(where, h.rotation);
                _jar.localScale = Vector3.one * 2.2f;
            }

            // Душа в банке дышит: свет и ореол.
            float breath = 1f + Mathf.Sin(t * 2.4f) * 0.09f;
            _halo.localScale = Vector3.one * (0.30f * breath * s);
            _halo.rotation = c.rotation;
            _glow.intensity = 0.45f * s * breath;
            _glow.range = 0.7f * _jar.localScale.x;
            var halo = _tint; halo.a = 0.45f * s;
            _haloMat.SetColor("_BaseColor", halo);
            _soulMat.color = Color.Lerp(_tint, Color.white, 0.25f) * (1.05f + 0.15f * breath);
        }

        /// <summary>
        /// Банка в кисти глазами: банка — в точке <paramref name="at"/> (оси камеры), прямо; кисть —
        /// сзади, ладонью к банке, пальцами вверх; предплечье — к локтю за краем кадра. Общий
        /// с зондом (Editor/HandProbe): он снимает ровно то, что в игре.
        /// </summary>
        public static void Hold(HeroArm arm, Transform jar, Transform c, Vector3 at, float shown)
        {
            var jarCentre = c.TransformPoint(at);
            jar.SetPositionAndRotation(jarCentre, c.rotation * Quaternion.Euler(0f, 0f, 4f));
            jar.localScale = Vector3.one;
            if (arm == null) return;
            var palm = c.TransformDirection(PalmToJar.normalized);
            var fingers = c.TransformDirection(FingersUp.normalized);
            var rot = arm.Pose(fingers, palm);
            // Середина кисти — у стенки банки, на ладонь ниже середины банки: пальцы обнимают её.
            var handCentre = jarCentre - palm * (JarRadius + 0.012f) - fingers * 0.012f;
            var wrist = handCentre - rot * arm.Centre;
            arm.Hand.SetPositionAndRotation(wrist, rot);
            arm.Reach(wrist, c.TransformPoint(Elbow + new Vector3(0f, -(1f - shown) * 0.3f, 0f)));
        }

        /// <summary>Банка, как на камне у дороги: светится душой, тёмная крышка и поясок. Зовёт и зонд.</summary>
        public static Transform Jar(Transform parent, out Material soul)
        {
            var jar = new GameObject("Банка с душой").transform;
            jar.SetParent(parent, false);
            soul = SoulGlow.Core(Color.white);
            Shape(PrimitiveType.Cylinder, jar, soul, Vector3.zero, Quaternion.identity,
                  new Vector3(JarRadius * 2f, JarHeight * 0.5f, JarRadius * 2f));
            Shape(PrimitiveType.Cylinder, jar, Lit(new Color(0.30f, 0.22f, 0.14f)), Vector3.up * (JarHeight * 0.5f + 0.008f),
                  Quaternion.identity, new Vector3(JarRadius * 2.25f, 0.009f, JarRadius * 2.25f));
            // Горлышко — тёмный поясок под крышкой: силуэт банки, а не светящейся шайбы.
            Shape(PrimitiveType.Cylinder, jar, Lit(new Color(0.18f, 0.20f, 0.24f)), Vector3.up * (JarHeight * 0.5f - 0.006f),
                  Quaternion.identity, new Vector3(JarRadius * 2.08f, 0.006f, JarRadius * 2.08f));
            return jar;
        }

        private static bool TalkShot()
        {
            var talk = Dialogue.DialogueCameraController.Instance;
            return talk != null && (talk.InDialogue || talk.Homebound);
        }

        /// <summary>Цвет — грех души, разбавленный белым: банка светится душой, а не лампочкой греха.</summary>
        private void Tint(SoulData soul)
        {
            _soul = soul;
            var sin = soul != null ? SinPalette.Of(soul.Sin) : new Color(0.55f, 0.75f, 1f);
            _tint = Color.Lerp(sin, Color.white, 0.3f);
            _glow.color = _tint;
        }

        private void Clear()
        {
            if (_arm != null) _arm.Destroy();
            _arm = null;
            if (_root != null) Destroy(_root.gameObject);
            _root = null;
        }

        private void Build(SinbinderPlayer hero)
        {
            _root = new GameObject("Рука с банкой").transform;
            _root.SetParent(transform, false);

            // Банка — как на камне у дороги (DreamRoad.BuildShrine).
            _jar = Jar(_root, out _soulMat);
            _haloMat = SoulGlow.Halo();
            _halo = SoulGlow.Shape(PrimitiveType.Quad, _jar, _haloMat, Vector3.zero, 0.3f);
            _glow = SoulGlow.Lamp(_jar, Color.white, 0.7f, 0.45f);
            _glow.transform.localPosition = new Vector3(0f, 0.02f, -0.05f);

            // Кисть — его собственная, вырезана из модели в нынешней позе.
            _arm = HeroArm.Cut(hero, true, _root);
            _bone = HeroArm.Bone(hero, "RightHand");
            if (_arm == null) Debug.LogWarning("[РУКИ] кисть Греховода не вырезалась — банка без руки");
            else Debug.Log("[РУКИ] кисть Греховода держит банку");
            Tint(CryptHands.Soul);
        }

        /// <summary>Примитив без коллайдера и тени.</summary>
        private static Transform Shape(PrimitiveType type, Transform parent, Material mat, Vector3 at, Quaternion rot, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go.transform;
        }

        private static Material Lit(Color colour)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var m = new Material(shader) { color = colour };
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.25f);
            return m;
        }

        private static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
    }
}
