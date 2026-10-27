// Assets/Scripts/Gameplay/RecordShelf.cs
// Перевод: текст через Loc
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Полка под пластинки в шатре и в склепе (автор, 27.10). На ней стоят ребром все
    /// собранные (<see cref="Records"/>), кроме той, что в руках, и той, что играет: коллекцию
    /// видно глазами, и она растёт. F — взять следующую в руки; с пластинкой в руках F —
    /// вернуть её и взять следующую.
    /// </summary>
    public class RecordShelf : MonoBehaviour
    {
        private const float Reach = 1.8f;
        private static readonly List<RecordShelf> Live = new();
        private readonly List<GameObject> _discs = new();
        private string _shownKey;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => Live.Clear();

        void OnEnable() => Live.Add(this);
        void OnDisable() => Live.Remove(this);

        public static RecordShelf Nearest()
        {
            if (!SinbinderPlayer.Exists) return null;
            foreach (var s in Live)
                if (s != null && CampFocus.GroundDistance(SinbinderPlayer.Where, s.transform.position) <= Reach)
                    return s;
            return null;
        }

        /// <summary>Что стоит на полке сейчас: собранные, кроме руки и граммофона.</summary>
        public static List<Records.Record> OnShelf()
        {
            var list = new List<Records.Record>();
            foreach (var r in Records.Owned())
                if (r != RecordHands.Holding && !Gramophone.IsPlaying(r)) list.Add(r);
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

        public void Use()
        {
            var on = OnShelf();
            var back = RecordHands.Drop();
            if (on.Count == 0) return;

            // Следующая после той, что вернули; не было в руках — первая.
            int i = back != null ? (on.FindIndex(r => r.Id == back.Id) + 1) % on.Count : 0;
            if (back != null && on.Count > 1 && on[i].Id == back.Id) i = (i + 1) % on.Count;
            var take = on[i];
            RecordHands.Take(take);
            Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(
                Loc.F("В руках: {0} — «{1}».", Records.MetalWord(take), Loc.T(take.Title)));
        }

        void Update()
        {
            // Полка показывает то, что на ней: перерисовать, когда состав сменился.
            var on = OnShelf();
            var key = string.Join(",", on.ConvertAll(r => r.Id));
            if (key == _shownKey) return;
            _shownKey = key;

            foreach (var d in _discs) if (d != null) Destroy(d);
            _discs.Clear();

            var prefab = Resources.Load<GameObject>("Props/Record");
            if (prefab == null) return;
            for (int k = 0; k < on.Count; k++)
            {
                // Ребром, в ряд вдоль полки, с лёгким наклоном — как стоят пластинки на полке.
                var holder = new GameObject("Пластинка на полке");
                holder.transform.SetParent(transform, false);
                holder.transform.localPosition = new Vector3(-0.30f + k * 0.055f, 0.02f, 0f);
                holder.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * Quaternion.Euler(-82f, 0f, 0f);
                var disc = Instantiate(prefab, holder.transform, false);
                disc.transform.localScale = prefab.transform.localScale;
                foreach (var c in disc.GetComponentsInChildren<Collider>()) Destroy(c);
                Gramophone.Tint(disc, on[k]);
                _discs.Add(holder);
            }
        }
    }
}
