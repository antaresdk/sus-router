using System;
using UnityEngine;
using UnityEngine.UIElements;

using Sharq.Core;

namespace Sharq.Router
{
    /// <summary>
    /// Route transition: code-based animation (not USS transition-property — not supported in Unity).
    /// Built on <see cref="SusMotion"/> / <see cref="SusMotionPresets"/> (fixed +0.016 ticks via Every(16)).
    ///
    /// Transitions:
    ///   Fade       — opacity 1↔0
    ///   SlideLeft  — opacity 1↔0 + translate X -30↔0
    ///   SlideRight — opacity 1↔0 + translate X +30↔0
    /// </summary>
    public class SusRouteTransition
    {
        const float SlideOffsetPx = 30f;

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
        //  PlayOut / PlayIn — SusMotion presets; KeywordNull + remove on out
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Animate element out over Duration, then remove from hierarchy.
        /// PlayOut ≈ QuadInOut; restore <see cref="SusRestoreMode.KeywordNull"/>.
        /// </summary>
        public void PlayOut(VisualElement element)
        {
            if (Duration <= 0f || element == null) return;

            Action remove = () =>
            {
                if (element.parent != null)
                    element.RemoveFromHierarchy();
            };

            if (Id == "fade")
            {
                // Mirror SusMotionPresets.FadeOut with PlayOut ease (QuadInOut) + remove.
                SusMotion.On(element)
                    .Opacity(0f, Duration, SusEase.QuadInOut)
                    .Restore(SusRestoreMode.KeywordNull)
                    .Play(remove);
                return;
            }

            if (IsSlide())
            {
                float dx = Id == "slide-left" ? -SlideOffsetPx : SlideOffsetPx;
                // Mirror SusMotionPresets.SlideOut (+ remove on complete).
                SusMotion.On(element)
                    .Opacity(0f, Duration, SusEase.QuadInOut)
                    .Translate(new Vector2(dx, 0f), Duration, SusEase.QuadInOut)
                    .Restore(SusRestoreMode.KeywordNull)
                    .Play(remove);
            }
        }

        /// <summary>
        /// Animate element in over Duration from the "out" start-state.
        /// PlayIn ≈ QuadOut via <see cref="SusMotionPresets"/>; restore KeywordNull.
        /// </summary>
        public void PlayIn(VisualElement element)
        {
            if (Duration <= 0f || element == null) return;

            if (Id == "fade")
            {
                SusMotionPresets.FadeIn(element, Duration, SusRestoreMode.KeywordNull);
                return;
            }

            if (IsSlide())
            {
                float dx = Id == "slide-left" ? -SlideOffsetPx : SlideOffsetPx;
                SusMotionPresets.SlideIn(element, new Vector2(dx, 0f), Duration, SusRestoreMode.KeywordNull);
            }
        }

        private bool IsSlide() => Id == "slide-left" || Id == "slide-right";
    }
}
