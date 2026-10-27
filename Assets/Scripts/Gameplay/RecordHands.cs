// Assets/Scripts/Gameplay/RecordHands.cs
// Перевод: текст через Loc
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Пластинка в руках Греховода (автор, 27.10: «пускай носятся в руках, а в лагере и склепе
    /// будет полка под диски. Новая мелодия не ставится сама, а только руками»).
    ///
    /// Одна за раз. Берут с полки (<see cref="RecordShelf"/>), с пола (<see cref="HiddenRecord"/>)
    /// или с граммофона; кладут на граммофон (<see cref="Gramophone"/>) или обратно на полку.
    /// Ушёл из сцены с пластинкой — она на полке: коллекция её не теряет (<see cref="Records"/>).
    ///
    /// Видно в руке: сверху — у правой руки Греховода, глазами — внизу справа в кадре.
    /// Объект не под телом героя: тело прячется от первого лица целиком (RTS_Camera.ShowHero),
    /// а пластинку в руке видеть надо.
    /// </summary>
    public class RecordHands : MonoBehaviour
    {
        public static Records.Record Holding { get; private set; }

        private static RecordHands _shown;
        private Transform _hand;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() { Holding = null; _shown = null; }

        public static bool Empty => Holding == null;

        /// <summary>Взять в руки (прежняя — на полку). Показывает диск у руки.</summary>
        public static void Take(Records.Record r)
        {
            Drop();
            if (r == null) return;
            Holding = r;

            var prefab = Resources.Load<GameObject>("Props/Record");
            if (prefab == null) return;
            var go = new GameObject("Пластинка в руках");
            var disc = Object.Instantiate(prefab, go.transform, false);
            disc.transform.localScale = prefab.transform.localScale * 0.9f;
            foreach (var c in disc.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            foreach (var rend in disc.GetComponentsInChildren<Renderer>())
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Gramophone.Tint(disc, r);
            Gramophone.Label(disc, new Vector3(0.04f, 0f, 0.0101f), 0.045f, Gramophone.LabelOf(r));
            _shown = go.AddComponent<RecordHands>();
        }

        /// <summary>Отдать из рук (на граммофон или на полку — решает зовущий). Возвращает, что было.</summary>
        public static Records.Record Drop()
        {
            var was = Holding;
            Holding = null;
            if (_shown != null) Object.Destroy(_shown.gameObject);
            _shown = null;
            return was;
        }

        void OnDestroy()
        {
            // Сцена закрылась с пластинкой в руках — руки пусты, а пластинка в коллекции
            // и на полке: Records её помнит, полка показывает всё, что не на граммофоне.
            if (_shown == this) { _shown = null; Holding = null; }
        }

        void LateUpdate()
        {
            if (!SinbinderPlayer.Exists) return;
            var cam = Camera.main;
            var view = cam != null ? cam.GetComponent<RTS_Camera>() : null;

            if (view != null && view.FirstPersonNow)
            {
                // Глазами: внизу справа, чуть наклонена к взгляду — как вещь в руке.
                var c = cam.transform;
                transform.SetPositionAndRotation(c.position + c.forward * 0.42f + c.right * 0.20f - c.up * 0.22f,
                                                 c.rotation * Quaternion.Euler(-70f, 0f, 15f));
                return;
            }

            if (_hand == null) _hand = HandOf(SinbinderPlayer.Instance.transform);
            var hero = SinbinderPlayer.Instance.transform;
            var at = _hand != null ? _hand.position : hero.position + Vector3.up * 1.0f + hero.right * 0.3f;
            transform.SetPositionAndRotation(at + hero.forward * 0.12f, hero.rotation * Quaternion.Euler(-80f, 0f, 0f));
        }

        private static Transform HandOf(Transform hero)
        {
            foreach (var t in hero.GetComponentsInChildren<Transform>(true))
                if (t.name == "RightHand") return t;
            return null;
        }
    }
}
