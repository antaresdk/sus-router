using UnityEngine.UIElements;

namespace Sharq.Router
{
    /// <summary>
    /// Shared focus helpers for screen AutoFocus and modal focus-in (ARCH-LUNA-GAP-B D1–D3).
    /// </summary>
    public static class SusFocusUtil
    {
        /// <summary>
        /// USS / marker class matching the arch opt-out <c>data-sus-no-auto-focus</c>.
        /// </summary>
        public const string NoAutoFocusClass = "data-sus-no-auto-focus";

        /// <summary>
        /// Finds the first focusable, enabled, visibly laid-out descendant (depth-first Query order).
        /// Skips <see cref="DisplayStyle.None"/>, hidden, and zero-size layout when resolved.
        /// </summary>
        public static VisualElement FindFirstFocusable(VisualElement root)
        {
            if (root == null) return null;

            return root.Query<VisualElement>()
                .Where(IsFocusCandidate)
                .First();
        }

        /// <summary>
        /// Whether <paramref name="ve"/> is a valid target for AutoFocus / modal focus-in.
        /// </summary>
        public static bool IsFocusCandidate(VisualElement ve)
        {
            if (ve == null) return false;
            if (!ve.focusable || !ve.enabledInHierarchy) return false;

            var display = ve.resolvedStyle.display;
            if (display == DisplayStyle.None) return false;

            var visibility = ve.resolvedStyle.visibility;
            if (visibility == Visibility.Hidden) return false;

            // Zero layout only when the element has been through layout (non-NaN).
            var w = ve.layout.width;
            var h = ve.layout.height;
            if (!float.IsNaN(w) && !float.IsNaN(h) && (w <= 0f || h <= 0f))
                return false;

            return true;
        }

        /// <summary>
        /// True when <paramref name="descendant"/> is <paramref name="ancestor"/> or nested under it.
        /// </summary>
        public static bool IsUnder(VisualElement descendant, VisualElement ancestor)
        {
            if (descendant == null || ancestor == null) return false;
            for (var p = descendant; p != null; p = p.parent)
            {
                if (p == ancestor) return true;
            }
            return false;
        }
    }
}
