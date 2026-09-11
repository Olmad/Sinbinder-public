using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Sinbinder.Crypt;
using Sinbinder.Gameplay;

namespace Sinbinder.Core
{
    /// <summary>
    /// Снять состояние игры и вернуть его обратно.
    ///
    /// Состояние живёт в пяти местах и ни одно из них себя не хранит:
    /// отряд (<see cref="SquadRoster"/>), полка душ
    /// (<see cref="SoulManager"/>), казна
    /// (<see cref="Inventory.PlayerInventory"/>), улучшения склепа
    /// (<see cref="CryptUpgrades"/>) и открытая сцена. Собирать это
    /// по кускам из пяти вызывающих значило бы завести пятую и шестую
    /// правду о том, что такое «игра» — поэтому сборка одна и она здесь.
    ///
    /// <b>Чего снимок не содержит.</b> Середины боя. Бой длится минуту,
    /// решает его движок, и скрытого состояния у движка нет: те же души
    /// в том же положении дадут тот же исход. Переигрывать бой незачем —
    /// а вот переигрывать вылазку очень хочется, и ровно поэтому есть
    /// <see cref="Commitment"/>.
    /// </summary>
    public static class SaveSystem
    {
        /// <summary>
        /// Гнёзда свободной игры. Их немного и они постоянные: список,
        /// который растёт без края, — это архив, а не сохранение.
        /// </summary>
        public static readonly string[] Slots = { "первое", "второе", "третье", "быстрое" };

        /// <summary>Быстрое гнездо: в него пишет F5.</summary>
        public const string Quick = "быстрое";

        /// <summary>
        /// Единственное гнездо ответственной игры.
        ///
        /// Отдельное от гнёзд свободной намеренно: иначе одна партия
        /// затирала бы записи другой, а вернуться к свободной игре
        /// после ответственной — законно.
        /// </summary>
        public const string Bound = "с обязательством";

        private static string Folder =>
            Path.Combine(Application.persistentDataPath, "saves");

        /// <summary>Путь к гнезду по имени.</summary>
        public static string PathOf(string slot)
        {
            // Имя гнезда приходит из нашего же списка, но путь собирается
            // из строки — и однажды кто-нибудь передаст сюда чужую.
            string safe = string.IsNullOrEmpty(slot) ? Quick : slot;
            foreach (char bad in Path.GetInvalidFileNameChars())
                safe = safe.Replace(bad, '_');

            return Path.Combine(Folder, safe + ".sinbin");
        }

        /// <summary>
        /// Куда пишет F5 сейчас. В ответственной игре — в её единственное
        /// гнездо: там «быстрое сохранение» и «сохранение» одно и то же.
        /// </summary>
        public static string QuickPath =>
            PathOf(Commitment.On ? Bound : Quick);

        /// <summary>Гнёзда, доступные сейчас. В ответственной игре — одно.</summary>
        public static string[] Available()
        {
            return Commitment.On ? new[] { Bound } : Slots;
        }

        // ──────────────────────────────────
        // Снимок
        // ──────────────────────────────────

        public static SaveGame Snapshot()
        {
            var save = new SaveGame
            {
                Scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                Gold = Inventory.PlayerInventory.Instance != null
                     ? Inventory.PlayerInventory.Instance.Gold : 0,
                Installed = CryptUpgrades.InstalledAll(),
                Brought = CryptUpgrades.BroughtAll(),
                Commitment = Commitment.On,
            };

            save.Label = Label(save);

            foreach (var m in SquadRoster.Members)
            {
                save.Squad.Add(new SavedMember
                {
                    Name = m.Name,
                    Sin = (int)m.Sin,
                    Moral = (int)m.Moral,
                    Gender = (int)m.Gender,
                    Intensity = m.Intensity,
                    Loyalty = m.Loyalty,
                    UnpaidMissions = m.UnpaidMissions,
                    Leadership = m.Leadership,
                    IsCommander = m.IsCommander,
                    IsCandidate = m.IsCandidate,
                    Unavailable = m.Unavailable,
                });
            }

            var souls = SoulManager.Instance;
            if (souls != null)
            {
                foreach (var kept in souls.Harvested)
                {
                    if (kept.Soul == null) continue;

                    save.Shelf.Add(new SavedSoul
                    {
                        Name = kept.Soul.Name,
                        Moral = (int)kept.Soul.Moral,
                        Level = kept.Soul.Level,
                        Spectra = kept.Soul.CopySpectra(),
                        Quality = (int)kept.Quality,
                    });
                }
            }

            return save;
        }

        // ──────────────────────────────────
        // Возврат
        // ──────────────────────────────────

        /// <summary>
        /// Вернуть состояние. Возвращает <c>false</c>, если снимок
        /// не от этой игры: молча прочитать половину хуже, чем не
        /// прочитать ничего, — половина выглядит целой.
        /// </summary>
        public static bool Restore(SaveGame save)
        {
            if (save == null) return false;

            if (save.Version != SaveGame.Current)
            {
                Debug.LogWarning($"[СОХРАНЕНИЕ] Файл другого уклада "
                               + $"({save.Version}, ждали {SaveGame.Current}). Не читаю.");
                return false;
            }

            var members = new List<SquadRoster.Member>();
            foreach (var m in save.Squad)
            {
                members.Add(new SquadRoster.Member
                {
                    Name = m.Name,
                    Sin = (SinType)m.Sin,
                    Moral = (MoralType)m.Moral,
                    Gender = (Gender)m.Gender,
                    Intensity = m.Intensity,
                    Loyalty = m.Loyalty,
                    UnpaidMissions = m.UnpaidMissions,
                    Leadership = m.Leadership,
                    IsCommander = m.IsCommander,
                    IsCandidate = m.IsCandidate,
                    Unavailable = m.Unavailable,
                });
            }

            SquadRoster.Set(members);
            CryptUpgrades.Restore(save.Installed, save.Brought);
            Commitment.Set(save.Commitment);

            Inventory.PlayerInventory.Instance?.SetGold(save.Gold);

            var souls = SoulManager.Instance;
            if (souls != null)
            {
                souls.ClearShelf();

                foreach (var s in save.Shelf)
                {
                    var soul = new SoulData(s.Name, (MoralType)s.Moral, s.Level, s.Spectra);
                    souls.PutBack(new SoulManager.Kept(soul, (SoulQuality)s.Quality));
                }
            }

            return true;
        }

        // ──────────────────────────────────
        // Файл
        // ──────────────────────────────────

        /// <summary>
        /// Чем назвать запись. Словами и по тому, что в ней лежит:
        /// «Склеп · девять воинов · трое в долгу».
        /// </summary>
        private static string Label(SaveGame save)
        {
            string where = string.IsNullOrEmpty(save.Scene) ? "Где-то" : save.Scene;

            // Счёт словами берём у Leadership: он уже говорит об этом же
            // отряде теми же словами, и второй словарь чисел разошёлся
            // бы с ним на первой правке — там об этом прямо написано.
            string who = Leadership.Collective(save.Squad.Count);

            int owed = 0;
            foreach (var m in save.Squad) if (m.UnpaidMissions > 0) owed++;

            string debt = owed == 0
                ? "долгов нет"
                : $"{Leadership.Collective(owed)} в долгу";

            return $"{where} · {who} · {debt}";
        }

        public static bool Write(SaveGame save, string path)
        {
            if (save == null) return false;

            try
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllText(path, JsonUtility.ToJson(save, true));
                return true;
            }
            catch (IOException e)
            {
                // Отсутствие права записи — событие, а не ноль: молча
                // «сохранить» и не сохранить хуже любой ошибки.
                Debug.LogWarning($"[СОХРАНЕНИЕ] Не записалось: {e.Message}");
                return false;
            }
        }

        public static SaveGame Read(string path)
        {
            if (!File.Exists(path)) return null;

            try
            {
                return JsonUtility.FromJson<SaveGame>(File.ReadAllText(path));
            }
            catch (IOException e)
            {
                Debug.LogWarning($"[СОХРАНЕНИЕ] Не прочиталось: {e.Message}");
                return null;
            }
        }

        public static bool Exists(string path) => File.Exists(path);

        /// <summary>
        /// Записать самой, без нажатия. Зовётся в названные моменты —
        /// пока такой момент один: отряд вернулся с вылазки.
        ///
        /// В свободной игре тоже пишет: точка, к которой игрок вернётся,
        /// если закроет игру, нужна в обоих режимах. Разница между
        /// режимами не в том, кто пишет, а в том, можно ли грузить.
        /// </summary>
        public static bool AutoSave()
        {
            bool ok = Write(Snapshot(), QuickPath);

            if (ok) Debug.Log("[СОХРАНЕНИЕ] Записано само.");
            return ok;
        }
    }
}
