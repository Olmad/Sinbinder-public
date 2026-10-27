// Assets/Scripts/Gameplay/HiddenRecord.cs
// Перевод: текст через Loc
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Скрытая пластинка в мире (автор, 27.10: «лежит рядом с граммофоном и открывает
    /// счёт скрытых пластинок»). F — поднять: пластинка в коллекции (<see cref="Records"/>),
    /// предмет исчезает. Уже собранная в прошлой игре — не лежит.
    /// </summary>
    public class HiddenRecord : MonoBehaviour
    {
        [SerializeField] private string _id = "Moonlight";

        private const float Reach = 1.6f;
        private static readonly List<HiddenRecord> Live = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => Live.Clear();

        public void Configure(string id) => _id = id;

        void Start()
        {
            var record = Records.ById(_id);
            if (Records.Has(record)) { Destroy(gameObject); return; }

            // Картинка в центре — та же, что ляжет на диск граммофона (props.py, record).
            Gramophone.Label(gameObject, new Vector3(0.04f, 0f, 0.0101f), 0.045f, Gramophone.LabelOf(record));
            Gramophone.Tint(gameObject, record);
        }

        void OnEnable() => Live.Add(this);
        void OnDisable() => Live.Remove(this);

        public static HiddenRecord Nearest()
        {
            if (!SinbinderPlayer.Exists) return null;
            foreach (var r in Live)
                if (r != null && CampFocus.GroundDistance(SinbinderPlayer.Where, r.transform.position) <= Reach)
                    return r;
            return null;
        }

        public string Prompt => Loc.T("F — поднять пластинку");

        public void Take()
        {
            var record = Records.ById(_id);
            if (record == null) return;
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
