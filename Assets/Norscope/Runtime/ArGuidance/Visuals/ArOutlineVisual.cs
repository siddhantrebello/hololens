using UnityEngine;
using TMPro;

namespace Norscope.ArGuidance
{
    /// <summary>Reusable perimeter outline with optional corner brackets and faint fill.</summary>
    public sealed class ArOutlineVisual : ArHologramVisualBase
    {
        [Header("Outline (metres)")]
        [SerializeField] private float widthMeters = 0.358f;
        [SerializeField] private float depthMeters = 0.232f;
        [SerializeField] private float lineThicknessMeters = 0.002f;
        [SerializeField, Range(0f, 1f)] private float fillOpacity = 0.08f;
        [SerializeField] private float cornerBracketLengthMeters = 0.020f;
        [Tooltip("Quarter-circle radius applied to all four outline corners, in metres. " +
                 "0 keeps the original sharp 90° corners. Outer extents are unchanged — only " +
                 "the corner apex is replaced by the arc, so the straight edges still reach " +
                 "±width/2 and ±depth/2.")]
        [SerializeField] private float cornerRadiusMeters;
        [SerializeField] private bool showCornerBrackets = true;
        [SerializeField] private bool fadeInOnEnable = true;
        [SerializeField] private float fadeInSeconds = 0.45f;
        [SerializeField] private bool ghostSlideIn;
        [SerializeField] private float ghostSlideMeters = 0.030f;
        [SerializeField] private float ghostSlideSeconds = 1.2f;

        [SerializeField] private LineRenderer perimeter;
        [SerializeField] private LineRenderer[] cornerBrackets;
        [SerializeField] private MeshRenderer fillRenderer;
        [SerializeField] private TMP_Text label;

        private MaterialPropertyBlock _mpb;
        private float _spawnTime;
        private Vector3 _restLocalPos;
        private bool _restCached;

        protected override void Awake()
        {
            base.Awake();
            _mpb = new MaterialPropertyBlock();
            RebuildGeometry();
            ApplyLabel();
        }

        private void OnEnable()
        {
            _spawnTime = Time.unscaledTime;
            // Always refresh rest pose — otherwise a first-enable at (0,0,0) locks
            // ghost-slide (and later shows) to the wrong place after calibration.
            NotifyPoseApplied();
        }

        public override void NotifyPoseApplied()
        {
            _restLocalPos = transform.localPosition;
            _restCached = true;
        }

        private void Update()
        {
            if (!AnimationEnabled)
                return;

            if (fadeInOnEnable && fadeInSeconds > 0.01f)
            {
                float t = Mathf.Clamp01((Time.unscaledTime - _spawnTime) / fadeInSeconds);
                Color c = CurrentColor;
                c.a *= t;
                ApplyColored(c);
            }

            if (ghostSlideIn && ghostSlideSeconds > 0.01f)
            {
                float t = Mathf.Clamp01((Time.unscaledTime - _spawnTime) / ghostSlideSeconds);
                float eased = t * t * (3f - 2f * t);
                transform.localPosition = _restLocalPos + Vector3.up * ((1f - eased) * ghostSlideMeters);
            }
        }

        protected override void ApplyAppearance()
        {
            ApplyColored(CurrentColor);
            ApplyLabel();
        }

        private void ApplyColored(Color c)
        {
            ApplyLineColor(perimeter, c);
            if (cornerBrackets != null)
            {
                for (int i = 0; i < cornerBrackets.Length; i++)
                    ApplyLineColor(cornerBrackets[i], c);
            }

            if (fillRenderer != null)
            {
                bool showFill = fillOpacity > 0.001f;
                fillRenderer.enabled = showFill;
                if (showFill)
                {
                    Color fill = c;
                    fill.a = fillOpacity * c.a;
                    if (_mpb == null)
                        _mpb = new MaterialPropertyBlock();
                    fillRenderer.GetPropertyBlock(_mpb);
                    _mpb.SetColor("_Color", fill);
                    _mpb.SetColor("_BaseColor", fill);
                    fillRenderer.SetPropertyBlock(_mpb);
                }
            }

            if (label != null)
                label.color = c;
        }

        private void ApplyLabel()
        {
            if (label == null)
                return;
            label.text = LabelText ?? string.Empty;
            label.raycastTarget = false;
            label.transform.localPosition = LabelOffset;
        }

        public void RebuildGeometry()
        {
            float hw = widthMeters * 0.5f;
            float hd = depthMeters * 0.5f;
            float bl = cornerBracketLengthMeters;

            Vector3[] corners =
            {
                new Vector3(-hw, 0f, -hd),
                new Vector3(hw, 0f, -hd),
                new Vector3(hw, 0f, hd),
                new Vector3(-hw, 0f, hd)
            };
            Vector3[] inwardX =
            {
                Vector3.right, Vector3.left, Vector3.left, Vector3.right
            };
            Vector3[] inwardZ =
            {
                Vector3.forward, Vector3.forward, Vector3.back, Vector3.back
            };

            // A corner can't round past the shorter half-extent, nor past the bracket arm
            // length (that would invert the arm). 0 keeps the original sharp geometry, which
            // is the default for every prefab that doesn't opt in.
            float radius = Mathf.Clamp(cornerRadiusMeters, 0f, Mathf.Min(Mathf.Min(hw, hd), bl));
            bool rounded = radius > 0.0001f;

            if (perimeter != null)
            {
                perimeter.useWorldSpace = false;
                perimeter.loop = true;
                perimeter.widthMultiplier = lineThicknessMeters;
                if (rounded)
                {
                    // Same traversal order as the original loop (bottom → right → top → left).
                    // Each arc is emitted in the direction traversal enters it, so the four
                    // straight edges fall out as the spans between consecutive arcs — no
                    // explicit edge vertices needed, and no seam where they meet.
                    var loop = new Vector3[4 * CornerArcSegments];
                    WriteCornerArc(loop, 0 * CornerArcSegments, corners[0], inwardX[0], inwardZ[0], radius, true);
                    WriteCornerArc(loop, 1 * CornerArcSegments, corners[1], inwardX[1], inwardZ[1], radius, false);
                    WriteCornerArc(loop, 2 * CornerArcSegments, corners[2], inwardX[2], inwardZ[2], radius, true);
                    WriteCornerArc(loop, 3 * CornerArcSegments, corners[3], inwardX[3], inwardZ[3], radius, false);
                    perimeter.positionCount = loop.Length;
                    perimeter.SetPositions(loop);
                }
                else
                {
                    perimeter.positionCount = 4;
                    perimeter.SetPositions(corners);
                }
            }

            if (showCornerBrackets && cornerBrackets != null)
            {
                for (int i = 0; i < cornerBrackets.Length && i < 4; i++)
                {
                    LineRenderer lr = cornerBrackets[i];
                    if (lr == null)
                        continue;
                    lr.useWorldSpace = false;
                    lr.loop = false;
                    lr.widthMultiplier = lineThicknessMeters;
                    if (rounded)
                    {
                        // Arm ends stay exactly where they were; only the apex between them
                        // becomes the arc — and it is the identical arc the perimeter uses at
                        // this corner, so the two overlay perfectly instead of crossing.
                        var pts = new Vector3[CornerArcSegments + 2];
                        pts[0] = corners[i] + inwardX[i] * bl;
                        WriteCornerArc(pts, 1, corners[i], inwardX[i], inwardZ[i], radius, false);
                        pts[pts.Length - 1] = corners[i] + inwardZ[i] * bl;
                        lr.positionCount = pts.Length;
                        lr.SetPositions(pts);
                    }
                    else
                    {
                        lr.positionCount = 3;
                        lr.SetPosition(0, corners[i] + inwardX[i] * bl);
                        lr.SetPosition(1, corners[i]);
                        lr.SetPosition(2, corners[i] + inwardZ[i] * bl);
                    }
                }
            }

            ApplyAppearance();
        }

        /// <summary>Points per quarter-circle corner arc (inclusive of both tangent points).</summary>
        private const int CornerArcSegments = 5;

        /// <summary>Writes a quarter-circle rounding `corner` into `into` at `offset`, running
        /// from the tangent point on the inwardX edge to the tangent point on the inwardZ edge
        /// (reversed when `reverse`). inwardX/inwardZ are the perpendicular unit directions
        /// pointing back along the two edges that meet at this corner, so the same call yields
        /// correctly mirrored geometry for all four corners with no per-corner special-casing.</summary>
        private static void WriteCornerArc(
            Vector3[] into, int offset, Vector3 corner,
            Vector3 inwardX, Vector3 inwardZ, float radius, bool reverse)
        {
            Vector3 center = corner + inwardX * radius + inwardZ * radius;
            for (int i = 0; i < CornerArcSegments; i++)
            {
                float t = i / (float)(CornerArcSegments - 1);
                if (reverse)
                    t = 1f - t;
                float a = t * Mathf.PI * 0.5f;
                // Orthonormal basis → exact circular sweep from the X-edge tangent to the
                // Z-edge tangent, both at distance `radius` from centre.
                Vector3 dir = -inwardZ * Mathf.Cos(a) - inwardX * Mathf.Sin(a);
                into[offset + i] = center + dir * radius;
            }
        }
    }
}
