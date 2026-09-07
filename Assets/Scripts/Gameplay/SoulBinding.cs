// Assets/Scripts/Gameplay/SoulBinding.cs
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Связывание: собранная душа входит в тело и встаёт на вашу сторону.
    /// Вторая половина урока сцены 4 (docs/09-PROLOGUE.md §4): «здесь же
    /// обучение жатве душ и связыванию: из троих поднимется, скорее всего,
    /// только зомби».
    ///
    /// Почему «скорее всего только зомби» — не оговорка сценариста, а
    /// следствие. Жатва и связывание разнесены во времени: пока игрок
    /// добежал до второго тела, первая душа уже осыпалась. Что именно
    /// осыпалось, решает <see cref="SoulDecay"/>: сначала тускнеют
    /// спектры, потом уходит память, под конец остаётся одна воля.
    /// Поднятый из такой души и есть зомби — не по названию оболочки,
    /// а по тому, что от него осталось.
    ///
    /// Оболочка в демо одна, и это тоже намеренно: выбор оболочек — сборка
    /// воина из 08-FLOOR §3, работа полной версии. Здесь связывание учит
    /// одному: спеши.
    /// </summary>
    public class SoulBinding : MonoBehaviour
    {
        [SerializeField] private KeyCode _bindKey = KeyCode.R;

        [Tooltip("Во что связывать. В демо одна оболочка: выбор оболочек — "
               + "сборка воина из полной версии.")]
        [SerializeField] private ShellType _shell = ShellType.Zombie;

        [Tooltip("Как далеко от связавшего встаёт поднятый.")]
        [SerializeField] private float _riseOffset = 1.6f;

        [SerializeField] private float _cooldown = 1.5f;

        private float _cooldownTimer;
        private bool _isPlayerUnit;
        private RelationshipSystem _relSystem;

        private static int _lastComplaintFrame = -1;

        void Start()
        {
            var warrior = GetComponent<Warrior>();
            _isPlayerUnit = warrior != null && warrior.Team == Team.Player;
        }

        void Update()
        {
            if (!_isPlayerUnit) return;

            _cooldownTimer -= Time.deltaTime;

            if (Input.GetKeyDown(_bindKey) && _cooldownTimer <= 0f)
                TryBind();
        }

        private void TryBind()
        {
            var souls = SoulManager.Instance;

            if (souls == null)
            {
                Debug.LogWarning("[СВЯЗЫВАНИЕ] SoulManager в сцене нет: "
                               + "связывать нечего и нечем.");
                return;
            }

            if (souls.Harvested.Count == 0)
            {
                // Компонент висит на каждом своём воине, и по нажатию R
                // сюда приходят все разом. Жалуемся один раз за кадр,
                // иначе журнал заполнится одной и той же строкой,
                // повторённой по числу выживших.
                if (_lastComplaintFrame != Time.frameCount)
                {
                    _lastComplaintFrame = Time.frameCount;
                    Log("Ни одной души при себе. Сперва заберите её.");
                }
                return;
            }

            var soul = souls.TakeHarvested();
            if (soul == null) return;

            _cooldownTimer = _cooldown;

            var risen = Raise(soul);
            if (risen == null) return;

            // Говорим не «поднят зомби», а чем он оказался: урок в том,
            // что вышло из промедления, а не в названии оболочки.
            Log($"{risen.DisplayName} поднялся и встал рядом.");

            if (soul.Memory == null)
                Log("Он не помнит, кем был. Слушается — и только.");
        }

        /// <summary>
        /// Поднять воина из души. Собран так же, как своих собирает
        /// лагерный спавнер: иначе поднятый вёл бы себя не как все,
        /// а движок обязан быть один на всех.
        /// </summary>
        private Warrior Raise(SoulData soul)
        {
            _relSystem ??= new RelationshipSystem(AOS.MemoryProcessor.Instance);

            var go = new GameObject(soul.Name);
            go.transform.position = transform.position + transform.right * _riseOffset;
            go.transform.rotation = transform.rotation;

            var warrior = go.AddComponent<Warrior>();
            warrior.Initialize(soul, _shell, _relSystem, false, Team.Player);

            WarriorRig.Attach(go);
            go.AddComponent<SoulHarvester>();
            go.AddComponent<SoulBinding>();

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Тело";
            body.transform.SetParent(go.transform);
            body.transform.localPosition = new Vector3(0f, 0.65f, 0f);
            body.transform.localScale = new Vector3(0.5f, 1.05f, 0.5f);

            // Поднятый посреди боя не проходит через настройку сцены:
            // без этого вызова он остался бы телом без движка решений.
            var setup = Object.FindFirstObjectByType<AOS.AOSSceneSetup>();
            if (setup != null) setup.SetupWarrior(go);
            else Debug.LogWarning("[СВЯЗЫВАНИЕ] AOSSceneSetup в сцене нет: "
                                + "поднятый не будет ничего решать.");

            return warrior;
        }

        private static void Log(string line)
            => Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(line);
    }
}
