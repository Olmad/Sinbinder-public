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
    /// <b>Оболочку теперь выбирает игрок</b> — <see cref="UI.ShellPickerUI"/>.
    /// Четыре тела собраны как ассеты давно, а связывание держало зашитого
    /// зомби: система пользовалась одной своей четвертью. И урок «спеши»
    /// стал механикой, а не словом рассказчика: правило
    /// <see cref="ShellChoice"/> не пускает истлевшую душу в тяжёлое тело,
    /// так что промедление отнимает не качество, а <b>выбор</b>.
    ///
    /// Экрана в сцене может не быть — тогда связываем прежним способом,
    /// в поле <c>_shell</c>. Отсутствие интерфейса не имеет права
    /// отменить механику.
    /// </summary>
    public class SoulBinding : MonoBehaviour
    {
        [SerializeField] private KeyCode _bindKey = KeyCode.R;

        [Tooltip("Во что связывать, когда экрана выбора в сцене нет.")]
        [SerializeField] private ShellType _shell = ShellType.Zombie;

        [Tooltip("Как далеко от связавшего встаёт поднятый.")]
        [SerializeField] private float _riseOffset = 1.6f;

        [SerializeField] private float _cooldown = 1.5f;

        private float _cooldownTimer;
        private bool _isPlayerUnit;
        private RelationshipSystem _relSystem;

        private static int _lastComplaintFrame = -1;

        // Компонент висит на каждом своём воине, и по нажатию R сюда
        // приходят все разом. Связывать должен один: иначе первый откроет
        // экран выбора, а остальные тут же свяжут душу мимо него.
        private static int _lastBindFrame = -1;

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

            if (_lastBindFrame == Time.frameCount) return;
            _lastBindFrame = Time.frameCount;

            var picker = Object.FindFirstObjectByType<UI.ShellPickerUI>();

            // Экран открыт — значит выбор уже идёт. Игра на паузе, но
            // Update крутится и R доходит сюда снова; без этой строки
            // второе нажатие связало бы душу мимо открытого экрана.
            if (picker != null && picker.IsOpen) return;

            var kept = souls.PeekHarvested();
            if (kept.Soul == null) return;

            _cooldownTimer = _cooldown;

            if (picker != null && picker.Open(kept.Soul, kept.Quality, Bind)) return;

            Bind(_shell);
        }

        /// <summary>
        /// Забрать первую собранную и поднять её в выбранном теле.
        /// Душа забирается здесь, а не при открытии экрана: закрытый
        /// без выбора экран не имеет права её потерять.
        /// </summary>
        private void Bind(ShellType shell)
        {
            var souls = SoulManager.Instance;
            if (souls == null) return;

            var kept = souls.TakeHarvested();
            if (kept.Soul == null) return;

            var risen = Raise(kept.Soul, shell);
            if (risen == null) return;

            // Говорим не «поднят зомби», а чем он оказался: урок в том,
            // что вышло из промедления, а не в названии оболочки.
            Log($"{risen.DisplayName} поднялся и встал рядом.");

            if (kept.Soul.Memory == null)
                Log("Он не помнит, кем был. Слушается — и только.");
        }

        /// <summary>
        /// Поднять воина из души. Собран так же, как своих собирает
        /// лагерный спавнер: иначе поднятый вёл бы себя не как все,
        /// а движок обязан быть один на всех.
        /// </summary>
        private Warrior Raise(SoulData soul, ShellType shell)
        {
            _relSystem ??= new RelationshipSystem(AOS.MemoryProcessor.Instance);

            var go = new GameObject(soul.Name);
            go.transform.position = transform.position + transform.right * _riseOffset;
            go.transform.rotation = transform.rotation;

            var warrior = go.AddComponent<Warrior>();
            warrior.Initialize(soul, shell, _relSystem, false, Team.Player);

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
