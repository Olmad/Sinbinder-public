// Assets/Scripts/Gameplay/Gramophone.cs
// Перевод: текст через Loc
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Граммофон в шатре и в склепе (37-DEMO §8). Пластинку ставят руками (автор, 27.10:
    /// «пускай носятся в руках… новая мелодия не ставится сама, а только руками»):
    /// * с пластинкой в руках (<see cref="RecordHands"/>) F — поставить; стояла другая —
    ///   она переходит в руки;
    /// * с пустыми руками F — снять пластинку в руки; пусто и тут — подсказка «на полке».
    ///
    /// Вещь «для души»: ни силы, ни урока. О себе молчит: строка F — только тому, кто
    /// подошёл сам (§8.2, п. 2). Пока играет — тема сцены молчит (<see cref="Audio.Music.Gramophone"/>).
    /// Сцена ставит свою тему (набег, бегство, эпилог) — игла поднимается: событие главнее;
    /// пластинка остаётся на диске. В журнале — название и где эта тема звучала (§8.5).
    /// </summary>
    public class Gramophone : MonoBehaviour
    {
        /// <summary>С какого расстояния слушается F, м.</summary>
        private const float Reach = 2.0f;

        private static readonly List<Gramophone> Live = new();

        private AudioSource _source;
        private Records.Record _on;     // что лежит на диске (играет или игла поднята)

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => Live.Clear();

        void OnEnable()
        {
            Live.Add(this);
            Audio.Music.Switched += OnSceneTheme;
        }

        void OnDisable()
        {
            Live.Remove(this);
            Audio.Music.Switched -= OnSceneTheme;
            if (Sounding) Audio.Music.Gramophone = false;
        }

        /// <summary>Граммофон рядом с Греховодом. Нет — null.</summary>
        public static Gramophone Nearest()
        {
            if (!SinbinderPlayer.Exists) return null;
            foreach (var g in Live)
                if (g != null && CampFocus.GroundDistance(SinbinderPlayer.Where, g.transform.position) <= Reach)
                    return g;
            return null;
        }

        /// <summary>Лежит ли эта пластинка на каком-нибудь граммофоне (полка её тогда не показывает).</summary>
        public static bool IsPlaying(Records.Record r)
        {
            foreach (var g in Live) if (g != null && g._on == r) return true;
            return false;
        }

        private bool Sounding => _source != null && _source.isPlaying;

        public string Prompt
        {
            get
            {
                if (!RecordHands.Empty) return Loc.F("F — поставить «{0}»", Loc.T(RecordHands.Holding.Title));
                if (_on != null) return Loc.T("F — снять пластинку");
                return Loc.T("Пластинки — на полке");
            }
        }

        /// <summary>F у граммофона: поставить из рук, поменять или снять в руки.</summary>
        public void Use()
        {
            if (!RecordHands.Empty)
            {
                var put = RecordHands.Drop();
                var was = _on;
                Play(put);
                if (was != null) RecordHands.Take(was);
                return;
            }

            if (_on == null) return;
            var off = _on;
            Stop();
            _on = null;
            ShowLabel(null);
            RecordHands.Take(off);
        }

        private void Play(Records.Record record)
        {
            var clip = Resources.Load<AudioClip>(record.Clip);
            if (clip == null) return;

            if (_source == null)
            {
                _source = gameObject.AddComponent<AudioSource>();
                _source.playOnAwake = false;
                _source.loop = false;
                _source.spatialBlend = 0.6f;      // слышно, откуда играет, но и в шаге от него
                _source.minDistance = 3f;
                _source.maxDistance = 30f;
                _source.rolloffMode = AudioRolloffMode.Linear;
                _source.dopplerLevel = 0f;
            }

            _source.Stop();
            _source.clip = clip;
            _source.volume = 0.55f * Preferences.MusicScale;
            _source.Play();
            _on = record;
            Audio.Music.Gramophone = true;
            ShowLabel(record);

            Log(Loc.F("«{0}» — {1}.", Loc.T(record.Title), Loc.T(record.Where)));
        }

        private bool _wasSounding;

        void Update()
        {
            // Доиграла — игла поднялась, тема сцены возвращается; пластинка лежит.
            bool now = Sounding;
            if (_wasSounding && !now) Audio.Music.Gramophone = false;
            _wasSounding = now;
        }

        private void OnSceneTheme(Audio.Track track)
        {
            if (!Sounding) return;
            Stop();
            Log(Loc.T("Игла поднялась: не до музыки."));
        }

        private void Stop()
        {
            if (Sounding) Audio.Music.Gramophone = false;
            if (_source != null) _source.Stop();
            _wasSounding = false;
        }

        private static void Log(string line)
            => Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(line);

        // ---------------------------------------------------------- ярлык

        /// <summary>Середина пластинки на диске — в осях модели (props.py, gramophone).</summary>
        private static readonly Vector3 PlatterLabel = new(0f, 0f, 0.1622f);

        private Renderer _label;

        void Start()
        {
            _label = Label(gameObject, PlatterLabel, 0.045f, "Rats_copper");
            ShowLabel(null);    // диск пуст, пока пластинку не поставили руками
        }

        /// <summary>Картинка ярлыка: «Лунная соната» — своя, прочие — герб «Крыс» в металл пластинки.</summary>
        public static string LabelOf(Records.Record r)
        {
            if (r != null && r.Id == "Moonlight") return "Moonlight";
            return Records.MetalOf(r) switch
            {
                Records.Metal.Gold => "Rats_gold",
                Records.Metal.Silver => "Rats_silver",
                _ => "Rats_copper",
            };
        }

        private void ShowLabel(Records.Record r)
        {
            Tint(gameObject, r);
            if (_label == null) return;
            _label.enabled = r != null;
            if (r == null) return;
            var tex = Resources.Load<Texture2D>("Records/Labels/" + LabelOf(r));
            if (tex != null) _label.sharedMaterial.SetTexture("_BaseMap", tex);
        }

        /// <summary>
        /// Покрасить диск пластинки (материал «Shellac» модели) в металл ступени: медь,
        /// серебро, золото — с металлическим отливом, чтобы фонарь в шатре его ловил.
        /// </summary>
        public static void Tint(GameObject prop, Records.Record r)
        {
            // Пусто (r == null) — диск цвета войлока под пластинкой, без блеска: пластинки нет.
            var colour = r == null ? new Color(0.055f, 0.050f, 0.048f) : Records.MetalOf(r) switch
            {
                Records.Metal.Gold => new Color(0.72f, 0.55f, 0.20f),
                Records.Metal.Silver => new Color(0.62f, 0.63f, 0.66f),
                _ => new Color(0.48f, 0.26f, 0.14f),
            };
            foreach (var rend in prop.GetComponentsInChildren<Renderer>())
            {
                var mats = rend.materials;
                bool any = false;
                foreach (var m in mats)
                {
                    if (m == null || !m.name.StartsWith("Shellac")) continue;
                    m.color = colour;
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", colour);
                    if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", r == null ? 0f : 0.85f);
                    if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", r == null ? 0.1f : 0.62f);
                    any = true;
                }
                if (any) rend.materials = mats;
            }
        }

        /// <summary>
        /// Положить картинку ярлыка (автор, 27.10: «картинка в центре, как у крутых дорогих
        /// пластинок») поверх красного кружка модели. Точка — в осях Blender: вершины меша
        /// лежат в них, и <c>TransformPoint</c> меша переводит их в мир при любом повороте.
        /// </summary>
        public static Renderer Label(GameObject prop, Vector3 blenderAt, float radius, string label)
        {
            var mf = prop.GetComponentInChildren<MeshFilter>();
            var tex = Resources.Load<Texture2D>("Records/Labels/" + label);
            if (mf == null || tex == null) return null;

            var t = mf.transform;
            var up = t.TransformDirection(Vector3.forward).normalized;           // Blender Z — вверх
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Ярлык";
            Object.Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(prop.transform, true);
            quad.transform.position = t.TransformPoint(blenderAt) + up * 0.0015f;
            quad.transform.rotation = Quaternion.LookRotation(-up, t.TransformDirection(Vector3.up));
            // Размер в мире — радиус в осях меша, умноженный на масштаб меша; масштаб родителя снимаем.
            float size = radius * 2f * t.lossyScale.x;
            quad.transform.localScale = Vector3.one * (size / Mathf.Max(1e-5f, prop.transform.lossyScale.x));

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Record Label" };
            mat.SetTexture("_BaseMap", tex);
            mat.SetFloat("_AlphaClip", 1f);
            mat.SetFloat("_Cutoff", 0.5f);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.SetFloat("_Smoothness", 0.35f);
            var r = quad.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return r;
        }
    }
}
