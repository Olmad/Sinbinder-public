// Assets/Scripts/Gameplay/DreamBowTarget.cs
using System.Collections;
using UnityEngine;
using Sinbinder.Inventory;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Подвесная учебная мишень. Приказ атаковать идёт через SelectionManager
    /// и настоящую AOS-оценку ObeyCommand; сюда приходит только исполнение.
    /// Меч не достаёт до подвешенной мишени. Лук является реальной вещью,
    /// которую передают воину через SquadGear, и меняет голоса TemptationResolver.
    /// Это учебный выстрел по конструкции, НЕ универсальная система дальнего боя.
    /// </summary>
    [RequireComponent(typeof(Sturdy))]
    public sealed class DreamBowTarget : MonoBehaviour
    {
        public const string BowName = "Лук тщеславия";
        private const float Range = 20f;
        private const float Cadence = 2.2f;
        private Sturdy _sturdy;
        private Rigidbody _body;
        private float _nextShot;
        private bool _falling;

        public bool Broken => _sturdy != null && _sturdy.Broken;

        void Awake()
        {
            _sturdy = GetComponent<Sturdy>();
            _body = GetComponent<Rigidbody>();
            if (_body == null) _body = gameObject.AddComponent<Rigidbody>();
            _body.isKinematic = true;
            _body.useGravity = false;
        }

        public static bool HasBow(Warrior warrior)
        {
            if (warrior == null || warrior.IsDead) return false;
            foreach (var item in warrior.Carried)
                if (item != null && item.Name == BowName && item.Slot == GearSlot.Weapon) return true;
            return false;
        }

        /// <summary>Вызывается только после AOS-исполнения приказа атаковать мишень.</summary>
        public void ExecuteShot(Warrior warrior)
        {
            if (Broken || warrior == null || warrior.IsDead) return;
            if (!HasBow(warrior))
            {
                if (Time.time >= _nextShot)
                {
                    _nextShot = Time.time + Cadence;
                    Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(
                        "Мишень слишком высоко для клинка. Передайте воину лук тщеславия через F — снаряжение.");
                }
                return;
            }
            Vector3 from = warrior.transform.position + Vector3.up * 1.3f;
            Vector3 to = transform.position;
            if (Vector3.Distance(from, to) > Range)
            {
                warrior.GetComponent<UnitMover>()?.CommandMove(new Vector3(-1.7f, 0f, 19.6f));
                return;
            }
            warrior.GetComponent<UnitMover>()?.Stop();
            if (Time.time < _nextShot || _falling) return;
            _nextShot = Time.time + Cadence;
            warrior.GetComponent<WarriorAnimation>()?.Struck();
            StartCoroutine(Arrow(from, to, warrior));
        }

        private IEnumerator Arrow(Vector3 from, Vector3 to, Warrior warrior)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Стрела лука тщеславия";
            Destroy(go.GetComponent<Collider>());
            var r = go.GetComponent<Renderer>();
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader != null) r.sharedMaterial = new Material(shader) { color = new Color(0.54f, 0.34f, 0.15f) };
            go.transform.localScale = new Vector3(0.035f, 0.35f, 0.035f);
            go.transform.rotation = Quaternion.FromToRotation(Vector3.up, to - from);
            for (float t = 0f; t < 0.4f; t += Time.deltaTime)
            {
                if (go == null || this == null || Broken) break;
                go.transform.position = Vector3.Lerp(from, to, Mathf.Clamp01(t / 0.4f));
                yield return null;
            }
            if (go != null) Destroy(go);
            if (this == null || Broken) yield break;
            _sturdy.TakeDamage(5f, warrior);
            if (Broken)
            {
                _falling = true;
                _body.isKinematic = false;
                _body.useGravity = true;
                Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(
                    "Стрела перерезала подвес. Мишень упала. Лук остаётся у воина и влияет на его решения.");
            }
        }
    }
}
