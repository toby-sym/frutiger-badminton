using UnityEngine;

namespace FrutigerBadminton
{
    /// <summary>Keeps nearby arena structures out of the elevated gameplay view.</summary>
    public sealed class AeroCourtLevelVisibility : MonoBehaviour
    {
        Renderer[] controlledRenderers;
        bool? visible;

        void Start()
        {
            controlledRenderers = GetComponentsInChildren<Renderer>();
            UpdateVisibility();
        }

        void LateUpdate() => UpdateVisibility();

        void UpdateVisibility()
        {
            Camera camera = Camera.main;
            bool show = !camera || camera.transform.position.y < 5.6f;
            if (visible == show) return;
            visible = show;
            foreach (Renderer controlledRenderer in controlledRenderers)
                if (controlledRenderer) controlledRenderer.enabled = show;
        }
    }
}
