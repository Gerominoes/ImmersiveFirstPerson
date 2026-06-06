using System.Text;
using UnityEngine;

namespace ImmersiveFirstPerson;

internal static class RendererScanner
{
    private static Player? _loggedPlayer;
    private static bool _logged;

    internal static void LogRenderersOnce(Player player)
    {
        if (!Plugin.EnableDebugLogs.Value)
            return;

        if (_logged && _loggedPlayer == player)
            return;

        _logged = true;
        _loggedPlayer = player;

        Renderer[] renderers = player.GetComponentsInChildren<Renderer>(true);
        Plugin.Log.LogInfo($"[RendererScanner] Found {renderers.Length} local player renderers.");

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            string path = renderer.transform != null ? GetPath(renderer.transform) : "<no transform>";
            Plugin.Log.LogInfo($"[RendererScanner] name='{renderer.name}' path='{path}' type='{renderer.GetType().Name}' enabled={renderer.enabled}");
        }
    }

    internal static void ResetLogState()
    {
        _loggedPlayer = null;
        _logged = false;
    }

    internal static string GetPath(Transform transform)
    {
        if (transform == null)
            return string.Empty;

        StringBuilder builder = new(transform.name);
        Transform? current = transform.parent;

        while (current != null)
        {
            builder.Insert(0, current.name + "/");
            current = current.parent;
        }

        return builder.ToString();
    }
}
