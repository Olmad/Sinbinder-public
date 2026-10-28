// Assets/Scripts/Gameplay/HiddenRecord.cs
// Перевод: текст через Loc
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Пластинка на земле. Скрытая (автор, 27.10: «лежит рядом с граммофоном и открывает
    /// счёт скрытых пластинок»): F — поднять, пластинка в коллекции (<see cref="Records"/>),
    /// предмет исчезает; уже собранная в прошлой игре — не лежит. Положенная (автор, 27.10:
    /// «нельзя убрать пластинку из рук»): Греховод опустил её из рук на землю
    /// (<see cref="RecordHands.PutDown"/>); F — снова в руки. Ушёл из сцены, а она лежит —
    /// она на полке: коллекция её помнит.
    /// </summary>
    public class HiddenRecord : MonoBehaviour
    {
        [SerializeField] private string _id = "Moonlight";

        private const float Reach = 1.6f;
        private static readonly List<HiddenRecord> Live = new();

        /// <summary>Положена из рук, а не спрятана: поднять — не находка, а просто взять.</summary>
        private bool _put;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => Live.Clear();

        public void Configure(string id) => _id = id;

        /// <summary>Положить пластинку из коллекции на землю: подбирается тем же F.</summary>
        public static HiddenRecord Put(Records.Record record, Vector3 at, float yaw)
        {
            var prefab = Resources.Load<GameObject>("Props/Record");
            if (prefab == null || record == null) return null;
            var go = Instantiate(prefab, at, Quaternion.Euler(0f, yaw, 0f) * prefab.transform.rotation);
            go.name = "Пластинка на земле";
            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
            var lying = go.AddComponent<HiddenRecord>();
            lying._id = record.Id;
            lying._put = true;
            return lying;
        }

        /// <summary>Лежит ли эта пластинка из коллекции на земле — тогда её нет на полке.</summary>
        public static bool Lying(Records.Record record)
        {
            if (record == null) return false;
            foreach (var r in Live)
                if (r != null && r._put && r._id == record.Id) return true;
            return false;
        }

        void Start()
        {
            var record = Records.ById(_id);
            if (!_put && Records.Has(record)) { Destroy(gameObject); return; }

            // Картинка в центре — та же, что ляжет на диск граммофона (props.py, record).
            Gramophone.Label(gameObject, new Vector3(0.04f, 0f, 0.0101f), 0.045f, Gramophone.LabelOf(record));
            Gramophone.Tint(gameObject, record);
        }

        void OnEnable() => Live.Add(this);
        void OnDisable() => Live.Remove(this);

        public static HiddenRecord Nearest()
        {
            foreach (var r in InReach()) return r;
            return null;
        }

        /// <summary>Все пластинки на земле, до которых достаёт рука.</summary>
        public static IEnumerable<HiddenRecord> InReach()
        {
            if (!SinbinderPlayer.Exists) yield break;
            foreach (var r in Live)
                if (r != null && CampFocus.GroundDistance(SinbinderPlayer.Where, r.transform.position) <= Reach)
                    yield return r;
        }

        public string Prompt => Loc.T("F — поднять пластинку");

        public void Take()
        {
            var record = Records.ById(_id);
            if (record == null) return;
            if (_put)
            {
                Destroy(gameObject);
                Live.Remove(this);           // в руки — уже не на земле, даже до конца кадра
                RecordHands.Take(record);
                return;
            }
            Records.Open(record);
            RecordHands.Take(record);     // в руки: ставят её сами (автор, 27.10)
            var (found, total) = Records.HiddenCount();
            Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(
                Loc.F("{0}: «{1}». Скрытых найдено: {2} из {3}. Поставьте её на граммофон.",
                      Records.MetalWord(record), Loc.T(record.Title), found, total));
            Destroy(gameObject);
        }
    }
}
