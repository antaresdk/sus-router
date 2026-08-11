using UnityEngine;
using UnityEngine.UIElements;

using Sharq.Core;

namespace Sharq.Router
{
    /// <summary>
    /// Route transition: code-based animation (not USS transition-property — not supported in Unity).
    /// Animate opacity/translate over time via schedule.Execute.
    ///
    /// Transitions:
    ///   Fade       — opacity 1↔0
    ///   SlideLeft  — opacity 1↔0 + translate X -30↔0
    ///   SlideRight — opacity 1↔0 + translate X +30↔0
    /// </summary>
    public class SusRouteTransition
    {
        /// <summary>Transition type identifier (informational).</summary>
        public string Id { get; }

        /// <summary>Duration in seconds.</summary>
        public float Duration { get; }

        private SusRouteTransition(string id, float durationS)
        {
            Id = id;
            Duration = durationS;
        }

        public static SusRouteTransition None() => new(null, 0);
        public static SusRouteTransition Fade(float durationS = 0.3f) => new("fade", durationS);
        public static SusRouteTransition SlideLeft(float durationS = 0.3f) => new("slide-left", durationS);
        public static SusRouteTransition SlideRight(float durationS = 0.3f) => new("slide-right", durationS);

        // ════════════════════════════════════════════════════════════════
        //  PlayOut / PlayIn — code-based, no USS transition-property
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Animate element out over Duration.
        /// 1. Record initial opacity / translate
        /// 2. Schedule incremental updates until opacity reaches 0
        /// 3. After Duration: remove from DOM
        /// </summary>
        public void PlayOut(VisualElement element)
        {
            if (Duration <= 0f || element == null) return;

            var startOpacity = element.resolvedStyle.opacity;
            var startTranslate = element.resolvedStyle.translate;
            var startX = startTranslate.x;
            var startY = startTranslate.y;
            float elapsed = 0f;
            IVisualElementScheduledItem item = null;

            // Accumulate ~16ms per schedule tick (Every(16)). Wall-clock
            // realtimeSinceStartup barely advances under batchmode/-nographics
            // when many frames execute in a few ms — animations would never finish.
            item = element.schedule.Execute(() =>
            {
                elapsed += 0.016f;
                float t = Mathf.Clamp01(elapsed / Duration);
                // Ease-in-out
                t = t < 0.5f ? 2f * t * t : -1f + (4f - 2f * t) * t;

                element.style.opacity = Mathf.Lerp(startOpacity, 0f, t);

                if (IsSlide())
                {
                    float targetX = Id == "slide-left" ? startX - 30f : startX + 30f;
                    element.style.translate = new Translate(
                        Mathf.Lerp(startX, targetX, t),
                        startY);
                }

                if (t >= 1f)
                {
                    element.style.opacity = StyleKeyword.Null;
                    element.style.translate = StyleKeyword.Null;
                    if (element.parent != null)
                        element.parent.Remove(element);
                    item?.Pause();
                }
            }).Every(16); // ~60 FPS
        }

        /// <summary>
        /// Animate element in over Duration.
        /// 1. Set opacity to 0, offset X for slide
        /// 2. Schedule incremental updates to opacity 1 / translate 0
        /// </summary>
        public void PlayIn(VisualElement element)
        {
            if (Duration <= 0f || element == null) return;

            // Start from "out" state
            element.style.opacity = 0f;
            if (IsSlide())
            {
                float offsetX = Id == "slide-left" ? -30f : 30f;
                element.style.translate = new Translate(offsetX, 0);
            }

            float elapsed = 0f;
            IVisualElementScheduledItem item = null;

            item = element.schedule.Execute(() =>
            {
                elapsed += 0.016f;
                float t = Mathf.Clamp01(elapsed / Duration);
                // Ease-out
                t = 1f - (1f - t) * (1f - t);

                element.style.opacity = Mathf.Lerp(0f, 1f, t);

                if (IsSlide())
                {
                    float startOffset = Id == "slide-left" ? -30f : 30f;
                    element.style.translate = new Translate(
                        Mathf.Lerp(startOffset, 0f, t),
                        0);
                }

                if (t >= 1f)
                {
                    element.style.opacity = StyleKeyword.Null;
                    element.style.translate = StyleKeyword.Null;
                    item?.Pause();
                }
            }).Every(16);
        }

        private bool IsSlide() => Id == "slide-left" || Id == "slide-right";
    }
}
