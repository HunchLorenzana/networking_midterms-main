using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MenuLaunch
{
    public const string MAIN_MENU_SCENE = "MainMenu";
    public const string GAME_SCENE = "Match";

    private static bool hasPendingLaunch;
    private static bool isPendingPractice;
    private static GameMode pendingMode;

    public static void LaunchPractice()
    {
        Launch(true, GameMode.FreeForAll);
    }

    public static void LaunchMatch(GameMode mode)
    {
        Launch(false, mode);
    }

    public static bool TryTakePendingLaunch(out bool isPractice, out GameMode mode)
    {
        bool hadLaunch = hasPendingLaunch;

        isPractice = isPendingPractice;
        mode = pendingMode;
        hasPendingLaunch = false;

        return hadLaunch;
    }

    // The NetworkManager is destroyed so the next arena visit starts with a fresh one
    public static void ReturnToMainMenu()
    {
        NetworkManager network = NetworkManager.Singleton;

        if (network != null)
        {
            network.Shutdown();
            Object.Destroy(network.gameObject);
        }

        SceneManager.LoadScene(MAIN_MENU_SCENE);
    }

    private static void Launch(bool isPractice, GameMode mode)
    {
        hasPendingLaunch = true;
        isPendingPractice = isPractice;
        pendingMode = mode;
        SceneManager.LoadScene(GAME_SCENE);
    }
}
