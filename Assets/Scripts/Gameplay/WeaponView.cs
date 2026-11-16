// Assets/Scripts/Gameplay/WeaponView.cs
// Перевод: текста нет
using System.Collections.Generic;
using UnityEngine;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Оружие в руке — глазами (автор, 08.11: «нужно показывать руку с мечом, когда он достат,
    /// не значком, а моделькой справа»).
    ///
    /// От первого лица тело выключено целиком (<see cref="RTS_Camera"/>, ShowHero), а с ним и
    /// вещь на кости кисти. Здесь её копия: внизу справа, кулак на рукояти, рукав к локтю за
    /// краем кадра. Удар (<see cref="FirstPersonCombat"/>) — взмах справа налево; блок (ПКМ) —
    /// оружие поперёк. Копия, а не сама вещь: та висит на кости и нужна со стороны — в разговоре
    /// и сверху.
    ///
    /// Что в руке: взятое со стойки — только наголо (<see cref="Armament"/>, B); без него — посох
    /// Греховода (SinStaff), он при нём всегда. Руки заняты пластинкой или душой — оружия не видно.
    /// </summary>
    public class WeaponView : MonoBehaviour
    {
        private static WeaponView _one;

        private GameObject _source;
        private Transform _holder, _fist, _sleeve;
        private HeroArm _arm;               // его собственная кисть (16.11); нет — кулак и рукав-примитивы
        private readonly List<GameObject> _parts = new List<GameObject>();
        private float _swingT = -1f, _shown, _phase;
        private Vector3 _lastEye;

        // Позы — в осях камеры: x вправо, y вверх, z вперёд. dir — куда смотрит остриё.
        // Покой — в правой трети кадра, кулак над нижней панелью (кадр w0811f: посох шёл
        // через середину, кулак прятался под панелью выделенного).
        private static readonly Vector3 IdleAt = new Vector3(0.30f, -0.19f, 0.50f), IdleDir = new Vector3(-0.10f, 0.93f, 0.35f);
        private static readonly Vector3 BlockAt = new Vector3(0.10f, -0.10f, 0.48f), BlockDir = new Vector3(-0.97f, 0.18f, 0.18f);
        private static readonly Vector3 WindAt = new Vector3(0.36f, -0.06f, 0.40f), WindDir = new Vector3(0.35f, 0.85f, -0.20f);
        // Конец удара — выше нижней панели: при y −0,24 взмах уходил под неё, и на кадре удара
        // (сон, 16.11) посоха не было вовсе — игрок видел, как оружие пропадает.
        private static readonly Vector3 CutAt = new Vector3(-0.12f, -0.13f, 0.58f), CutDir = new Vector3(-0.80f, -0.15f, 0.58f);
        private static readonly Vector3 Elbow = new Vector3(0.45f, -0.55f, 0.10f);
        private const float SwingSeconds = 0.6f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() { _one = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_one != null) return;
            var go = new GameObject("Оружие глазами");
            DontDestroyOnLoad(go);
            _one = go.AddComponent<WeaponView>();
        }

        /// <summary>Удар принят — взмах (зовёт <see cref="FirstPersonCombat"/>).</summary>
        public static void Swing()
        {
            if (_one != null) _one._swingT = 0f;
        }

        /// <summary>Что сейчас в руке, если показывать нечего — null.</summary>
        private static GameObject Held(SinbinderPlayer hero)
        {
            var arms = hero.GetComponentInChildren<Armament>(true);
            if (arms != null) return arms.Drawn ? arms.InHand : null;
            foreach (var t in hero.GetComponentsInChildren<Transform>(true))
                if (t.name == "SinStaff") return t.gameObject;
            return null;
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            var view = cam != null ? cam.GetComponent<RTS_Camera>() : null;
            var hero = SinbinderPlayer.Instance;
            var held = view != null && hero != null && !hero.IsDead ? Held(hero) : null;
            if (held != _source) Rebuild(held, cam);

            bool want = held != null && _holder != null && view.FirstPersonNow && view.Lying < 0.01f
                     && !DreamTutorialDirector.InputLocked && RecordHands.Empty && Crypt.CryptHands.Empty
                     && !TalkShot();
            if (!want)
            {
                if (_holder != null && _holder.gameObject.activeSelf) Show(false);
                _shown = 0f;
                _swingT = -1f;
                if (cam != null) _lastEye = cam.transform.position;
                return;
            }
            if (!_holder.gameObject.activeSelf) Show(true);

            float dt = Time.deltaTime;
            _shown = Mathf.MoveTowards(_shown, 1f, dt * 4f);
            var c = cam.transform;

            // Поза: покой, блок или взмах (замах — удар — назад).
            var combat = hero.GetComponent<FirstPersonCombat>();
            bool block = combat != null && combat.Blocking;
            Vector3 at = block ? BlockAt : IdleAt, dir = block ? BlockDir : IdleDir;
            if (_swingT >= 0f)
            {
                _swingT += dt / SwingSeconds;
                float t = _swingT;
                if (t < 0.15f) Pose(IdleAt, IdleDir, WindAt, WindDir, Smooth(t / 0.15f), out at, out dir);
                else if (t < 0.4f) Pose(WindAt, WindDir, CutAt, CutDir, Smooth((t - 0.15f) / 0.25f), out at, out dir);
                else if (t < 1f) Pose(CutAt, CutDir, IdleAt, IdleDir, Smooth((t - 0.4f) / 0.6f), out at, out dir);
                else _swingT = -1f;
            }

            // Шаг: оружие покачивается, пока идёт тело.
            var moved = c.position - _lastEye;
            moved.y = 0f;
            _lastEye = c.position;
            float speed = dt > 0f ? Mathf.Min(moved.magnitude / dt, 6f) : 0f;
            _phase += speed * dt * 2.4f;
            float sway = Mathf.Clamp01(speed / 3f);
            at += new Vector3(Mathf.Sin(_phase) * 0.012f, -Mathf.Abs(Mathf.Cos(_phase)) * 0.016f, 0f) * sway;
            at.y -= (1f - Smooth(_shown)) * 0.35f;           // входит снизу

            var grip = c.TransformPoint(at);
            var up = c.TransformDirection(dir.normalized);
            var face = Vector3.ProjectOnPlane(c.forward, up);
            if (face.sqrMagnitude < 1e-4f) face = Vector3.ProjectOnPlane(-c.up, up);
            _holder.SetPositionAndRotation(grip, Quaternion.LookRotation(face, up));

            // Рукав — от запястья к локтю за краем кадра.
            var elbow = c.TransformPoint(Elbow + new Vector3(0f, -(1f - Smooth(_shown)) * 0.35f, 0f));
            if (_arm != null)
            {
                // Кисть держит вещь, как в модели (она дочь рукояти); предплечье — к локтю.
                _arm.Reach(_arm.Hand.position, elbow);
                return;
            }
            var wrist = grip - up * 0.05f;
            var arm = elbow - wrist;
            _sleeve.SetPositionAndRotation((wrist + elbow) * 0.5f, Quaternion.FromToRotation(Vector3.up, arm));
            _sleeve.localScale = new Vector3(0.11f, arm.magnitude * 0.5f, 0.11f);
            _fist.SetPositionAndRotation(grip, _holder.rotation);
        }

        private static bool TalkShot()
        {
            var talk = Dialogue.DialogueCameraController.Instance;
            return talk != null && (talk.InDialogue || talk.Homebound);
        }

        private static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        private static void Pose(Vector3 a0, Vector3 d0, Vector3 a1, Vector3 d1, float t, out Vector3 at, out Vector3 dir)
        {
            at = Vector3.Lerp(a0, a1, t);
            dir = Vector3.Slerp(d0.normalized, d1.normalized, t);
        }

        private void Show(bool on)
        {
            foreach (var go in _parts) if (go != null) go.SetActive(on);
        }

        private void Clear()
        {
            foreach (var go in _parts) if (go != null) Destroy(go);
            _parts.Clear();
            if (_arm != null) _arm.Destroy();
            _arm = null;
            _holder = _fist = _sleeve = null;
        }

        /// <summary>
        /// Копия вещи: те же сетки и материалы, без коллайдеров и скриптов (луч удара идёт из
        /// камеры — копия перед глазами его бы ловила). Рукоять — там, где вещь держит кость:
        /// её точка на длинной оси вещи; остриё — дальний конец.
        /// </summary>
        private void Rebuild(GameObject source, Camera cam)
        {
            Clear();
            _source = source;
            if (source == null) return;

            var meshes = new List<(Mesh mesh, Material[] mats, Transform at)>();
            foreach (var r in source.GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh = null;
                if (r is MeshRenderer) { var f = r.GetComponent<MeshFilter>(); if (f != null) mesh = f.sharedMesh; }
                else if (r is SkinnedMeshRenderer s) mesh = s.sharedMesh;
                if (mesh != null) meshes.Add((mesh, r.sharedMaterials, r.transform));
            }
            if (meshes.Count == 0) return;

            // Самая большая сетка задаёт оси: длинная — клинок (древко), вторая — ширина.
            int main = 0; float biggest = -1f;
            for (int i = 0; i < meshes.Count; i++)
            {
                var size = Vector3.Scale(meshes[i].mesh.bounds.size, meshes[i].at.lossyScale);
                if (size.sqrMagnitude > biggest) { biggest = size.sqrMagnitude; main = i; }
            }
            var m = meshes[main];
            var sz = Vector3.Scale(m.mesh.bounds.size, m.at.lossyScale);
            var axes = new[] { (sz.x, m.at.right), (sz.y, m.at.up), (sz.z, m.at.forward) };
            System.Array.Sort(axes, (p, q) => q.Item1.CompareTo(p.Item1));
            var along = axes[0].Item2.normalized;
            var width = axes[1].Item2.normalized;
            var centre = m.at.TransformPoint(m.mesh.bounds.center);
            var bone = source.transform.parent != null ? source.transform.parent.position : source.transform.position;
            var grip = centre + along * Vector3.Dot(bone - centre, along);
            if (Vector3.Dot(centre - grip, along) < 0f) along = -along;     // остриё — дальний от руки конец

            _holder = new GameObject("Рукоять").transform;
            _holder.SetParent(transform, false);
            _holder.SetPositionAndRotation(grip, Quaternion.LookRotation(Vector3.Cross(width, along), along));
            _parts.Add(_holder.gameObject);
            foreach (var (mesh, mats, at) in meshes)
            {
                var go = new GameObject(at.name);
                go.transform.SetPositionAndRotation(at.position, at.rotation);
                go.transform.localScale = at.lossyScale;
                go.transform.SetParent(_holder, true);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }

            // Кисть — его собственная (автор, 16.11: «почему не использовать руку самого Греховода»):
            // вырезана из модели в нынешней позе и держит вещь, как в модели. Посох у него в левой —
            // глазами вещь справа, и кисть отражается в правую: большой палец там, где надо.
            var hero = SinbinderPlayer.Instance;
            bool right = true;
            for (var p = source.transform.parent; p != null; p = p.parent)
            {
                if (p.name == "LeftHand") { right = false; break; }
                if (p.name == "RightHand") break;
            }
            _arm = HeroArm.Cut(hero, right, transform);
            if (_arm != null)
            {
                _arm.Hand.SetParent(_holder, true);
                if (!right)
                {
                    var mirror = new GameObject("Зеркало").transform;
                    mirror.SetParent(_holder, false);
                    mirror.localScale = new Vector3(-1f, 1f, 1f);
                    var kids = new List<Transform>();
                    foreach (Transform k in _holder) if (k != mirror) kids.Add(k);
                    foreach (var k in kids) k.SetParent(mirror, false);
                }
                _parts.Add(_arm.Forearm.gameObject);
                var lamp = new GameObject("Свет у руки").AddComponent<Light>();
                lamp.type = LightType.Point;
                lamp.range = 0.9f;
                lamp.intensity = 0.12f;
                lamp.color = new Color(1f, 0.82f, 0.62f);
                lamp.shadows = LightShadows.None;
                lamp.transform.SetParent(_holder, false);
                lamp.transform.localPosition = new Vector3(-0.15f, 0.12f, -0.2f);
                Debug.Log("[РУКИ] кисть Греховода держит " + source.name + (right ? "" : " (левая, отражена)"));
                _lastEye = cam != null ? cam.transform.position : Vector3.zero;
                Show(false);
                return;
            }

            // Запас: кулак и рукав — кость в цвет руки, рукав — сукно Греховода, если оно есть.
            Material cloth = null;
            if (hero != null)
                foreach (var r in hero.GetComponentsInChildren<Renderer>(true))
                    if (r.name.StartsWith("SinGarment") || r.name.StartsWith("SinSleeveRight")
                        || r.transform.parent != null && r.transform.parent.name.StartsWith("SinSleeveRight"))
                    { cloth = r.sharedMaterial; break; }
            if (cloth == null) cloth = Lit(new Color(0.42f, 0.05f, 0.06f));

            _fist = Part(PrimitiveType.Sphere, "Кулак", Lit(new Color(0.60f, 0.56f, 0.48f)));
            _fist.SetParent(transform, false);
            var knuckles = _fist.GetChild(0);
            knuckles.localScale = new Vector3(0.085f, 0.11f, 0.095f);
            _sleeve = Part(PrimitiveType.Cylinder, "Рукав", cloth);
            _sleeve.GetChild(0).localScale = Vector3.one;

            var glow = new GameObject("Свет у руки").AddComponent<Light>();
            glow.type = LightType.Point;
            // Слабо: у самого кулака свет в 0,5 выбеливал его в белое яйцо (кадр w0811g).
            glow.range = 0.9f;
            glow.intensity = 0.12f;
            glow.color = new Color(1f, 0.82f, 0.62f);
            glow.shadows = LightShadows.None;
            glow.transform.SetParent(_fist, false);
            glow.transform.localPosition = new Vector3(-0.15f, 0.12f, -0.2f);
            _lastEye = cam != null ? cam.transform.position : Vector3.zero;
            Show(false);
        }

        /// <summary>Примитив без коллайдера, в обёртке: обёртку двигает поза, примитив задаёт форму.</summary>
        private Transform Part(PrimitiveType type, string name, Material mat)
        {
            var root = new GameObject(name).transform;
            root.SetParent(transform, false);
            var shape = GameObject.CreatePrimitive(type);
            DestroyImmediate(shape.GetComponent<Collider>());
            shape.transform.SetParent(root, false);
            var r = shape.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            _parts.Add(root.gameObject);
            return root;
        }

        private static Material Lit(Color colour)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var m = new Material(shader) { color = colour };
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.25f);
            return m;
        }
    }
}
