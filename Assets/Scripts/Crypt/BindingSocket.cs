// Assets/Scripts/Crypt/BindingSocket.cs
// Перевод: текст через Loc
using UnityEngine;

using Sinbinder.Core;
namespace Sinbinder.Crypt
{
    /// <summary>
    /// Гнездо устройства: сюда кладут душу или тело, отсюда забирают
    /// обратно. Само ничего не решает — только передаёт устройству.
    /// </summary>
    public class BindingSocket : CryptInteractable
    {
        public enum Slot { Soul = 0, Shell = 1 }

        [SerializeField] private Slot _slot = Slot.Soul;
        [SerializeField] private BindingDevice _device;

        public override string Label
            => _slot == Slot.Soul ? Loc.T("Гнездо души") : Loc.T("Ложе тела");

        protected override string Hint
        {
            get
            {
                bool filled = _slot == Slot.Soul
                    ? _device != null && _device.HasSoul
                    : _device != null && _device.HasShell;

                if (filled) return Loc.F("{0} занято — забрать на {1}.", Label, _key);

                // 47-TEXTS §4, пп. 3–4 (автор: «да»): что сюда кладут и откуда
                // взять — без загадки.
                if (CryptHands.Empty)
                    return _slot == Slot.Soul
                        ? Loc.T("Гнездо души пусто: сюда кладут душу. Возьмите банку с полки и подойдите.")
                        : Loc.T("Ложе тела пусто: сюда кладут тело. Возьмите его со стойки с телами и подойдите.");
                return Loc.F("{0} — положить на {1}.", Label, _key);
            }
        }

        void Start()
        {
            if (_device == null) _device = Object.FindFirstObjectByType<BindingDevice>();

            if (_device == null)
                Debug.LogError("[СКЛЕП] Гнездо без устройства: класть некуда.");
        }

        protected override void Use()
        {
            if (_device == null) return;

            bool put = _slot == Slot.Soul ? _device.PutSoul() : _device.PutShell();
            if (put) { Announce(); return; }

            bool took = _slot == Slot.Soul ? _device.TakeSoulBack() : _device.TakeShellBack();
            if (took) { Say(Loc.F("{0}: забрано обратно.", Label)); return; }

            // Ни положить, ни забрать — значит в руках не то. Сказать
            // об этом надо словами: молчащий предмет читается как
            // сломанный.
            Say(CryptHands.Empty
                ? Loc.F("{0}: в руках пусто.", Label)
                : Loc.F("{0}: сюда это не кладут. В руках {1}.", Label, CryptHands.What));
        }

        /// <summary>Что вышло и что теперь можно.</summary>
        private void Announce()
        {
            Say(Loc.F("{0}: положено.", Label));

            string no = _device.NotReady;

            if (!string.IsNullOrEmpty(no)) { Say(no); return; }

            string foretell = _device.Foretell();
            if (!string.IsNullOrEmpty(foretell)) Say(foretell);
        }

        // ---------------------------------------------------------- вид

        private GameObject _shown;
        private string _shownKey;

        /// <summary>
        /// Положенное видно на устройстве (автор, 26.10: «при постановке души и тела
        /// на алтарь — они не отображаются. Непонятно»): в гнезде — банка души и горит
        /// её грехами, как на полке; на ложе — тело той оболочки, навзничь.
        /// </summary>
        void LateUpdate()
        {
            if (_device == null) return;

            string key = _slot == Slot.Soul
                ? (_device.HasSoul ? "soul:" + _device.SoulInside.Name : "")
                : (_device.HasShell ? "shell:" + _device.ShellInside : "");
            if (key == _shownKey) return;
            _shownKey = key;

            if (_shown != null) Destroy(_shown);
            _shown = null;
            if (key == "") return;

            _shown = _slot == Slot.Soul ? JarOnTop() : BodyOnTop();
        }

        /// <summary>Верх того, на чём лежит: по видимой части гнезда.</summary>
        private Vector3 Top()
        {
            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return transform.position + Vector3.up * 0.9f;
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return new Vector3(b.center.x, b.max.y, b.center.z);
        }

        private GameObject JarOnTop()
        {
            var model = Resources.Load<GameObject>("Props/SoulJarFull");
            if (model == null) return null;

            var go = new GameObject("Банка в гнезде");
            go.transform.position = Top();
            var body = Instantiate(model, go.transform, false);
            // Корень модели несёт масштаб и поворот импорта — умножаем (как SoulShelf).
            body.transform.localScale = model.transform.localScale * 1.8f;
            go.AddComponent<Gameplay.SoulJarGlow>().Show(_device.SoulInside);
            return go;
        }

        private GameObject BodyOnTop()
        {
            var model = Resources.Load<GameObject>("Bodies/" + _device.ShellInside);
            if (model == null) return null;

            var go = new GameObject("Тело на ложе");
            go.transform.position = Top() + Vector3.up * 0.05f;
            // Навзничь, головой вдоль ложа.
            go.transform.rotation = transform.rotation * Quaternion.Euler(-90f, 0f, 0f);
            var body = Instantiate(model, go.transform, false);
            body.transform.localScale = model.transform.localScale * 1.6f;
            foreach (var a in body.GetComponentsInChildren<Animator>()) a.enabled = false;
            foreach (var c in body.GetComponentsInChildren<Collider>()) Destroy(c);
            return go;
        }

        void OnDestroy()
        {
            if (_shown != null) Destroy(_shown);
        }

        /// <summary>Настроить из сборщика сцены.</summary>
        public void Set(Slot slot, BindingDevice device)
        {
            _slot = slot;
            _device = device;
        }
    }
}
