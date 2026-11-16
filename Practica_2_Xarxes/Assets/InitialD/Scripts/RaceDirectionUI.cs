using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class RaceDirectionUI : MonoBehaviour
{
    public enum Direction
    {
        Left,
        Right,
        SharpLeft,
        SharpRight
    }

    [Header("UI")]
    [SerializeField] private Image arrowImage;

    [Header("Arrow Sprites")]
    [SerializeField] private Sprite leftArrow;
    [SerializeField] private Sprite rightArrow;
    [SerializeField] private Sprite sharpLeftArrow;
    [SerializeField] private Sprite sharpRightArrow;

    [Header("Settings")]
    [SerializeField] private float defaultDuration = 3f;

    private Coroutine hideCoroutine;

    //Hides the direction arrow when the UI is initialized
    private void Awake()
    {
        if (arrowImage != null)
        {
            arrowImage.enabled = false;
        }
    }

    //Displays the selected direction and restarts the hide timer
    public void ShowDirection(Direction direction)
    {
        ShowDirection(direction, defaultDuration);
    }

    public void ShowDirection(Direction direction, float duration)
    {
        if (arrowImage == null)
        {
            Debug.LogWarning("[DIRECTION UI] Arrow Image is not assigned.");
            return;
        }

        Sprite selectedSprite = GetSprite(direction);

        if (selectedSprite == null)
        {
            Debug.LogWarning($"[DIRECTION UI] Missing sprite for {direction}.");
            return;
        }

        arrowImage.sprite = selectedSprite;
        arrowImage.enabled = true;

        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
        }

        hideCoroutine = StartCoroutine(HideAfterDelay(duration));
    }

    //Immediately hides the arrow and cancels any active hide timer
    public void HideDirection()
    {
        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
            hideCoroutine = null;
        }

        if (arrowImage != null)
        {
            arrowImage.enabled = false;
        }
    }

    private Sprite GetSprite(Direction direction)
    {
        switch (direction)
        {
            case Direction.Left:
                return leftArrow;

            case Direction.Right:
                return rightArrow;

            case Direction.SharpLeft:
                return sharpLeftArrow;

            case Direction.SharpRight:
                return sharpRightArrow;

            default:
                return null;
        }
    }

    //Waits for the specified duration before hiding the arrow automatically
    private IEnumerator HideAfterDelay(float duration)
    {
        yield return new WaitForSeconds(
            Mathf.Max(0f, duration)
        );

        if (arrowImage != null)
        {
            arrowImage.enabled = false;
        }

        hideCoroutine = null;
    }
}