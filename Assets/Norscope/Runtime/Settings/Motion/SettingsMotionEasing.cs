using UnityEngine;

namespace Norscope.Settings.Motion
{
    /// <summary>
    /// Shared easing helpers for Settings UX motion. No allocations in Evaluate.
    /// </summary>
    public static class SettingsMotionEasing
    {
        /// <summary>Figma cubic-bezier(0.22, 1, 0.36, 1).</summary>
        public static AnimationCurve FigmaEaseOut
        {
            get
            {
                if (_figmaEaseOut == null)
                    _figmaEaseOut = CreateCubicBezier(0.22f, 1f, 0.36f, 1f);
                return _figmaEaseOut;
            }
        }

        public static AnimationCurve EaseOutQuad
        {
            get
            {
                if (_easeOutQuad == null)
                    _easeOutQuad = new AnimationCurve(
                        new Keyframe(0f, 0f, 0f, 2f),
                        new Keyframe(1f, 1f, 0f, 0f));
                return _easeOutQuad;
            }
        }

        private static AnimationCurve _figmaEaseOut;
        private static AnimationCurve _easeOutQuad;

        public static float Evaluate(AnimationCurve curve, float t)
        {
            t = Mathf.Clamp01(t);
            return curve != null ? curve.Evaluate(t) : t;
        }

        public static AnimationCurve CreateCubicBezier(float x1, float y1, float x2, float y2)
        {
            // Sampled bezier → AnimationCurve (cached by callers via FigmaEaseOut).
            const int samples = 16;
            var keys = new Keyframe[samples + 1];
            for (int i = 0; i <= samples; i++)
            {
                float t = i / (float)samples;
                Vector2 p = CubicBezier(t, x1, y1, x2, y2);
                keys[i] = new Keyframe(p.x, p.y);
            }

            var curve = new AnimationCurve(keys);
            for (int i = 0; i < curve.keys.Length; i++)
                curve.SmoothTangents(i, 0f);
            return curve;
        }

        private static Vector2 CubicBezier(float t, float x1, float y1, float x2, float y2)
        {
            // Solve for parameter u such that Bx(u) ≈ t, then return (t, By(u)).
            float u = t;
            for (int i = 0; i < 5; i++)
            {
                float bx = BezierX(u, x1, x2) - t;
                float dx = BezierDX(u, x1, x2);
                if (Mathf.Abs(dx) < 1e-5f) break;
                u = Mathf.Clamp01(u - bx / dx);
            }

            float y = BezierX(u, y1, y2);
            return new Vector2(t, y);
        }

        private static float BezierX(float u, float c1, float c2)
        {
            float omu = 1f - u;
            return 3f * omu * omu * u * c1 + 3f * omu * u * u * c2 + u * u * u;
        }

        private static float BezierDX(float u, float c1, float c2)
        {
            float omu = 1f - u;
            return 3f * omu * omu * c1 + 6f * omu * u * (c2 - c1) + 3f * u * u * (1f - c2);
        }
    }
}
