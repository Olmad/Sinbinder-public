// Assets/Scripts/AOS Engine/WitnessGrief.cs
// Перевод: текста нет — только числа чувств
using Sinbinder.Core;

namespace Sinbinder.AOS
{
    /// <summary>Чувства свидетеля гибели союзника: сколько печали, гнева и страха.</summary>
    public readonly struct Grief
    {
        public readonly float Sadness, Anger, Fear;
        public Grief(float sadness, float anger, float fear = 0f) { Sadness = sadness; Anger = anger; Fear = fear; }

        /// <summary>Что возьмёт верх (<see cref="EmotionSystem.GetDominantEmotion"/> — по силе).</summary>
        public EmotionType Dominant =>
            Fear > Sadness && Fear > Anger ? EmotionType.Fear
            : Anger > Sadness ? EmotionType.Anger : EmotionType.Sadness;
    }

    /// <summary>
    /// Как воин принимает гибель своего — от главного греха или добродетели души (автор, 26.11:
    /// «это всё-таки должно работать от греха»). До этого каждый свидетель получал одно и то же —
    /// печаль 0,4 и гнев 0,3, — печаль всегда брала верх, и смерть друга любого делала пугливее
    /// (Страх ×1,3), а злее — никого, даже гневливого.
    ///
    /// Чувство берёт верх по силе, а гаснет на 0,1 за пять секунд (EmotionSystem): сильное —
    /// и громче, и дольше. Что делает верх: гнев — Гнев ×1,5, Добродетель ×0,5; печаль — Страх ×1,3,
    /// прочие ×0,9; страх — Страх ×1,5, Гнев ×0,5. ЧЕРНОВИК ЧИСЕЛ — на чтение автору.
    /// </summary>
    public static class WitnessGrief
    {
        /// <summary>Прежняя реакция — для воина без души.</summary>
        public static readonly Grief Plain = new Grief(0.4f, 0.3f);

        public static Grief For(SoulData soul)
        {
            if (soul == null) return Plain;
            var sin = soul.Sin;
            bool virtue = soul.Get(sin) < 0f;

            if (!virtue)
                switch (sin)
                {
                    case SinType.Wrath:    return new Grief(0.2f, 0.6f);        // гневливый — в ярость
                    case SinType.Pride:    return new Grief(0.25f, 0.45f);      // гордец — своего тронули
                    case SinType.Envy:     return new Grief(0.2f, 0.3f);        // завистник — злость на удачливого врага, недолгая
                    case SinType.Lust:     return new Grief(0.55f, 0.3f);       // привязчивый — горе
                    case SinType.Gluttony: return new Grief(0.2f, 0.1f, 0.5f);  // плоть — испуг за свою шкуру
                    case SinType.Greed:    return new Grief(0.2f, 0.1f);        // жадный — почти мимо, гаснет за десять секунд
                    case SinType.Sloth:    return new Grief(0.6f, 0.1f);        // унылый — опускаются руки
                }

            switch (sin)
            {
                case SinType.Wrath:    return new Grief(0.4f, 0.1f);    // терпеливый — скорбь без ярости
                case SinType.Pride:    return new Grief(0.4f, 0.15f);   // смиренный — скорбь
                case SinType.Envy:     return new Grief(0.5f, 0.2f);    // добрый — горе по чужому
                case SinType.Lust:     return new Grief(0.35f, 0.2f);   // целомудренный — сдержанная скорбь
                case SinType.Gluttony: return new Grief(0.3f, 0.25f);   // умеренный — ровно
                case SinType.Greed:    return new Grief(0.45f, 0.25f);  // щедрый — горе
                case SinType.Sloth:    return new Grief(0.3f, 0.35f);   // усердный — за дело злее
                default:               return Plain;
            }
        }
    }
}
