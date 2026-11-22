using UnityEngine;

namespace FrutigerBadminton
{
    /// <summary>Keeps the overhead structure out of the elevated gameplay view.</summary>
    public sealed class AeroCanopyVisibility : MonoBehaviour
    {
        Renderer[] canopyRenderers;
        bool? visible;

        void Start()
        {
            canopyRenderers = GetComponentsInChildren<Renderer>();
            UpdateVisibility();
        }

        void LateUpdate() => UpdateVisibility();

        void UpdateVisibility()
        {
            Camera camera = Camera.main;
            bool show = !camera || camera.transform.position.y < 5.6f;
            if (visible == show) return;
            visible = show;
            foreach (Renderer canopyRenderer in canopyRenderers)
                if (canopyRenderer) canopyRenderer.enabled = show;
        }
    }
}
