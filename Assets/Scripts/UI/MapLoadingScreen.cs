using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The Match scene's start screen: shown by UIManager until the local player exists, i.e. while the map loads
public class MapLoadingScreen : MonoBehaviour
{
    [SerializeField] private TMP_Text mapNameText;
    [SerializeField] private Image previewImage;

    public void Show(MapDefinition map)
    {
        mapNameText.text = map.DisplayName;
        previewImage.sprite = map.PreviewImage;
        previewImage.enabled = map.PreviewImage != null;
    }
}
