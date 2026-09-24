// Assets/Scripts/Gameplay/Beat.cs
using System;
using System.Collections;
using UnityEngine;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Один шаг пролога: дождаться того, что сделал игрок.
    ///
    /// Решение автора от 13 сентября, после прохождения демо: доли
    /// пролога ведутся действиями, а не секундами (14-HANDOFF §25).
    /// Карган звал к столу через четырнадцать секунд после старта сцены,
    /// тревога приходила через семь секунд после совета — независимо
    /// от того, сделал ли игрок то, ради чего шаг существует. Сцена
    /// истекала, а не случалась.
    ///
    /// Правило, уточнённое автором 24 сентября:
    ///
    /// <b>Событие ведёт. Шаг игрока ждёт игрока — срок только напоминает.
    /// Срок страхует лишь то, что игрок исправить не может, и тогда
    /// обязан кричать.</b>
    ///
    /// До 24 сентября страховка стояла и на шагах игрока: не подошёл
    /// к сундуку за две минуты — тревога пришла сама, не вернулся к шару —
    /// отряды погасли без него, не подошёл к столу — совет открылся сам.
    /// Автор: «по-прежнему всё работает от времени, поэтому можно просто
    /// стоять, а сюжет будет двигаться». Для шага игрока ожидание —
    /// не зависание: он стоит, пока не пошёл. Такие шаги ждут через
    /// <see cref="UntilPlayer"/>.
    ///
    /// <see cref="Until"/> со сроком остаётся тому, что ведёт не игрок:
    /// ушла ли заставка, дошёл ли провожатый, снялась ли пауза. Застрянь
    /// там машина — ждать было бы нечего, и доля заперлась бы насмерть.
    /// Истёкший срок пишет предупреждение всегда, без флажка «можно тихо»:
    /// страховка, сработавшая молча, прячет ровно то, что шаг не случился.
    ///
    /// Время нескалированное: совет и заставка ставят игру на паузу,
    /// и игровое время под ними не идёт. Пока игра на паузе, срок
    /// страховки <b>не тратится</b> — игрок читает панель, а не медлит.
    ///
    /// Время как длительность эффекта — наезд, полосы, вспышки шара,
    /// пауза на прочтение строки — не шаг, и сюда не относится.
    /// </summary>
    public static class Beat
    {
        /// <summary>
        /// Ждать, пока <paramref name="happened"/> не станет правдой.
        /// Не случилось за <paramref name="safety"/> секунд игры не на
        /// паузе — идти дальше и сказать об этом <paramref name="late"/>.
        /// </summary>
        public static IEnumerator Until(Func<bool> happened, float safety, string late)
        {
            float waited = 0f;

            while (!happened())
            {
                if (!Paused()) waited += Time.unscaledDeltaTime;

                if (waited >= safety)
                {
                    Debug.LogWarning("[ПРОЛОГ] " + late);
                    yield break;
                }

                yield return null;
            }
        }

        /// <summary>
        /// Ждать поступка игрока — сколько потребуется.
        ///
        /// Вместо срока — напоминание: раз в <paramref name="nudgeEvery"/>
        /// секунд игры не на паузе журнал говорит <paramref name="nudge"/>.
        /// Пусто — ждём молча. Игра не делает шаг за игрока, но и не
        /// бросает его гадать, чего от него ждут.
        /// </summary>
        public static IEnumerator UntilPlayer(Func<bool> happened, float nudgeEvery, string nudge)
        {
            float waited = 0f;

            while (!happened())
            {
                if (!Paused()) waited += Time.unscaledDeltaTime;

                if (nudgeEvery > 0f && waited >= nudgeEvery)
                {
                    waited = 0f;
                    if (!string.IsNullOrEmpty(nudge))
                        UnityEngine.Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(nudge);
                }

                yield return null;
            }
        }

        private static bool Paused()
            => Core.GamePauseController.Instance != null
            && Core.GamePauseController.Instance.IsPaused;
    }
}
