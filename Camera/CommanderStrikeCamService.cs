using System.Collections.Generic;
using UnityEngine;

namespace NuclearOptionCommander;

internal sealed class CommanderStrikeCamService
{
    private const float PiPWidth = 220f;
    private const float PiPHeight = 132f;
    private const float LingerDurationSeconds = 2.0f;

    private RenderTexture? pipTexture;
    private Camera? pipCamera;
    private GameObject? cameraObject;
    private Unit? trackedMunition;
    private float lingerEndTime;
    private Vector3 lastImpactPos;
    private bool manualDismissed;
    private float nextScanTime;

    internal static CommanderStrikeCamService? Instance { get; private set; }

    internal bool IsActive => pipCamera != null && pipCamera.enabled;

    internal CommanderStrikeCamService()
    {
        Instance = this;
    }

    internal void Tick()
    {
        if (!CommanderSettings.ShowPiPStrikeCam)
        {
            DisableCamera();
            return;
        }

        float now = Time.unscaledTime;

        // 1. If currently tracking a munition
        if (trackedMunition != null)
        {
            if (trackedMunition.disabled || !trackedMunition.gameObject.activeInHierarchy)
            {
                // Munition detonated or impacted -> linger at impact spot
                lastImpactPos = trackedMunition.transform.position;
                trackedMunition = null;
                lingerEndTime = now + LingerDurationSeconds;
            }
            else
            {
                EnsureCamera();
                if (pipCamera != null)
                {
                    pipCamera.enabled = true;
                    Vector3 forward = trackedMunition.transform.forward;
                    if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
                    pipCamera.transform.position = trackedMunition.transform.position - (forward * 14f) + (Vector3.up * 3.5f);
                    pipCamera.transform.rotation = Quaternion.LookRotation(forward + (Vector3.down * 0.05f), Vector3.up);
                }
                return;
            }
        }

        // 2. Lingering at impact point
        if (now < lingerEndTime)
        {
            if (pipCamera != null)
            {
                pipCamera.enabled = true;
                pipCamera.transform.position = lastImpactPos + (Vector3.up * 18f) - (Vector3.forward * 25f);
                pipCamera.transform.LookAt(lastImpactPos + (Vector3.up * 2f));
            }
            return;
        }

        // 3. Scan for active friendly missiles in flight (throttled every 0.8s)
        if (now >= nextScanTime)
        {
            nextScanTime = now + 0.8f;
            ScanForFriendlyStrike();
        }
        else if (trackedMunition == null)
        {
            DisableCamera();
        }
    }

    private void ScanForFriendlyStrike()
    {
        if (manualDismissed) return;

        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        if (localHq == null)
        {
            DisableCamera();
            return;
        }

        // Check UnitRegistry for active friendly missiles
        Unit? bestMissile = null;
        List<Unit>? all = UnitRegistry.allUnits;
        if (all != null)
        {
            for (int i = all.Count - 1; i >= 0; i--)
            {
                Unit u = all[i];
                if (u != null && !u.disabled && u is Missile m && CommanderGameAccess.IsFriendlyUnit(m, localHq))
                {
                    bestMissile = m;
                    break;
                }
            }
        }

        if (bestMissile != null)
        {
            trackedMunition = bestMissile;
            EnsureCamera();
        }
        else
        {
            DisableCamera();
        }
    }

    internal void TrackStrike(Unit munition)
    {
        if (munition == null || munition.disabled) return;
        trackedMunition = munition;
        manualDismissed = false;
        EnsureCamera();
    }

    private void EnsureCamera()
    {
        if (cameraObject == null)
        {
            cameraObject = new GameObject("Commander_StrikePipCamera");
            cameraObject.hideFlags = HideFlags.DontSave;
            pipCamera = cameraObject.AddComponent<Camera>();
            pipCamera.fieldOfView = 50f;
            pipCamera.nearClipPlane = 1f;
            pipCamera.farClipPlane = 25000f;
            pipCamera.clearFlags = CameraClearFlags.Skybox;
            pipCamera.enabled = false;
        }

        if (pipTexture == null || !pipTexture.IsCreated())
        {
            pipTexture = new RenderTexture(240, 144, 16, RenderTextureFormat.ARGB32);
            pipTexture.Create();
        }

        if (pipCamera != null && pipCamera.targetTexture != pipTexture)
        {
            pipCamera.targetTexture = pipTexture;
        }
    }

    private void DisableCamera()
    {
        if (pipCamera != null)
        {
            pipCamera.enabled = false;
        }
    }

    internal void Draw()
    {
        if (!IsActive || pipTexture == null || !pipTexture.IsCreated()) return;

        CommanderUiTheme.Ensure();
        float x = CommanderUiScale.Width - PiPWidth - 14f;
        float y = 54f;
        Rect pipRect = new(x, y, PiPWidth, PiPHeight);

        // Background panel & render texture
        GUI.Box(new Rect(pipRect.x - 2f, pipRect.y - 20f, pipRect.width + 4f, pipRect.height + 24f), string.Empty, CommanderUiTheme.Panel);
        GUI.DrawTexture(pipRect, pipTexture);

        // Sleek tactical frame & reticle
        CommanderUiTheme.DrawFrame(pipRect, 1.5f);

        // Header telemetry
        string targetLabel = trackedMunition != null ? (!string.IsNullOrEmpty(trackedMunition.unitName) ? trackedMunition.unitName : trackedMunition.name) : "DETONATION DETECTED";
        GUI.Label(new Rect(pipRect.x + 4f, pipRect.y - 18f, pipRect.width - 24f, 16f), $"STRIKE CAM | {targetLabel.ToUpperInvariant()}", CommanderUiTheme.MutedLabel);

        // Close button
        if (GUI.Button(new Rect(pipRect.xMax - 18f, pipRect.y - 19f, 16f, 16f), "X", CommanderUiTheme.Button))
        {
            manualDismissed = true;
            trackedMunition = null;
            lingerEndTime = 0f;
            DisableCamera();
        }

        // Tactical HUD reticle center
        Vector2 center = new(pipRect.x + pipRect.width * 0.5f, pipRect.y + pipRect.height * 0.5f);
        CommanderWorldMarkerRenderer.DrawTacticalReticle(center, new Color(0.2f, 0.95f, 0.5f, 0.85f), 12f);
    }

    internal void ResetSession()
    {
        trackedMunition = null;
        manualDismissed = false;
        lingerEndTime = 0f;
        DisableCamera();
        if (pipTexture != null)
        {
            pipTexture.Release();
            Object.Destroy(pipTexture);
            pipTexture = null;
        }
        if (cameraObject != null)
        {
            Object.Destroy(cameraObject);
            cameraObject = null;
            pipCamera = null;
        }
    }
}
