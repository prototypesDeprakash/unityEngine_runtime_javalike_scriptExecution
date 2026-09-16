using UnityEngine;
using UnityEngine.EventSystems;

// Put this directly on the window panel (the Image with Raycast Target on).
// Dragging within `edgeMargin` pixels of an edge resizes the window from
// that edge; dragging the middle of the panel does nothing (that's what
// WindowDrag on the title bar is for).
[RequireComponent(typeof(RectTransform))]
public class WindowEdgeResize : MonoBehaviour,
    IPointerDownHandler, IDragHandler, IPointerMoveHandler, IPointerExitHandler
{
    [Header("Behaviour")]
    public float edgeMargin = 8f;
    public Vector2 minSize = new Vector2(250f, 150f);

    [Header("Title bar (auto-stretches to match window width)")]
    public RectTransform titleBar; // assign the titleBar child here in the Inspector

    [Header("Optional cursor icons (leave null to skip)")]
    public Texture2D horizontalCursor;
    public Texture2D verticalCursor;
    public Texture2D diagonalCursorNWSE;
    public Texture2D diagonalCursorNESW;

    private RectTransform rect;
    private Canvas canvas;

    private bool resizingLeft, resizingRight, resizingTop, resizingBottom;
    private bool isDragging;

    private void Awake()
    {
        rect = (RectTransform)transform;
        canvas = GetComponentInParent<Canvas>();

        // Force top-left pivot WITHOUT visually shifting the window.
        // (Setting .pivot directly moves the rect, since anchoredPosition
        // is measured relative to pivot — this compensates for that.)
        SetPivotPreservingPosition(new Vector2(0f, 1f));

        FixTitleBarAnchors();
    }

    private void SetPivotPreservingPosition(Vector2 newPivot)
    {
        Vector2 size = rect.rect.size;
        Vector2 deltaPivot = rect.pivot - newPivot;
        Vector3 deltaPosition = new Vector3(deltaPivot.x * size.x, deltaPivot.y * size.y, 0f);
        rect.pivot = newPivot;
        rect.localPosition -= rect.TransformVector(deltaPosition);
    }

    // Forces titleBar to actually stretch-fill the window's width,
    // regardless of whatever offsetMin/offsetMax got left behind when
    // the stretch anchor preset was applied in the editor.
    private void FixTitleBarAnchors()
    {
        if (titleBar == null) return;

        titleBar.anchorMin = new Vector2(0f, 1f);
        titleBar.anchorMax = new Vector2(1f, 1f);
        titleBar.pivot = new Vector2(0f, 1f);

        // Flush left/right edges — keep whatever height (y offsets) you set.
        titleBar.offsetMin = new Vector2(0f, titleBar.offsetMin.y);
        titleBar.offsetMax = new Vector2(0f, titleBar.offsetMax.y);
    }

    private bool GetLocalPoint(PointerEventData eventData, out Vector2 localPoint)
    {
        Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? eventData.pressEventCamera
            : null;
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, cam, out localPoint);
    }

    private void UpdateEdgeFlags(Vector2 localPoint)
    {
        float width = rect.rect.width;
        float height = rect.rect.height;

        resizingLeft = localPoint.x <= edgeMargin;
        resizingRight = localPoint.x >= width - edgeMargin;
        resizingTop = localPoint.y >= -edgeMargin;
        resizingBottom = localPoint.y <= -height + edgeMargin;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!GetLocalPoint(eventData, out Vector2 localPoint)) return;

        UpdateEdgeFlags(localPoint);
        isDragging = resizingLeft || resizingRight || resizingTop || resizingBottom;

        if (isDragging)
        {
            rect.SetAsLastSibling();
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        float scale = (canvas != null) ? canvas.scaleFactor : 1f;
        Vector2 delta = eventData.delta / scale;

        Vector2 size = rect.sizeDelta;
        Vector2 pos = rect.anchoredPosition;

        if (resizingRight)
        {
            size.x = Mathf.Max(minSize.x, size.x + delta.x);
        }
        if (resizingLeft)
        {
            float rightFixed = pos.x + size.x;
            size.x = Mathf.Max(minSize.x, size.x - delta.x);
            pos.x = rightFixed - size.x;
        }
        if (resizingBottom)
        {
            size.y = Mathf.Max(minSize.y, size.y - delta.y);
        }
        if (resizingTop)
        {
            float bottomFixed = pos.y - size.y;
            size.y = Mathf.Max(minSize.y, size.y + delta.y);
            pos.y = bottomFixed + size.y;
        }

        rect.sizeDelta = size;
        rect.anchoredPosition = pos;
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        if (isDragging) return;
        if (!GetLocalPoint(eventData, out Vector2 localPoint)) return;

        UpdateEdgeFlags(localPoint);
        ApplyCursor();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (isDragging) return;
        ResetCursor();
    }

    private void OnDisable()
    {
        isDragging = false;
        ResetCursor();
    }

    private void ApplyCursor()
    {
        if ((resizingLeft && resizingTop) || (resizingRight && resizingBottom))
        {
            SetCursor(diagonalCursorNWSE);
        }
        else if ((resizingRight && resizingTop) || (resizingLeft && resizingBottom))
        {
            SetCursor(diagonalCursorNESW);
        }
        else if (resizingLeft || resizingRight)
        {
            SetCursor(horizontalCursor);
        }
        else if (resizingTop || resizingBottom)
        {
            SetCursor(verticalCursor);
        }
        else
        {
            ResetCursor();
        }
    }

    private void SetCursor(Texture2D tex)
    {
        if (tex == null)
        {
            ResetCursor();
            return;
        }
        Cursor.SetCursor(tex, new Vector2(tex.width / 2f, tex.height / 2f), CursorMode.Auto);
    }

    private void ResetCursor()
    {
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }
}