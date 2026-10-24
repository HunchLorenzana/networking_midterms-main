using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScoreboardRow : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text killsText;
    [SerializeField] private TMP_Text deathsText;
    [SerializeField] private TMP_Text assistsText;
    [SerializeField] private TMP_Text loadoutText;

    public void Show(MatchParticipant participant)
    {
        nameText.text = participant.DisplayName;
        nameText.color = participant.DisplayColor;
        killsText.text = participant.Kills.ToString();
        deathsText.text = participant.Deaths.ToString();
        assistsText.text = participant.Assists.ToString();
        loadoutText.text = participant.GetLoadoutLabel();
    }
}
