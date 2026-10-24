using UnityEngine;
using UnityEngine.EventSystems;

public class CrosshairCursor : MonoBehaviour
{
    [SerializeField] private Texture2D crosshair;

    private bool isCrosshairShown;

    void Update()
    {
        bool shouldShow = ShouldShowCrosshair();

        if (shouldShow == isCrosshairShown) return;

        isCrosshairShown = shouldShow;
        ApplyCursor();
    }

    void OnDisable()
    {
        isCrosshairShown = false;
        ApplyCursor();
    }

    private bool ShouldShowCrosshair()
    {
        PlayerController local = PlayerController.Local;

        if (local == null || !local.InputReader.IsAiming) return false;

        return EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject();
    }

    private void ApplyCursor()
    {
        if (isCrosshairShown)
        {
            Cursor.SetCursor(crosshair, new Vector2(crosshair.width / 2f, crosshair.height / 2f), CursorMode.Auto);
            return;
        }

        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }
}
