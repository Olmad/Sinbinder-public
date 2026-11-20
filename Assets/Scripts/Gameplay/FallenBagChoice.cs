// Who decides what to do with a fallen warrior's possessions?
// A pure part of the real camp behaviour, regression-tested without Unity.
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    public enum FallenBagIntent
    {
        Leave = 0,
        DeliverToLord = 1,
        Steal = 2,
        RespectDead = 3
    }

    public static class FallenBagChoice
    {
        public static FallenBagIntent Decide(SinType sin, MoralType moral,
                                              float relationship, bool brother)
        {
            // Attachment to the deceased wins over personal greed.
            if (brother || relationship > 20f) return FallenBagIntent.DeliverToLord;

            // Greed is an autonomous temptation. The owner does not need to
            // be a sworn enemy: a stranger's purse is still tempting.
            // This is deliberately NOT gated by the player's proximity.
            if (sin == SinType.Greed) return FallenBagIntent.Steal;
            if (moral == MoralType.Pious) return FallenBagIntent.RespectDead;

            return FallenBagIntent.Leave;
        }
    }
}
