// Assets/Scripts/Crypt/TestChamberEntry.cs
using UnityEngine;
using Sinbinder.Gameplay;

namespace Sinbinder.Crypt
{
    /// <summary>
    /// Откуда Греховод входит на полигон.
    ///
    /// Отдельный компонент, а не строка в сборщике: тело появляется
    /// в игре, а не в сцене. Сборщик ставит только точку — и это разница
    /// того же рода, из-за которой «игрок» полгода означал камеру.
    ///
    /// Ставится на пустой объект; на его месте Греховод и встаёт.
    /// </summary>
    public class TestChamberEntry : MonoBehaviour
    {
        void Start()
        {
            // Руки — статика, а статика переживает смену сцены. Не забыв
            // их, мы принесли бы в новый склеп банку из старого: душа
            // была бы и в руках, и на полке разом.
            CryptHands.Forget();

            if (SinbinderPlayer.Exists) return;

            var player = SinbinderPlayer.Spawn(transform.position, transform.parent);

            if (player == null)
                Debug.LogError("[ПОЛИГОН] Греховод не поднялся: подойти "
                             + "к рычагам будет некому.");
        }
    }
}
