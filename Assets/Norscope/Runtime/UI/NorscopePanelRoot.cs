using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Norscope.UI
{
    /// <summary>
    /// Root panel sizing helper. Edit preferred pixel size or metres; pixels win when both change.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NorscopePanelRoot : MonoBehaviour
    {
        [SerializeField] private NorscopeTheme theme;
        [SerializeField] private NorscopeWorldSpaceUiConfig worldSpaceConfig;
        [SerializeField] private RectTransform root;
        [SerializeField] private Image background;
        [SerializeField] private Image border;
        [SerializeField] private TMP_Text title;

        [Header("Physical size (metres)")]
        [SerializeField] private Vector2 sizeMeters = new Vector2(0.55f, 0.38f);

        [Header("Pixel size (Canvas units)")]
        [SerializeField] private Vector2 sizePixels = new Vector2(550f, 380f);

        [SerializeField] private bool useMetersAsSource = true;

        public Vector2 SizeMeters => sizeMeters;
        public Vector2 SizePixels => sizePixels;

        private void OnValidate()
        {
            SyncSizes();
            Apply();
        }

        private void Awake()
        {
            SyncSizes();
            Apply();
        }

        private void SyncSizes()
        {
            float ppm = worldSpaceConfig != null ? worldSpaceConfig.pixelsPerMeter : 1000f;
            if (useMetersAsSource)
            {
                sizePixels = sizeMeters * ppm;
            }
            else
            {
                sizeMeters = sizePixels / Mathf.Max(1f, ppm);
            }
        }

        public void Apply()
        {
            if (root == null)
            {
                root = transform as RectTransform;
            }

            if (root != null)
            {
                root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, sizePixels.x);
                root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, sizePixels.y);
            }

            if (theme != null)
            {
                if (background != null)
                {
                    background.color = theme.panelBackground;
                }

                if (border != null)
                {
                    border.color = theme.border;
                }

                if (title != null)
                {
                    title.color = theme.primaryText;
                    title.fontSize = theme.headingSize;
                    if (theme.fonts != null && theme.fonts.headingFont != null)
                    {
                        title.font = theme.fonts.headingFont;
                    }
                }
            }
        }
    }
}
