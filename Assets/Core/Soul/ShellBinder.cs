// Assets/Core/Soul/ShellBinder.cs
using UnityEngine;

namespace Sinbinder.Core
{
    /// <summary>
    /// Связывание: душа входит в тело и немного становится этим телом.
    ///
    /// Смещение оседает при каждом связывании, а не читается на лету.
    /// Это принципиально: дрейф необратим. Вынув душу из волка, вы
    /// получите обратно не ту, кого вкладывали, — и второй раз в волка
    /// она уйдёт дальше. Игрок волен так делать; он просто должен знать
    /// цену, и панель предсказания темперамента ему её покажет.
    /// </summary>
    public static class ShellBinder
    {
        public static void Bind(SoulData soul, ShellData shell)
        {
            if (soul == null || shell == null) return;
            if (shell.bindStrength <= 0f) return;

            for (int i = 0; i < SoulData.SpectrumCount; i++)
            {
                float bias = shell.GetBias((SinType)i);
                if (Mathf.Approximately(bias, 0f)) continue;
                soul.Change((SinType)i, bias * shell.bindStrength);
            }
        }
    }
}
