// Assets/Scripts/Gameplay/WarriorRig.cs
using UnityEngine;
using UnityEngine.AI;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Общая оснастка воина: всё, без чего он не воин, а декорация.
    ///
    /// Заведена после того, как выяснилось худшее из возможного: в демо
    /// <b>никто не мог сдвинуться с места</b>. Цепочка движения —
    /// <c>AOSWarriorWrapper.Execute</c> зовёт <see cref="UnitMover"/>,
    /// тот требует <see cref="NavMeshAgent"/>, тому нужен испечённый
    /// навмеш, — и собиралась она только в <c>UnitFactory</c>, которым
    /// пролог не пользуется. Спавнеры лепили воинов руками и звеньев
    /// не ставили.
    ///
    /// Последствия были не косметические. Охотники выходили в двенадцати
    /// метрах при дальности удара в два: бой не начинался вовсе. А на
    /// сцене 5 побег — это «довести отряд до края карты», и доводить
    /// было некого: круг оставался пуст, отсчёт не начинался, и демо
    /// вставало намертво.
    ///
    /// Поэтому оснастка теперь одна на всех, кто делает воинов. Три
    /// спавнера, разошедшиеся с фабрикой, — это ровно то, что здесь
    /// однажды уже случилось.
    /// </summary>
    public static class WarriorRig
    {
        /// <summary>
        /// Повесить всё общее. Возвращает <see cref="Damageable"/> —
        /// он нужен вызывающему для надголовного интерфейса.
        /// </summary>
        public static Damageable Attach(GameObject go, float speed = 3.5f)
        {
            if (go == null) return null;

            var damageable = Require<Damageable>(go);

            // Ноги. Радиус меньше стандартного: воины стоят кругом у костра
            // в трёх с половиной метрах, и на полуметровых агентах они
            // выталкивают друг друга из строя ещё до первого приказа.
            var agent = Require<NavMeshAgent>(go);
            agent.speed = speed;
            agent.angularSpeed = 360f;
            agent.acceleration = 12f;
            agent.radius = 0.35f;
            agent.height = 1.6f;
            agent.stoppingDistance = 1.2f;

            Require<UnitMover>(go);

            // Руки. Без AutoAttack воин не может ударить вообще: и приказ
            // «атаковать», и собственное решение Attack ищут этот компонент.
            Require<AutoAttack>(go);

            // Голос. Синтезированные бипы в духе Undertale задуманы
            // с самого начала (00-GDD §9), генератор написан, а
            // DialogueUI уже зовёт Speak на каждой букве — но вешать
            // компонент было некому, и GetComponent всегда возвращал
            // null. Реплики печатались молча, и звука в игре не было
            // ни одного не потому, что его не сделали, а потому, что
            // не подключили. AudioSource придёт сам: у генератора
            // RequireComponent.
            Require<Audio.VoiceGenerator>(go);

            // И то, без чего игра не игра: возможность его выделить.
            // Без SelectionComponent игрок не может выбрать никого, а значит
            // не может отдать ни одного приказа — то есть «отдай приказ,
            // посмотри, послушают ли» не начинается.
            Require<SelectionComponent>(go);

            // Тело для луча. Щелчок, правый клик по врагу и подсказка при
            // наведении ищут воина лучом, а луч видит только коллайдеры.
            // Пока тело было кубом из CreatePrimitive, коллайдер приходил
            // вместе с ним; модели с 12 сентября импортируются без
            // коллайдеров (addColliders: 0 у всех пяти), и с того дня луч
            // проходил воина насквозь: щелчок не выбирал никого, приказ
            // «бить этого» падал в землю и становился «иди туда», а вторая
            // ступень прозрачности — подсказка — молчала. Нашлось на
            // жалобе автора «Греховода выделишь — не управляется».
            //
            // Навмеш капсула не трогает: он печётся по рендер-мешам
            // и раньше, чем спавнеры ставят первого воина.
            if (go.GetComponent<Collider>() == null)
            {
                var hull = go.AddComponent<CapsuleCollider>();
                hull.center = new Vector3(0f, 0.9f, 0f);
                hull.height = 1.8f;
                hull.radius = 0.45f;
            }

            // Пол боя и цена приказа (docs/11-MISSING.md §2.3).
            Require<Fatigue>(go);
            Require<Engagement>(go);
            Require<AOS.RefusalPresenter>(go);

            // Тело говорит то, что решила душа. Молчит, пока аниматора
            // нет, — он появится вместе с первой моделью, и в тот же
            // день всё заработает само. Проводка, написанная заранее,
            // тем и хороша: её не надо вспоминать в вечер, когда модель
            // наконец легла.
            Require<WarriorAnimation>(go);

            // Первая ступень прозрачности.
            UI.OverheadBuilder.Attach(go, damageable);

            return damageable;
        }

        /// <summary>
        /// Умения по самой громкой шкале души.
        ///
        /// Отдельным вызовом, а не внутри <see cref="Attach"/>, потому что
        /// зависит от души: спавнеры сперва лепят тело, потом задают грехи,
        /// и набор, выбранный до этого, был бы выбран по пустой душе.
        /// Зовётся из <c>AOSSceneSetup.SetupWarrior</c>, который работает
        /// в Start — когда души уже расставлены.
        ///
        /// Шесть наборов умений лежали в проекте написанные и никем
        /// не повешенные. Записка объявляла это дырой движка («бюллетень
        /// жёстко из шести действий»); на деле бюллетень умения принимает
        /// давно, а не хватало ровно этих строк.
        ///
        /// Один набор, по доминирующей шкале, и полюс имеет значение:
        /// Терпение — это Гнев со знаком минус, Усердие — Уныние.
        /// У Жадности, Гордыни и Зависти умений не написано, и такой
        /// воин остаётся с шестью базовыми действиями. Это видно
        /// в замере стенда и записано как есть.
        /// </summary>
        public static void AttachSkills(GameObject go)
        {
            if (go == null) return;

            var warrior = go.GetComponent<Warrior>();
            if (warrior == null || warrior.Soul == null) return;

            var soul = AOS.Soul.FromWarrior(warrior);
            if (soul == null) return;

            var actions = AOS.SkillCatalog.Dominant(soul);
            if (actions == null || actions.Count == 0) return;

            // По первому действию набора понятно, какой это набор.
            // Спрашивать сам каталог о типе компонента нельзя: он живёт
            // в движке и о Gameplay ничего не знает — и не должен.
            switch (actions[0])
            {
                case AOS.ActionType.Berserk:   Require<WrathSkills>(go);     break;
                case AOS.ActionType.IronStance: Require<PatienceSkills>(go); break;
                case AOS.ActionType.Yawn:      Require<SlothSkills>(go);     break;
                case AOS.ActionType.WorkSurge: Require<DiligenceSkills>(go); break;
                case AOS.ActionType.Charm:     Require<LustSkills>(go);      break;
                case AOS.ActionType.Devour:    Require<GluttonySkills>(go);  break;
            }
        }

        private static T Require<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }
    }
}
