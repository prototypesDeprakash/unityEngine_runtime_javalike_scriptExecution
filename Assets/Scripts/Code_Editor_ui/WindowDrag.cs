using UnityEngine;
using UnityEngine.EventSystems;

public class WindowDrag : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    private RectTransform windowRoot;
    private Canvas canvas;

    private void Awake()
    {
        WindowEdgeResize window = GetComponentInParent<WindowEdgeResize>();

        if (window != null)
        {
            windowRoot = window.GetComponent<RectTransform>();
        }
        else
        {
            Debug.LogError("WindowEdgeResize not found above TitleBar.");
        }

        canvas = GetComponentInParent<Canvas>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (windowRoot != null)
        {
            windowRoot.SetAsLastSibling();
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (windowRoot == null)
            return;

        float scale = canvas != null ? canvas.scaleFactor : 1f;

        windowRoot.anchoredPosition += eventData.delta / scale;
    }
}