using UnityEngine;

namespace NuclearOptionCommander;

/// <summary>
/// Client-local game speed control backed by the game's own <c>TimeScaleManager.Scale</c>.
/// Time scale is a local static on each process, so in multiplayer every player sets their
/// own speed; the host is not forced onto clients.
/// </summary>
internal sealed class CommanderGameSpeedService
{
    private static readonly float[] Steps = { 0.5f, 1f, 1.5f, 2f, 3f, 5f };

    private int stepIndex = 1;

    internal static CommanderGameSpeedService? Instance { get; private set; }

    internal CommanderGameSpeedService()
    {
        Instance = this;
    }

    internal float Current => Steps[stepIndex];

    internal string Label => CommanderSettings.GameSpeedEnabled
        ? $"SPEED {Current:0.0}x"
        : "SPEED OFF";

    internal void Activate()
    {
        ApplySetting();
    }

    internal void Deactivate() { }

    internal void ResetSession()
    {
        ApplySetting();
    }

    /// <summary>Cycles to the next speed step. Returns false when the control is unavailable.</summary>
    internal bool Cycle()
    {
        if (!CanControl())
        {
            return false;
        }

        stepIndex = (stepIndex + 1) % Steps.Length;
        CommanderSettings.GameSpeedValue = Current;
        Apply();
        return true;
    }

    private bool CanControl()
    {
        return CommanderSettings.GameSpeedEnabled && CommanderHostAuthority.IsSessionOwner();
    }

    private void ApplySetting()
    {
        float value = CommanderSettings.GameSpeedEnabled ? CommanderSettings.GameSpeedValue : 1f;
        stepIndex = NearestStep(value);
        Apply();
    }

    private static int NearestStep(float value)
    {
        int best = 1;
        float bestDelta = Mathf.Abs(Steps[1] - value);
        for (int i = 0; i < Steps.Length; i++)
        {
            float delta = Mathf.Abs(Steps[i] - value);
            if (delta < bestDelta)
            {
                best = i;
                bestDelta = delta;
            }
        }
        return best;
    }

    private void Apply()
    {
        float target = CommanderSettings.GameSpeedEnabled ? Current : 1f;
        if (Mathf.Approximately(TimeScaleManager.Scale, target))
        {
            return;
        }

        TimeScaleManager.Scale = target;
    }
}
