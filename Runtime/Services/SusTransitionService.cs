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

    public class SusTransitionService
    {
        private OverlayHost _overlayHost;
        private VisualElement _curtain;
        private IVisualElementScheduledItem _animation;

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
            c.style.opacity = 0f;
            // Capture local ref — FadeIn may null _curtain while this animation still ticks.
            Animate(c, duration * 0.5f, t => c.style.opacity = t, () => onComplete?.Invoke());
        }

        public void FadeIn(float duration = 0.3f)
        {
            if (_curtain == null) return;
            var c = _curtain;
            _curtain = null;
            c.style.opacity = 1f;
            Animate(c, duration * 0.5f, t => c.style.opacity = 1f - t, () => _overlayHost?.RemoveFromOverlay(c));
        }

        public void SlideOut(TransitionStyle direction, float duration = 0.3f, Action onComplete = null)
        {
            EnsureCurtain();
            var c = _curtain;
            float start = direction == TransitionStyle.SlideLeft ? -100f : 100f;
            c.style.translate = new Translate(Length.Percent(start), 0, 0);
            c.style.opacity = 1f;
            Animate(c, duration * 0.5f, t =>
                c.style.translate = new Translate(Length.Percent(Mathf.Lerp(start, 0f, t)), 0, 0),
                onComplete);
        }

        public void SlideIn(TransitionStyle direction, float duration = 0.3f)
        {
            if (_curtain == null) return;
            var c = _curtain;
            _curtain = null;
            c.style.opacity = 1f;
            float end = direction == TransitionStyle.SlideLeft ? 100f : -100f;
            Animate(c, duration * 0.5f, t =>
                c.style.translate = new Translate(Length.Percent(Mathf.Lerp(0f, end, t)), 0, 0),
                () => _overlayHost?.RemoveFromOverlay(c));
        }

        public void CrossFade(float duration = 0.5f, Action onComplete = null)
        {
            EnsureCurtain();
            var c = _curtain;
            c.style.opacity = 0f;
            float half = duration * 0.5f;
            Animate(c, half, t => c.style.opacity = t, () =>
            {
                c.style.opacity = 1f;
                onComplete?.Invoke();
                Animate(c, half, t => c.style.opacity = 1f - t, () =>
                {
                    _overlayHost?.RemoveFromOverlay(c);
                    if (ReferenceEquals(_curtain, c))
                        _curtain = null;
                });
            });
        }

        public void Cancel()
        {
            if (_curtain == null) return;
            _animation?.Pause();
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

        private void Animate(VisualElement target, float duration, Action<float> step, Action onComplete)
        {
            if (target == null) return;
            _animation?.Pause();
            if (duration <= 0.001f) { step(1f); onComplete?.Invoke(); return; }
            float startTime = Time.unscaledTime;
            _animation = target.schedule.Execute(() =>
            {
                float t = Mathf.Clamp01((Time.unscaledTime - startTime) / duration);
                step(Mathf.SmoothStep(0f, 1f, t));
                if (t >= 1f) { _animation?.Pause(); onComplete?.Invoke(); }
            });
            _animation.Every(16);
        }
    }
}
