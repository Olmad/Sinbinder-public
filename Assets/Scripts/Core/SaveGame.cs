using System.Collections.Generic;

namespace Sinbinder.Core
{
    /// <summary>Душа на полке, как она ложится в файл.</summary>
    [System.Serializable]
    public class SavedSoul
    {
        public string Name;
        public int Moral;
        public int Level;
        public float[] Spectra;
        public int Quality;
    }

    /// <summary>Запись отряда, как она ложится в файл.</summary>
    [System.Serializable]
    public class SavedMember
    {
        public string Name;
        public int Sin;
        public int Moral;
        public float Intensity;
        public float Loyalty;
        public int UnpaidMissions;
        public float Leadership;
        public bool IsCommander;
        public bool IsCandidate;
        public string Unavailable;
    }

    /// <summary>
    /// Снимок игры.
    ///
    /// Сохраняется <b>состояние склепа между вылазками</b>: отряд, полка
    /// душ, казна, улучшения. Положение тел на поле и середина боя —
    /// нет, и это не упущение: бой длится минуту, а решает его движок,
    /// у которого нет скрытого состояния. Переигрывать середину боя
    /// незачем — а вот переигрывать вылазку очень хочется, и именно
    /// поэтому есть режим, где нельзя.
    ///
    /// Плоские поля и массивы: <c>JsonUtility</c> не умеет ни словари,
    /// ни свойства, ни вложенные обобщения. Узнаётся это обычно на пустом
    /// файле сохранения, который записался без единой ошибки.
    /// </summary>
    [System.Serializable]
    public class SaveGame
    {
        /// <summary>
        /// Номер уклада. Файл, записанный старой игрой, обязан быть
        /// опознан как старый, а не прочитан наполовину: половина
        /// состояния хуже, чем ничего, потому что выглядит целой.
        /// </summary>
        public int Version = Current;

        public const int Current = 1;

        public string Scene;

        public List<SavedMember> Squad = new();
        public List<SavedSoul> Shelf = new();

        public int Gold;
        public bool[] Installed;
        public bool[] Brought;

        /// <summary>
        /// Был ли режим обязательств. Хранится в самом файле, а не
        /// в настройках: иначе снимок, сделанный в ответственной игре,
        /// открывался бы в обычной — и весь смысл режима пропадал бы
        /// вместе с одной галочкой в меню.
        /// </summary>
        public bool Commitment;

        /// <summary>
        /// Чем эта запись была. Строится при снимке и хранится в файле:
        /// иначе список сохранений пришлось бы собирать, открывая каждое,
        /// а открывать чужой уклад мы как раз отказываемся.
        ///
        /// Без цифр, как и всё, что видит игрок: «Склеп · девять воинов ·
        /// долгов нет». Номер гнезда игроку тоже цифра — гнёзда
        /// называются тем, что в них лежит.
        /// </summary>
        public string Label;
    }
}
