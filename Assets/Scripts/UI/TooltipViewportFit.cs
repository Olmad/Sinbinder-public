// Shared, Unity-independent viewport geometry for the AOS warrior tooltip.
// Compiled by Unity and by Tools/tutorial/TutorialChecks.csproj in CI.
using System;

namespace Sinbinder.UI
{
    public static class TooltipViewportFit
    {
        // Never enlarge a standard tooltip. Fit even an oversized explanation
        // inside the view while preserving its native aspect and panel pivot.
        // The caller measures the panel at its original prefab scale.
        public static float Scale(float width, float height,
                                  float screenWidth, float screenHeight, float margin)
        {
            float availableWidth = Math.Max(1f, screenWidth - 2f * margin);
            float availableHeight = Math.Max(1f, screenHeight - 2f * margin);
            return Math.Min(1f, Math.Min(availableWidth / Math.Max(1f, Math.Abs(width)),
                                         availableHeight / Math.Max(1f, Math.Abs(height))));
        }
    }
}
