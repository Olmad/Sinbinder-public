// Assets/Scripts/Gameplay/HuntsSinbinder.cs
using UnityEngine;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Охотник знает, за кем пришёл.
    ///
    /// Слово автора, 24 сентября: «Очень важно, чтобы обе волны бежали
    /// в сторону Греховода и дрались со всеми подряд. Чтобы нельзя было
    /// просто отойти и охотники забыли про свою цель». До этого охотник
    /// без врага рядом стоял или шёл грабить: отойди от него — и он
    /// забывал, зачем пришёл.
    ///
    /// Метка, а не поведение: исполняет её <see cref="AOS.AOSWarriorWrapper"/>.
    /// Кто рядом — с тем решает голос, как у всех (драться, бежать,
    /// спасать своего). Рядом никого — охотник идёт к Греховоду, где бы
    /// тот ни был: «они узнали, где наш лагерь».
    ///
    /// Вешает её <see cref="HunterSquadSpawner"/> — только охотникам
    /// пролога. Враги полигона и перебежчики за Греховодом не гонятся.
    /// </summary>
    [DisallowMultipleComponent]
    public class HuntsSinbinder : MonoBehaviour
    {
        /// <summary>Жива ли добыча. Мёртвого Греховода не преследуют — игра окончена.</summary>
        public static bool QuarryAlive
            => SinbinderPlayer.Exists && !SinbinderPlayer.Instance.IsDead;
    }
}
