using System;
using UnityEngine;
using UnityEngine.UIElements;

using Sharq.Core;

namespace Sharq.Router
{
    public enum TransitionStyle
    {
        Fade,
        SlideLeft,
        SlideRight,
        CrossFade,
    }

    /// <summary>
    /// Overlay curtain transitions (Fade / Slide / CrossFade) on <see cref="OverlayHost"/>.
    /// Opacity paths use <see cref="SusMotion"/>; percent slides share <see cref="SusEaseUtil"/> ticks.
    /// </summary>
    public class SusTransitionService
    {
        private OverlayHost _overlayHost;
        private VisualElement _curtain;
        private SusMotionHandle _motion;
        private IVisualElementScheduledItem _slideTick;

        public bool IsTransitioning => _curtain != null;

        public OverlayHost OverlayHost
        {
            get => _overlayHost;
            set => _overlayHost = value;
        }

        public void FadeOut(float duration = 0.3f, Action onComplete = null)
        {
            EnsureCurtain();
            var c = _curtain;
            float half = duration * 0.5f;
            _motion = SusMotion.On(c)
                .FromOpacity(0f)
                .Opacity(1f, half, SusEase.QuadInOut)
                .Restore(SusRestoreMode.Keep)
                .Play(() => onComplete?.Invoke());
        }

        public void FadeIn(float duration = 0.3f)
        {
            if (_curtain == null) return;
            var c = _curtain;
            _curtain = null;
            float half = duration * 0.5f;
            _motion = SusMotion.On(c)
                .FromOpacity(1f)
                .Opacity(0f, half, SusEase.QuadInOut)
                .Restore(SusRestoreMode.Keep)
                .Play(() => _overlayHost?.RemoveFromOverlay(c));
        }

        public void SlideOut(TransitionStyle direction, float duration = 0.3f, Action onComplete = null)
        {
            EnsureCurtain();
            var c = _curtain;
            float start = direction == TransitionStyle.SlideLeft ? -100f : 100f;
            c.style.translate = new Translate(Length.Percent(start), 0, 0);
            c.style.opacity = 1f;
            // Percent translate — SusMotion uses px; keep shared SusEaseUtil tick model.
            AnimatePercent(c, duration * 0.5f,
                t => c.style.translate = new Translate(Length.Percent(Mathf.Lerp(start, 0f, t)), 0, 0),
                onComplete);
        }

        public void SlideIn(TransitionStyle direction, float duration = 0.3f)
        {
            if (_curtain == null) return;
            var c = _curtain;
            _curtain = null;
            c.style.opacity = 1f;
            float end = direction == TransitionStyle.SlideLeft ? 100f : -100f;
            AnimatePercent(c, duration * 0.5f,
                t => c.style.translate = new Translate(Length.Percent(Mathf.Lerp(0f, end, t)), 0, 0),
                () => _overlayHost?.RemoveFromOverlay(c));
        }

        public void CrossFade(float duration = 0.5f, Action onComplete = null)
        {
            EnsureCurtain();
            var c = _curtain;
            float half = duration * 0.5f;
            _motion = SusMotion.On(c)
                .FromOpacity(0f)
                .Opacity(1f, half, SusEase.QuadInOut)
                .Restore(SusRestoreMode.Keep)
                .Play(() =>
                {
                    onComplete?.Invoke();
                    _motion = SusMotion.On(c)
                        .FromOpacity(1f)
                        .Opacity(0f, half, SusEase.QuadInOut)
                        .Restore(SusRestoreMode.Keep)
                        .Play(() =>
                        {
                            _overlayHost?.RemoveFromOverlay(c);
                            if (ReferenceEquals(_curtain, c))
                                _curtain = null;
                        });
                });
        }

        public void Cancel()
        {
            StopTicks();
            if (_curtain == null) return;
            _overlayHost?.RemoveFromOverlay(_curtain);
            _curtain = null;
        }

        private void EnsureCurtain()
        {
            if (_overlayHost == null)
                throw new InvalidOperationException(
                    "SusTransitionService: OverlayHost not set. Call router.Init(overlayHost) first.");
            Cancel();
            _curtain = new VisualElement
            {
                pickingMode = PickingMode.Position,
                style =
                {
                    position = Position.Absolute,
                    top = 0, left = 0,
                    width = Length.Percent(100), height = Length.Percent(100),
                    backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 1f)),
                }
            };
            _overlayHost.AddToOverlay(_curtain, OverlayCategory.Transition);
        }

        private void StopTicks()
        {
            _slideTick?.Pause();
            _slideTick = null;
            if (_motion.IsPlaying)
                _motion.Stop(applyRestore: false);
        }

        /// <summary>
        /// Percent-based slide helper: same fixed +0.016 / Every(16) as SusMotion, easing via SusEaseUtil.
        /// </summary>
        private void AnimatePercent(VisualElement target, float duration, Action<float> step, Action onComplete)
        {
            if (target == null) return;
            StopTicks();
            if (duration <= 0.001f)
            {
                step(1f);
                onComplete?.Invoke();
                return;
            }

            float elapsed = 0f;
            _slideTick = target.schedule.Execute(() =>
            {
                elapsed += 0.016f;
                float t = Mathf.Clamp01(elapsed / duration);
                step(SusEaseUtil.Evaluate(SusEase.QuadInOut, t));
                if (t >= 1f)
                {
                    _slideTick?.Pause();
                    _slideTick = null;
                    onComplete?.Invoke();
                }
            });
            _slideTick.Every(16);
        }
    }
}
