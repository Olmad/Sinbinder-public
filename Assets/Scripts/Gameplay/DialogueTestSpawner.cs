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
                _dialogueTrigger = FindObjectOfType<Dialogue.DialogueTrigger>();

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

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(go.transform);
            cube.transform.localPosition = Vector3.zero;
            cube.transform.localScale = isCommander ? new Vector3(0.5f, 1.5f, 0.5f) : new Vector3(0.5f, 1f, 0.5f);

            var renderer = cube.GetComponent<Renderer>();
            renderer.material.color = team == Team.Player ? Color.blue : Color.red;

            return warrior;
        }
    }
}