// Assets/Scripts/Gameplay/DialogueTestSpawner.cs
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    public class DialogueTestSpawner : MonoBehaviour
    {
        [SerializeField] private Dialogue.DialogueTrigger _dialogueTrigger;
        private Core.RelationshipSystem _relSystem;

        void Start()
        {
            // Инициализатор поля отработал бы до Awake процессора памяти
            // и навсегда захватил бы null, молча выключив отношения.
            _relSystem = new Core.RelationshipSystem(AOS.MemoryProcessor.Instance);

            if (_dialogueTrigger == null)
                _dialogueTrigger = FindFirstObjectByType<Dialogue.DialogueTrigger>();

            // Союзники — Team.Player (синие)
            SpawnWarrior("Гордый Скелет", SinType.Pride, MoralType.Pious, ShellType.Skeleton, true, Team.Player, new Vector3(-5, 0, 0));
            SpawnWarrior("Жадный Скелет", SinType.Greed, MoralType.Vicious, ShellType.Skeleton, true, Team.Player, new Vector3(-3, 0, 2));

            // Враги — Team.Enemy (красные)
            SpawnWarrior("Гордый Зомби", SinType.Pride, MoralType.Vicious, ShellType.Zombie, true, Team.Enemy, new Vector3(5, 0, 0));
            SpawnWarrior("Гневный Орк", SinType.Wrath, MoralType.Vicious, ShellType.Golem, true, Team.Enemy, new Vector3(3, 0, 2));

            // Рядовые (не командиры) — не должны говорить
            SpawnWarrior("Рядовой Скелет 1", SinType.Sloth, MoralType.Neutral, ShellType.Skeleton, false, Team.Player, new Vector3(-4, 0, -2));
            SpawnWarrior("Рядовой Орк 1", SinType.Greed, MoralType.Vicious, ShellType.Zombie, false, Team.Enemy, new Vector3(4, 0, -2));
        }

        private Warrior SpawnWarrior(string name, SinType sin, MoralType moral, ShellType shell, bool isCommander, Team team, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.position = pos;

            var warrior = go.AddComponent<Warrior>();
            var soul = new SoulData(name, sin, moral, 1, sin == SinType.Pride ? 70f : -30f);
            warrior.Initialize(soul, shell, _relSystem, isCommander, team);

            var body = WarriorLook.Build(go, shell,
                                         isCommander ? 1.5f : 1f, 0.5f, 0f);

            // Отладочный спавнер красит по стороне: он для того и есть,
            // чтобы с одного взгляда различать своих и чужих. Красим
            // то, что вернул шов, а не куб: с моделью это тоже сработает.
            var renderer = body != null ? body.GetComponentInChildren<Renderer>() : null;
            if (renderer != null)
                renderer.material.color = team == Team.Player ? Color.blue : Color.red;

            return warrior;
        }
    }
}