using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Until online matchmaking exists the search is local: after a short wait the match is found and bots fill the slots
public class MatchmakingPanel : MonoBehaviour
{
    [SerializeField] private TMP_Text modeText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private Button cancelButton;
    [SerializeField] private float minSearchSeconds = 2f;
    [SerializeField] private float maxSearchSeconds = 5f;
    [SerializeField] private float matchFoundSeconds = 1f;

    private GameMode mode;
    private float elapsed;
    private float searchSeconds;
    private bool isShowing;
    private bool isFound;

    public bool IsSearching => isShowing;

    // Awake can run during the first Begin (if the panel was hidden before it woke), so it only hides when not showing
    void Awake()
    {
        cancelButton.onClick.AddListener(Cancel);

        if (!isShowing)
        {
            gameObject.SetActive(false);
        }
    }

    void Update()
    {
        elapsed += Time.unscaledDeltaTime;
        timerText.text = Mathf.FloorToInt(elapsed / 60f) + ":" + Mathf.FloorToInt(elapsed % 60f).ToString("00");

        if (!isFound && elapsed >= searchSeconds)
        {
            isFound = true;
            statusText.text = "Match found!";
            cancelButton.interactable = false;
        }

        if (isFound && elapsed >= searchSeconds + matchFoundSeconds)
        {
            enabled = false;
            MenuLaunch.LaunchMatch(mode);
        }
    }

    public void Begin(GameMode matchMode, string modeName)
    {
        mode = matchMode;
        elapsed = 0f;
        searchSeconds = Random.Range(minSearchSeconds, maxSearchSeconds);
        isFound = false;
        isShowing = true;
        modeText.text = modeName;
        statusText.text = "Searching for players...";
        cancelButton.interactable = true;
        enabled = true;
        gameObject.SetActive(true);
    }

    private void Cancel()
    {
        isShowing = false;
        gameObject.SetActive(false);
    }
}
