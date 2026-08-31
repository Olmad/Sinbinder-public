#if UNITY_EDITOR
// Файл зависит от UnityEditor. Обёртка стоит выше using намеренно —
// тем же приёмом защищены DemoSceneBuilder и PerkDatabaseInitializer.
// Assets/Scripts/Editor/DemoAssetBuilder.cs
using System.IO;
using UnityEditor;
using UnityEngine;
using Sinbinder.AOS;
using Sinbinder.Core;
using Sinbinder.Dialogue;

namespace Sinbinder.Utilets
{
    /// <summary>
    /// Собирает ассеты из docs/11-MISSING.md §2.2 — всё, что код требует
    /// из Resources и чего в проекте не было ни одного файла.
    ///
    /// Правило одно: ничего уже существующего не затираем. Конфиг, оболочки
    /// и реплики — это то, что человек будет крутить руками (§2.4), и
    /// повторный запуск сборки не имеет права стереть его вечер. Заново
    /// собирается только база перков: её содержимое живёт в коде
    /// PerkDatabaseInitializer, руками её не правят.
    ///
    /// Значков художника в проекте нет, поэтому четыре значка решения
    /// рисуются здесь же кодом. Это не заглушки на выброс: первая ступень
    /// прозрачности обязана работать сегодня, а не после найма художника.
    /// </summary>
    public static class DemoAssetBuilder
    {
        private const string ResourcesDir = "Assets/Resources";
        private const string IconsDir = ResourcesDir + "/Icons";
        private const string ShellsDir = ResourcesDir + "/Shells";

        // ---------- меню ----------

        [MenuItem("Sinbinder/Собрать ассеты демо")]
        public static void BuildAll()
        {
            EnsureFolder(ResourcesDir);

            BuildConfig();
            BuildIcons();
            BuildShells();
            BuildDialogue();
            PerkDatabaseInitializer.Initialize();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Открытый редактор мог уже спросить библиотеку и запомнить,
            // что оболочек нет. После сборки его память врёт.
            ShellLibrary.Forget();

            Debug.Log("[АССЕТЫ] Готово: собраны ассеты демо.");
        }

        /// <summary>Точка входа для пакетного режима (-executeMethod).</summary>
        public static void BuildAllBatch() => BuildAll();

        // ---------- конфиг ----------

        /// <summary>
        /// Без этого файла AOSConfig.Load отдаёт значения по умолчанию:
        /// игра работает, но крутить баланс не за что (§2.2).
        /// </summary>
        private static void BuildConfig()
        {
            const string path = ResourcesDir + "/AOSConfig.asset";
            if (Exists<AOSConfig>(path, "конфиг AOS")) return;

            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<AOSConfig>(), path);
            Debug.Log($"[АССЕТЫ] Создан: {path}");
        }

        // ---------- значки решения ----------

        /// <summary>
        /// Четыре значка первой ступени прозрачности. Пути жёстко заданы
        /// в AOSWarriorWrapper.GetIconForAction — имена менять нельзя.
        ///
        /// Формы выбраны так, чтобы различаться силуэтом, а не цветом:
        /// значок висит над головой, живёт полторы секунды и в тумане
        /// плотностью 0.04 читается только очертанием.
        /// </summary>
        private static void BuildIcons()
        {
            EnsureFolder(IconsDir);

            // Атака: скрещенные клинки.
            Icon("Attack", (x, y) =>
                Segment(x, y, 13f, 11f, 51f, 53f) <= 3.6f ||
                Segment(x, y, 51f, 11f, 13f, 53f) <= 3.6f);

            // Спасти своего: щит.
            Icon("SaveAlly", (x, y) =>
            {
                const float top = 55f, bottom = 9f;
                if (y > top || y < bottom) return false;

                float t = (top - y) / (top - bottom);           // 0 сверху, 1 у острия
                float half = t <= 0.45f ? 18f : 18f * (1f - (t - 0.45f) / 0.55f);
                return Mathf.Abs(x - 32f) <= half;
            });

            // Добыча: монета с отверстием — кольцо не спутать со щитом.
            Icon("Loot", (x, y) =>
            {
                float dx = x - 32f, dy = y - 32f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                return d <= 21f && d >= 9f;
            });

            // Бегство: стрела прочь.
            Icon("Flee", (x, y) =>
                Segment(x, y, 9f, 32f, 40f, 32f) <= 4f ||
                Triangle(x, y, 37f, 13f, 37f, 51f, 58f, 32f));
        }

        private static void Icon(string name, System.Func<float, float, bool> inside)
        {
            // Проверяем текстурой, а не спрайтом: картинка художника,
            // импортированная как Default, спрайтом не грузится — и мы
            // затёрли бы её собственной рисовалкой.
            string path = $"{IconsDir}/{name}.png";
            if (Exists<Texture>(path, $"значок «{name}»")) return;

            var texture = Draw(inside);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[АССЕТЫ] {path} не импортировался как текстура — "
                    + "значок останется картинкой и не станет спрайтом.");
                return;
            }

            // Без Sprite здесь Resources.Load<Sprite> вернёт ноль, и первая
            // ступень прозрачности промолчит без единой ошибки в консоли.
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();

            Debug.Log($"[АССЕТЫ] Создан: {path}");
        }

        private const int IconSize = 64;

        /// <summary>
        /// Растеризация формы. Каждый пиксель пробуется шестнадцатью
        /// подточками: на стороне 64 без сглаживания диагонали клинков
        /// рассыпаются в лесенку.
        /// </summary>
        private static Texture2D Draw(System.Func<float, float, bool> inside)
        {
            const int Samples = 4;

            var texture = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false);
            var pixels = new Color32[IconSize * IconSize];

            for (int y = 0; y < IconSize; y++)
            {
                for (int x = 0; x < IconSize; x++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < Samples; sy++)
                        for (int sx = 0; sx < Samples; sx++)
                            if (inside(x + (sx + 0.5f) / Samples, y + (sy + 0.5f) / Samples))
                                hits++;

                    // Белый с прозрачностью: цвет задаёт интерфейс, не картинка.
                    byte alpha = (byte)(255 * hits / (Samples * Samples));
                    pixels[y * IconSize + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        /// <summary>Расстояние от точки до отрезка.</summary>
        private static float Segment(float px, float py, float ax, float ay, float bx, float by)
        {
            float vx = bx - ax, vy = by - ay;
            float wx = px - ax, wy = py - ay;

            float len2 = vx * vx + vy * vy;
            float t = len2 <= 0f ? 0f : Mathf.Clamp01((wx * vx + wy * vy) / len2);

            float dx = wx - t * vx, dy = wy - t * vy;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        private static bool Triangle(float px, float py,
            float ax, float ay, float bx, float by, float cx, float cy)
        {
            float d1 = Side(px, py, ax, ay, bx, by);
            float d2 = Side(px, py, bx, by, cx, cy);
            float d3 = Side(px, py, cx, cy, ax, ay);

            bool negative = d1 < 0f || d2 < 0f || d3 < 0f;
            bool positive = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(negative && positive);
        }

        private static float Side(float px, float py, float ax, float ay, float bx, float by)
            => (px - bx) * (ay - by) - (ax - bx) * (py - by);

        // ---------- оболочки ----------

        /// <summary>
        /// Смещения спектров взяты из docs/11-MISSING.md §2.2. Волчьей
        /// оболочки из того же списка здесь нет: в ShellType четыре
        /// значения, волка среди них не значится, — а придумывать
        /// пятое значение перечисления ради строки в документе нельзя,
        /// сохранённые сцены считают эти числа.
        /// </summary>
        private static void BuildShells()
        {
            EnsureFolder(ShellsDir);

            // Кость помнит только усталость. Плоти нет — нечем и желать.
            Shell(ShellType.Skeleton, "Скелет",
                hp: 18f, defense: 1f, speed: 3.6f, revivable: true, wear: 0.35f, bind: 0.30f,
                (SinType.Sloth, 20f), (SinType.Lust, -25f));

            // Гниющее тело помнит голод и не помнит, кем гордилось.
            Shell(ShellType.Zombie, "Зомби",
                hp: 30f, defense: 2f, speed: 2.4f, revivable: true, wear: 0.55f, bind: 0.40f,
                (SinType.Gluttony, 25f), (SinType.Pride, -20f));

            // Бесплотный не может взять — только смотреть, как берут другие.
            Shell(ShellType.Ghost, "Призрак",
                hp: 12f, defense: 0f, speed: 5.0f, revivable: false, wear: 0f, bind: 0.20f,
                (SinType.Envy, 25f), (SinType.Greed, -30f));

            // Камень не завидует. Камень знает, что он камень.
            Shell(ShellType.Golem, "Голем",
                hp: 42f, defense: 4f, speed: 2.0f, revivable: true, wear: 0.10f, bind: 0.50f,
                (SinType.Pride, 25f), (SinType.Envy, -25f));
        }

        private static void Shell(ShellType type, string displayName,
            float hp, float defense, float speed, bool revivable, float wear, float bind,
            params (SinType Sin, float Value)[] bias)
        {
            string path = $"{ShellsDir}/{type}.asset";
            if (Exists<ShellData>(path, $"оболочка «{displayName}»")) return;

            var shell = ScriptableObject.CreateInstance<ShellData>();
            shell.shellName = displayName;
            shell.type = type;
            shell.baseHP = hp;
            shell.baseDefense = defense;
            shell.movementSpeed = speed;
            shell.canBeRevived = revivable;
            shell.wear = wear;
            shell.bindStrength = bind;

            shell.spectrumBias = new float[SoulData.SpectrumCount];
            foreach (var b in bias) shell.spectrumBias[(int)b.Sin] = b.Value;

            AssetDatabase.CreateAsset(shell, path);
            Debug.Log($"[АССЕТЫ] Создан: {path}");
        }

        // ---------- реплики ----------

        /// <summary>
        /// Две ситуации, которые спрашивает DialogueTrigger: PRE_01 —
        /// первые слова при встрече, JOIN_01 — слова вступившего в чужой
        /// разговор. Других имён код не запрашивает, и придумывать их
        /// впрок значило бы писать реплики, которые никто не услышит.
        ///
        /// Реплика подбирается по греху и морали. Мораль здесь не второй
        /// параметр, а разница между «отдай» и «отдай, что взял не своё»:
        /// злой и добрый жадные хотят одного и того же и объясняют это
        /// себе по-разному.
        /// </summary>
        private static void BuildDialogue()
        {
            const string path = ResourcesDir + "/DialogueDatabase.asset";
            if (Exists<DialogueDatabase>(path, "база реплик")) return;

            var database = ScriptableObject.CreateInstance<DialogueDatabase>();
            AssetDatabase.CreateAsset(database, path);

            database.Situations.Add(Situation(database, "PRE_01", new[]
            {
                (SinType.Greed, MoralType.Vicious,  "У тебя есть кошель. Скоро он будет мой."),
                (SinType.Greed, MoralType.Neutral,  "Дай пройти — и я не стану тебя обыскивать."),
                (SinType.Greed, MoralType.Pious,    "Отдай, что взял не своё, и разойдёмся."),

                (SinType.Pride, MoralType.Vicious,  "Ты даже не запомнишь, кто тебя убил. А я запомню."),
                (SinType.Pride, MoralType.Neutral,  "Назовись. Я не бью безымянных."),
                (SinType.Pride, MoralType.Pious,    "Я дам тебе первый удар. Он тебе не поможет."),

                (SinType.Wrath, MoralType.Vicious,  "Наконец-то. Я устал ждать."),
                (SinType.Wrath, MoralType.Neutral,  "Отойди. Второй раз просить не буду."),
                (SinType.Wrath, MoralType.Pious,    "Не заставляй меня. Прошу — не заставляй."),

                (SinType.Envy, MoralType.Vicious,   "Хороший клинок. Тебе он больше не понадобится."),
                (SinType.Envy, MoralType.Neutral,   "Тебе везло дольше, чем мне. Это кончилось."),
                (SinType.Envy, MoralType.Pious,     "Тебе дали больше. Но не право этим бить."),

                (SinType.Lust, MoralType.Vicious,   "Ты красивее, чем стоило бы для такого дела."),
                (SinType.Lust, MoralType.Neutral,   "Жаль. Мы могли встретиться иначе."),
                (SinType.Lust, MoralType.Pious,     "Я запомню твоё лицо. И помолюсь за него."),

                (SinType.Gluttony, MoralType.Vicious, "Я не ел с утра. Ты очень вовремя."),
                (SinType.Gluttony, MoralType.Neutral, "Давай быстрее. У меня стынет."),
                (SinType.Gluttony, MoralType.Pious,   "Сначала дело, потом хлеб. Так положено."),

                (SinType.Sloth, MoralType.Vicious,  "Опять. Каждый раз одно и то же."),
                (SinType.Sloth, MoralType.Neutral,  "Может, разойдёмся? Нет? Ну ладно."),
                (SinType.Sloth, MoralType.Pious,    "Я не хотел этого дня. Но он пришёл.")
            }));

            database.Situations.Add(Situation(database, "JOIN_01", new[]
            {
                (SinType.Greed, MoralType.Vicious,  "А делить будем на всех? Тогда я в деле."),
                (SinType.Greed, MoralType.Neutral,  "Я услышал слово «кошель». Продолжайте."),
                (SinType.Greed, MoralType.Pious,    "Хватит торговаться над живыми."),

                (SinType.Pride, MoralType.Vicious,  "Вы оба говорите слишком долго для мертвецов."),
                (SinType.Pride, MoralType.Neutral,  "Договорите. Потом моя очередь."),
                (SinType.Pride, MoralType.Pious,    "Дайте им сказать. Каждому положено слово."),

                (SinType.Wrath, MoralType.Vicious,  "Хватит болтать. Начинайте уже."),
                (SinType.Wrath, MoralType.Neutral,  "Ещё слово — и я начну без вас."),
                (SinType.Wrath, MoralType.Pious,    "Замолчите оба. Кровь и так близко."),

                (SinType.Envy, MoralType.Vicious,   "А почему говорят с ним, а не со мной?"),
                (SinType.Envy, MoralType.Neutral,   "Я тоже здесь. На случай, если забыли."),
                (SinType.Envy, MoralType.Pious,     "Не мне завидовать чужому слову. И всё же."),

                (SinType.Lust, MoralType.Vicious,   "Я послушаю. Мне нравится, как он говорит."),
                (SinType.Lust, MoralType.Neutral,   "Продолжай. Ты хорошо говоришь."),
                (SinType.Lust, MoralType.Pious,     "Слова красивы. Дела будут хуже."),

                (SinType.Gluttony, MoralType.Vicious, "Пока вы спорите, я успею перекусить."),
                (SinType.Gluttony, MoralType.Neutral, "Долго ещё? Я голоден."),
                (SinType.Gluttony, MoralType.Pious,   "Поспешите. Мёртвые не едят."),

                (SinType.Sloth, MoralType.Vicious,  "Разбудите, когда закончите."),
                (SinType.Sloth, MoralType.Neutral,  "Я постою. Мне и отсюда слышно."),
                (SinType.Sloth, MoralType.Pious,    "Пусть говорят. Может, обойдётся.")
            }));

            EditorUtility.SetDirty(database);
            Debug.Log($"[АССЕТЫ] Создан: {path} — ситуаций {database.Situations.Count}");
        }

        /// <summary>
        /// Ситуация ложится подобъектом внутрь базы. Отдельными файлами
        /// это было бы сто ассетов на десять ситуаций, а без файла вовсе —
        /// список пустых ссылок после первой же перезагрузки.
        /// </summary>
        private static DialogueSituation Situation(Object owner, string name,
            (SinType Sin, MoralType Moral, string Text)[] lines)
        {
            var situation = ScriptableObject.CreateInstance<DialogueSituation>();
            situation.name = name;
            situation.SituationName = name;

            foreach (var line in lines)
                situation.Lines.Add(new DialogueSituation.LineEntry
                {
                    Sin = line.Sin,
                    Moral = line.Moral,
                    Text = line.Text
                });

            AssetDatabase.AddObjectToAsset(situation, owner);
            return situation;
        }

        // ---------- мелкие помощники ----------

        /// <summary>
        /// Есть ли уже такой ассет. Существующее не трогаем: сборка
        /// не имеет права стереть вечер, потраченный на баланс.
        /// </summary>
        private static bool Exists<T>(string path, string what) where T : Object
        {
            if (AssetDatabase.LoadAssetAtPath<T>(path) == null) return false;

            Debug.Log($"[АССЕТЫ] Оставлен как есть: {path} ({what})");
            return true;
        }

        private static void EnsureFolder(string path)
        {
            if (path == "Assets" || AssetDatabase.IsValidFolder(path)) return;

            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
#endif
