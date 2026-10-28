// Assets/Scripts/Gameplay/CryptDoor.cs
// Перевод: текст через Loc
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Дверь склепа «Перетягивания» (автор, 27.10: «у склепа не хватает стен и возможности
    /// открывать дверь и пола»). Створка на петле: F, глядя на неё (UI.GearPanel, <see cref="Gaze"/>), —
    /// открыть или закрыть; поворачивается за полсекунды. Закрытая держит путь: на створке
    /// препятствие навмеша (вырезает проход), открытая — отходит к стене внутрь.
    ///
    /// Прочность — у створки (<see cref="Sturdy"/>): её видно при наведении, ломать будут шаги 3–4.
    /// Разрушена — проход открыт навсегда.
    /// </summary>
    public class CryptDoor : MonoBehaviour
    {
        /// <summary>На сколько градусов распахивается внутрь.</summary>
        [SerializeField] private float _swing = 105f;

        private const float Reach = 2.8f;
        private const float Seconds = 0.5f;

        private static readonly List<CryptDoor> Live = new();

        private Transform _leaf;
        private Sturdy _sturdy;
        private bool _open;
        private float _angle;

        public bool Open => _open || (_sturdy != null && _sturdy.Broken);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => Live.Clear();

        void Awake()
        {
            _leaf = transform.childCount > 0 ? transform.GetChild(0) : transform;
            _sturdy = GetComponentInChildren<Sturdy>();
        }

        void OnEnable() => Live.Add(this);
        void OnDisable() => Live.Remove(this);

        /// <summary>Двери, до которых достаёт рука Греховода.</summary>
        public static IEnumerable<CryptDoor> InReach()
        {
            if (!SinbinderPlayer.Exists) yield break;
            foreach (var d in Live)
                if (d != null && d._leaf != null && !(d._sturdy != null && d._sturdy.Broken)
                    && CampFocus.GroundDistance(SinbinderPlayer.Where, d._leaf.position) <= Reach)
                    yield return d;
        }

        /// <summary>Створка — на неё смотрят, чтобы открыть.</summary>
        public Transform Leaf => _leaf;

        public string Prompt => _open ? Loc.T("F — закрыть дверь склепа") : Loc.T("F — открыть дверь склепа");

        public void Toggle()
        {
            _open = !_open;
            Debug.Log($"[СКЛЕП] дверь {(_open ? "открыта" : "закрыта")}: {name}");
        }

        void Update()
        {
            float want = Open ? _swing : 0f;
            if (Mathf.Approximately(_angle, want)) return;
            _angle = Mathf.MoveTowards(_angle, want, Mathf.Abs(_swing) / Seconds * Time.deltaTime);
            transform.localRotation = Quaternion.Euler(0f, _angle, 0f);
        }
    }
}
