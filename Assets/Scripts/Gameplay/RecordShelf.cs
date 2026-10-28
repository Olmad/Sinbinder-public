// Assets/Scripts/Gameplay/RecordShelf.cs
// Перевод: текст через Loc
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Полка под пластинки в шатре и в склепе (автор, 27.10). На ней стоят ребром все
    /// собранные (<see cref="Records"/>), кроме той, что в руках, той, что играет, и той, что
    /// лежит на земле: коллекцию видно глазами, и она растёт.
    ///
    /// F — той пластинке, на которую смотрят (автор, 27.10: «менять пластинки на полке можно
    /// только первую и в руках»): с пустыми руками — взять её, с пластинкой в руках — поменять.
    /// Смотрят на пустой край полки с пластинкой в руках — поставить её на место
    /// («нельзя убрать пластинку из рук»).
    /// </summary>
    public class RecordShelf : MonoBehaviour
    {
        private const float Reach = 1.8f;

        /// <summary>Шаг между пластинками на полке, м; первая — с левого края.</summary>
        private const float Step = 0.055f;
        private const float FirstX = -0.30f;

        /// <summary>Середина пластинки над полкой: стоят ребром, диаметр 30 см.</summary>
        private const float DiscMiddle = 0.15f;

        private static readonly List<RecordShelf> Live = new();
        private readonly List<GameObject> _discs = new();
        private readonly List<Records.Record> _shown = new();
        private string _shownKey;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => Live.Clear();

        void OnEnable() => Live.Add(this);
        void OnDisable() => Live.Remove(this);

        public static RecordShelf Nearest()
        {
            foreach (var s in InReach()) return s;
            return null;
        }

        /// <summary>Полки, до которых достаёт рука.</summary>
        public static IEnumerable<RecordShelf> InReach()
        {
            if (!SinbinderPlayer.Exists) yield break;
            foreach (var s in Live)
                if (s != null && CampFocus.GroundDistance(SinbinderPlayer.Where, s.transform.position) <= Reach)
                    yield return s;
        }

        /// <summary>Что стоит на полке сейчас: собранные, кроме руки, граммофона и земли.</summary>
        public static List<Records.Record> OnShelf()
        {
            var list = new List<Records.Record>();
            foreach (var r in Records.Owned())
                if (r != RecordHands.Holding && !Gramophone.IsPlaying(r) && !HiddenRecord.Lying(r)) list.Add(r);
            return list;
        }

        public string Prompt
        {
            get
            {
                var on = OnShelf();
                if (!RecordHands.Empty) return Loc.T("F — вернуть пластинку на полку и взять другую");
                return on.Count > 0 ? Loc.T("F — взять пластинку с полки") : Loc.T("Полка пуста");
            }
        }

        /// <summary>
        /// Что можно сделать у полки взглядом (UI.GearPanel): по строке на каждую пластинку —
        /// взять её или поменять на ту, что в руках, — и пустой край полки, если в руках есть что вернуть.
        /// </summary>
        public void Offer(System.Action<float, string, System.Action> offer)
        {
            Redraw();
            var up = transform.up;
            for (int k = 0; k < _shown.Count && k < _discs.Count; k++)
            {
                var r = _shown[k];
                if (_discs[k] == null) continue;
                var at = transform.TransformPoint(new Vector3(FirstX + k * Step, 0f, 0f)) + up * DiscMiddle;
                string title = Loc.T(r.Title);
                offer(Gaze.Miss(at, 0.06f),
                      RecordHands.Empty ? Loc.F("F — взять «{0}»", title) : Loc.F("F — поменять на «{0}»", title),
                      () => Take(r));
            }

            if (RecordHands.Empty) return;
            var gap = transform.TransformPoint(new Vector3(FirstX + _shown.Count * Step + 0.04f, 0f, 0f)) + up * DiscMiddle;
            offer(Gaze.Miss(gap, 0.12f), Loc.T("F — поставить пластинку на полку"), PutBack);
        }

        /// <summary>Взять с полки эту; в руках была другая — она встаёт на полку.</summary>
        public void Take(Records.Record take)
        {
            if (take == null || !OnShelf().Exists(r => r.Id == take.Id)) return;
            RecordHands.Take(take);
            Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(
                Loc.F("В руках: {0} — «{1}».", Records.MetalWord(take), Loc.T(take.Title)));
        }

        /// <summary>Поставить пластинку из рук на полку — руки пусты.</summary>
        public void PutBack()
        {
            var back = RecordHands.Drop();
            if (back != null) Redraw();
        }

        /// <summary>
        /// Без взгляда (прогон): вернуть ту, что в руках, и взять следующую после неё;
        /// не было в руках — первую.
        /// </summary>
        public void Use()
        {
            var on = OnShelf();
            var back = RecordHands.Drop();
            if (on.Count == 0) return;

            int i = back != null ? (on.FindIndex(r => r.Id == back.Id) + 1) % on.Count : 0;
            if (back != null && on.Count > 1 && on[i].Id == back.Id) i = (i + 1) % on.Count;
            Take(on[i]);
        }

        void Update() => Redraw();

        /// <summary>Полка показывает то, что на ней: перерисовать, когда состав сменился.</summary>
        private void Redraw()
        {
            var on = OnShelf();
            var key = string.Join(",", on.ConvertAll(r => r.Id));
            if (key == _shownKey) return;
            _shownKey = key;

            foreach (var d in _discs) if (d != null) Destroy(d);
            _discs.Clear();
            _shown.Clear();

            var prefab = Resources.Load<GameObject>("Props/Record");
            if (prefab == null) return;
            for (int k = 0; k < on.Count; k++)
            {
                // Ребром, в ряд вдоль полки, с лёгким наклоном — как стоят пластинки на полке.
                var holder = new GameObject("Пластинка на полке");
                holder.transform.SetParent(transform, false);
                holder.transform.localPosition = new Vector3(FirstX + k * Step, 0.02f, 0f);
                holder.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * Quaternion.Euler(-82f, 0f, 0f);
                var disc = Instantiate(prefab, holder.transform, false);
                disc.transform.localScale = prefab.transform.localScale;
                foreach (var c in disc.GetComponentsInChildren<Collider>()) Destroy(c);
                Gramophone.Tint(disc, on[k]);
                _discs.Add(holder);
                _shown.Add(on[k]);
            }
        }
    }
}
