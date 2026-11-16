// Assets/Scripts/Gameplay/SoulInHandView.cs
// Перевод: текста нет
using UnityEngine;
using Sinbinder.Core;
using Sinbinder.Crypt;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Душа в руке — видна (план 66, сцена 1: «душа — свет»).
    ///
    /// До 16.11 R клал душу в руку, а на экране ничего не менялось: оружие пряталось
    /// (<see cref="WeaponView"/>), и только строка журнала говорила, что в руке что-то есть.
    /// Автор, 12.11: «с банками нужно обязательно взять в руку душу с камня» — значит, взятое
    /// должно быть видно. Теперь над раскрытой ладонью огонёк цвета греха этой души: дышит,
    /// вокруг кружат искры, ладонь под ним освещена.
    ///
    /// Глазами — внизу справа, как оружие. Сверху и сбоку — огонёк у правой руки Греховода,
    /// крупнее, чтобы читался с высоты. В сценке, на паузе руки и у мёртвого — не виден.
    /// </summary>
    public class SoulInHandView : MonoBehaviour
    {
        private static SoulInHandView _one;

        private Transform _root, _palm, _sleeve, _orb, _core, _halo;
        private readonly Transform[] _motes = new Transform[3];
        private Material _coreMat, _haloMat, _moteMat;
        private Light _glow;
        private SoulData _soul;
        private SinbinderPlayer _builtFor;
        private Color _tint = Color.white;
        private float _shown;

        // Оси камеры: x вправо, y вверх, z вперёд. Ладонь — над полосой выделенного (она
        // занимает нижнюю пятую экрана): при y −0,24 ладонь и огонёк ушли под неё (кадр 16.11).
        private static readonly Vector3 PalmAt = new Vector3(0.15f, -0.115f, 0.50f);
        private static readonly Vector3 Elbow = new Vector3(0.40f, -0.60f, 0.12f);
        private const float OrbHeight = 0.072f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() { _one = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_one != null) return;
            var go = new GameObject("Душа в руке");
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
            // Новая сцена — новый Греховод: рукав берёт сукно с него, старое могло уйти со сценой.
            if (_root != null && _builtFor != hero) { Destroy(_root.gameObject); _root = null; }
            if (_root == null) { Build(hero); _builtFor = hero; }
            if (CryptHands.Soul != _soul) Tint(CryptHands.Soul);
            if (!_root.gameObject.activeSelf) _root.gameObject.SetActive(true);

            _shown = Mathf.MoveTowards(_shown, 1f, Time.deltaTime * 4f);
            float s = Smooth(_shown);
            float t = Time.time;
            var c = cam.transform;

            // Глазами — ладонь с рукавом; со стороны рука своя, у тела, — только огонёк.
            bool eyes = view.FirstPersonNow;
            if (_palm.gameObject.activeSelf != eyes) { _palm.gameObject.SetActive(eyes); _sleeve.gameObject.SetActive(eyes); }
            if (eyes)
            {
                var at = PalmAt;
                at.y -= (1f - s) * 0.3f;                               // входит снизу
                at.y += Mathf.Sin(t * 1.3f) * 0.004f;                  // рука живая, не прибита к кадру
                _root.localScale = Vector3.one;
                _root.SetPositionAndRotation(c.TransformPoint(at), c.rotation * Quaternion.Euler(-24f, -8f, 6f));
                var wrist = _root.position - _root.forward * 0.05f - _root.up * 0.01f;
                var elbow = c.TransformPoint(Elbow + new Vector3(0f, -(1f - s) * 0.3f, 0f));
                var arm = elbow - wrist;
                _sleeve.SetPositionAndRotation((wrist + elbow) * 0.5f, Quaternion.FromToRotation(Vector3.up, arm));
                _sleeve.localScale = new Vector3(0.10f, arm.magnitude * 0.5f, 0.10f);
            }
            else
            {
                var h = hero.transform;
                _root.localScale = Vector3.one * 2.6f;
                _root.SetPositionAndRotation(h.position + h.right * 0.36f + h.forward * 0.30f + Vector3.up * 0.98f, h.rotation);
            }

            // Огонёк дышит; искры кружат вокруг на разной высоте.
            float breath = 1f + Mathf.Sin(t * 2.4f) * 0.09f;
            _orb.localPosition = new Vector3(0f, OrbHeight + Mathf.Sin(t * 1.7f) * 0.006f, 0.005f);
            _core.localScale = Vector3.one * (0.030f * breath * s);
            _halo.localScale = Vector3.one * (0.17f * (0.94f + 0.06f * breath) * s);
            _halo.rotation = c.rotation;
            for (int i = 0; i < _motes.Length; i++)
            {
                float a = t * (1.4f + i * 0.55f) + i * 2.1f;
                float r = 0.034f + i * 0.007f;
                _motes[i].localPosition = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a * 1.3f + i) * 0.016f, Mathf.Sin(a) * r);
                _motes[i].localScale = Vector3.one * (0.0075f * s * (0.7f + 0.3f * Mathf.Sin(t * 5f + i * 1.7f)));
                _motes[i].rotation = c.rotation;                       // плоские — лицом к камере
            }
            _glow.intensity = 0.32f * s * breath;
            _glow.range = 0.55f * _root.localScale.x;
            var halo = _tint; halo.a = 0.55f * s;
            _haloMat.SetColor("_BaseColor", halo);
        }

        private static bool TalkShot()
        {
            var talk = Dialogue.DialogueCameraController.Instance;
            return talk != null && (talk.InDialogue || talk.Homebound);
        }

        /// <summary>Цвет — грех души, разбавленный белым: огонёк, а не лампочка греха.</summary>
        private void Tint(SoulData soul)
        {
            _soul = soul;
            var sin = soul != null ? SinPalette.Of(soul.Sin) : new Color(0.75f, 0.85f, 1f);
            _tint = Color.Lerp(sin, Color.white, 0.3f);
            _coreMat.color = Color.Lerp(_tint, Color.white, 0.65f) * 1.6f;
            var mote = Color.Lerp(_tint, Color.white, 0.4f); mote.a = 0.9f;
            _moteMat.SetColor("_BaseColor", mote);
            _glow.color = _tint;
        }

        private void Build(SinbinderPlayer hero)
        {
            _root = new GameObject("Ладонь").transform;
            _root.SetParent(transform, false);

            // Ладонь горстью: плоская кость, четыре пальца подняты краем, большой — справа.
            var skin = Lit(new Color(0.60f, 0.56f, 0.48f));
            _palm = new GameObject("Горсть").transform;
            _palm.SetParent(_root, false);
            Shape(PrimitiveType.Sphere, _palm, skin, Vector3.zero, Quaternion.identity, new Vector3(0.095f, 0.028f, 0.11f));
            for (int i = 0; i < 4; i++)
            {
                float x = -0.033f + i * 0.022f;
                float len = i == 0 || i == 3 ? 0.019f : 0.023f;
                Shape(PrimitiveType.Capsule, _palm, skin, new Vector3(x, 0.022f, 0.062f),
                      Quaternion.Euler(52f, 0f, 0f), new Vector3(0.019f, len, 0.019f));
            }
            Shape(PrimitiveType.Capsule, _palm, skin, new Vector3(0.056f, 0.016f, 0.012f),
                  Quaternion.Euler(28f, 0f, -55f), new Vector3(0.021f, 0.022f, 0.021f));

            Material cloth = null;
            foreach (var r in hero.GetComponentsInChildren<Renderer>(true))
                if (r.name.StartsWith("SinGarment") || r.name.StartsWith("SinSleeveRight")
                    || r.transform.parent != null && r.transform.parent.name.StartsWith("SinSleeveRight"))
                { cloth = r.sharedMaterial; break; }
            if (cloth == null) cloth = Lit(new Color(0.42f, 0.05f, 0.06f));
            _sleeve = new GameObject("Рукав").transform;
            _sleeve.SetParent(_root, false);
            Shape(PrimitiveType.Cylinder, _sleeve, cloth, Vector3.zero, Quaternion.identity, Vector3.one);

            // Огонёк: ядро, ореол лицом к камере, три искры и слабый свет на ладонь.
            _orb = new GameObject("Огонёк").transform;
            _orb.SetParent(_root, false);
            _coreMat = SoulGlow.Core(Color.white);
            _core = SoulGlow.Shape(PrimitiveType.Sphere, _orb, _coreMat, Vector3.zero, 0.03f);
            _haloMat = SoulGlow.Halo();
            _halo = SoulGlow.Shape(PrimitiveType.Quad, _orb, _haloMat, Vector3.zero, 0.17f);
            _moteMat = SoulGlow.Halo("Душа — искры");
            for (int i = 0; i < _motes.Length; i++)
                _motes[i] = SoulGlow.Shape(PrimitiveType.Quad, _orb, _moteMat, Vector3.zero, 0.008f);

            // Слабо и близко: у кулака свет в 0,5 выбеливал его (кадр w0811g, WeaponView).
            _glow = SoulGlow.Lamp(_orb, Color.white, 0.55f, 0.3f);
            _glow.transform.localPosition = new Vector3(0f, 0.03f, -0.03f);
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
