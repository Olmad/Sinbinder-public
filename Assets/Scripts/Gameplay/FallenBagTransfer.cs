using System;
using System.Collections.Generic;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Transfers only accepted inventory items. Items rejected because the
    /// receiving inventory is full must stay in the corpse bag.
    /// Pure C# shared between Unity and .NET regression tests.
    /// </summary>
    public static class FallenBagTransfer
    {
        public static int Move<T>(List<T> source, Func<T, bool> receive)
        {
            if (source == null || receive == null) return 0;
            int moved = 0;
            for (int i = 0; i < source.Count;)
            {
                if (!receive(source[i])) { i++; continue; }
                source.RemoveAt(i);
                moved++;
            }
            return moved;
        }
    }
}
