using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class KillFeedUI : MonoBehaviour
{
    [SerializeField] private TMP_Text feedText;
    [SerializeField] private int maxLines = 5;
    [SerializeField] private float lineSeconds = 5f;

    private readonly List<string> lines = new List<string>();
    private readonly List<float> expireTimes = new List<float>();
    private readonly StringBuilder builder = new StringBuilder();

    void Start()
    {
        MatchManager.Instance.KillRegistered += HandleKill;
        MatchManager.Instance.CustomKillRegistered += HandleCustomKill;
    }

    void OnDestroy()
    {
        if (MatchManager.Instance == null) return;

        MatchManager.Instance.KillRegistered -= HandleKill;
        MatchManager.Instance.CustomKillRegistered -= HandleCustomKill;
    }

    void Update()
    {
        if (lines.Count == 0 || Time.time < expireTimes[0]) return;

        lines.RemoveAt(0);
        expireTimes.RemoveAt(0);
        Rebuild();
    }

    private void HandleCustomKill(string message)
    {
        AddLine(message);
    }

    private void HandleKill(MatchParticipant killer, MatchParticipant victim)
    {
        if (victim == null) return;

        string line = killer == null
            ? Colored(victim) + " died"
            : Colored(killer) + " killed " + Colored(victim);

        AddLine(line);
    }

    private void AddLine(string line)
    {
        if (lines.Count >= maxLines)
        {
            lines.RemoveAt(0);
            expireTimes.RemoveAt(0);
        }

        lines.Add(line);
        expireTimes.Add(Time.time + lineSeconds);
        Rebuild();
    }

    private void Rebuild()
    {
        builder.Clear();

        foreach (string line in lines)
        {
            builder.AppendLine(line);
        }

        feedText.text = builder.ToString();
    }

    private static string Colored(MatchParticipant participant)
    {
        return "<color=#" + ColorUtility.ToHtmlStringRGB(participant.DisplayColor) + ">" + participant.DisplayName + "</color>";
    }
}
