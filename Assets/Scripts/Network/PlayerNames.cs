using System.Collections.Generic;

public static class PlayerNames
{
    public const int MAX_LENGTH = 12;

    private static readonly Dictionary<ulong, string> namesByClient = new Dictionary<ulong, string>();

    public static string Clean(string rawName)
    {
        string cleaned = rawName.Trim();

        return cleaned.Length > MAX_LENGTH ? cleaned.Substring(0, MAX_LENGTH) : cleaned;
    }

    public static void Set(ulong clientId, string playerName)
    {
        namesByClient[clientId] = playerName;
    }

    public static string Get(ulong clientId)
    {
        return namesByClient.TryGetValue(clientId, out string playerName) ? playerName : "Player";
    }
}
