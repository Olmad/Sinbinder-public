// Assets/Scripts/Gameplay/HunterSquadSpawner.cs
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Охотники — единственный противник демо (docs/00-GDD.md §8).
    ///
    /// Их задача в прологе двойная: на доле 4 проиграть, чтобы игрок
    /// поверил во всемогущество, и на доле 5 оказаться не четырьмя,
    /// а сорока. Поэтому число задаётся снаружи, а не зашито.
    ///
    /// Как и отряд игрока, охотники создаются в рантайме: личность
    /// и отношения у Warrior не сериализуются.
    /// </summary>
    public class HunterSquadSpawner : MonoBehaviour
    {
        [SerializeField] private int _count = 4;
        [SerializeField] private float _lineWidth = 6f;
        [SerializeField] private bool _spawnOnStart = true;

        private Core.RelationshipSystem _relSystem;

        /// <summary>
        /// Охотники берут в наём кого попало, поэтому души у них разные.
        /// Список фиксирован и перебирается по кругу: одинаковый вход
        /// обязан давать одинаковый выход, никакого Random.
        /// </summary>
        private static readonly (string Name, SinType Sin, MoralType Moral, float Intensity)[] Kinds =
        {
            ("Охотник",          SinType.Wrath,    MoralType.Vicious, 60f),
            ("Охотник-следопыт", SinType.Envy,     MoralType.Neutral, 45f),
            ("Охотник-мясник",   SinType.Gluttony, MoralType.Vicious, 55f),
            ("Ловчий",           SinType.Greed,    MoralType.Vicious, 50f),
        };

        void Start()
        {
            if (_spawnOnStart) SpawnHunters();
        }

        [ContextMenu("Выпустить охотников")]
        public void SpawnHunters()
        {
            _relSystem = new Core.RelationshipSystem(AOS.MemoryProcessor.Instance);

            for (int i = 0; i < _count; i++)
                SpawnHunter(i);

            Debug.Log($"[ПРОЛОГ] Охотников выпущено: {_count}.");
        }

        /// <summary>
        /// Строй в шеренгу, а не в круг: охотники приходят извне и надвигаются,
        /// тогда как лагерь сидит вокруг огня. Разница в построении читается
        /// раньше, чем разница в цвете.
        /// </summary>
        private Vector3 PlaceInLine(int index)
        {
            if (_count <= 1) return transform.position;

            float t = index / (float)(_count - 1);
            return transform.position + transform.right * Mathf.Lerp(-_lineWidth * 0.5f, _lineWidth * 0.5f, t);
        }

        private Warrior SpawnHunter(int index)
        {
            var kind = Kinds[index % Kinds.Length];
            string name = _count > Kinds.Length ? $"{kind.Name} {index + 1}" : kind.Name;

            var go = new GameObject(name);
            go.transform.SetParent(transform);
            go.transform.position = PlaceInLine(index);
            go.transform.rotation = transform.rotation;

            var warrior = go.AddComponent<Warrior>();
            var soul = new SoulData(name, kind.Sin, kind.Moral, 1, kind.Intensity);
            warrior.Initialize(soul, ShellType.Zombie, _relSystem, index == 0, Team.Enemy);

            go.AddComponent<Damageable>();
            go.AddComponent<Fatigue>();
            go.AddComponent<Engagement>();
            go.AddComponent<AOS.RefusalPresenter>();

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Тело";
            body.transform.SetParent(go.transform);
            body.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            body.transform.localRotation = Quaternion.identity;
            body.transform.localScale = new Vector3(0.55f, 1.3f, 0.55f);

            return warrior;
        }
    }
}
