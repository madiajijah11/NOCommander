using NuclearOption.Networking;

namespace NuclearOptionCommander;

/// <summary>
/// Single source of truth for command authority checks.
/// </summary>
internal static class CommanderHostAuthority
{
    /// <summary>
    /// Strict host authority. Use for anything that spends faction funds, spawns networked
    /// objects, or writes NetworkVariables. A missing network manager means "not host".
    /// </summary>
    internal static bool IsHostAuthority()
    {
        NetworkManagerNuclearOption? manager = NetworkManagerNuclearOption.i;
        return manager != null && manager.Server != null && manager.Server.Active;
    }

    /// <summary>
    /// Ownership of the local session, used for client-local conveniences that never touch
    /// server state (camera, HUD, time scale). A missing network manager means local play.
    /// </summary>
    internal static bool IsSessionOwner()
    {
        NetworkManagerNuclearOption? manager = NetworkManagerNuclearOption.i;
        return manager == null || (manager.Server != null && manager.Server.Active);
    }
}
