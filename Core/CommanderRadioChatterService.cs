using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace NuclearOptionCommander;

/// <summary>
/// Simulates realistic military tactical radio chatter in the ticker log and triggers
/// immersive radio bursts and squelches. Hooked into combat events (hits, destroyed units, SAM engagements, retreat).
/// </summary>
internal sealed class CommanderRadioChatterService
{
    internal static CommanderRadioChatterService? Instance { get; private set; }

    private readonly CommanderAudioCueService? audioCueService;
    private readonly HashSet<Unit> subscribedUnits = new();
    private readonly List<Unit> tempPruneList = new();

    private float lastChatterTime;
    private int callsignIndex;

    private static readonly string[] CallSignList = new[]
    {
        "Viper 1-1", "Hammer Actual", "Ironclad 2", "Ghost 4", "Archangel 3",
        "Warlock 1", "Titan 6", "Spectre 2-2", "Sabre 5", "Anvil Lead"
    };

    private static readonly string[] UnderFireCallouts = new[]
    {
        "Taking heavy fire! Requesting fire support!",
        "Contact, taking effective rounds! Return fire!",
        "Armor integrity compromised, breaking contact!",
        "Taking direct hits! Where's that air cover?!",
        "Multiple incoming rounds! Deploying smoke!"
    };

    private static readonly string[] UnitLostCallouts = new[]
    {
        "We lost {0}! Vehicle catastrophic kill!",
        "{0} is down! Ejecting, ejecting!",
        "Direct hit on {0}, signal lost!",
        "{0} is burning, all crew bail out!",
        "Catastrophic detonation on {0}!"
    };

    private static readonly string[] SamLockCallouts = new[]
    {
        "SAM radar spike on our nose! Break left!",
        "Mud spike, hostile air defense active!",
        "Defensive! SAM launch detected in sector!",
        "Missile launch warning! Popping chaff and flares!"
    };

    private static readonly string[] TargetDestroyedCallouts = new[]
    {
        "Good kill, good kill! Hostile asset eliminated.",
        "Splash one bandit! Target destroyed.",
        "Target down, clean direct impact confirmed.",
        "Hostile armor wiped out. Area neutralized."
    };

    internal CommanderRadioChatterService(CommanderAudioCueService? audioCueService = null)
    {
        Instance = this;
        this.audioCueService = audioCueService;
    }

    internal void Tick()
    {
        PruneDeadReferences();
        SubscribeFriendlyUnits();
    }

    private void SubscribeFriendlyUnits()
    {
        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        if (localHq == null || localHq.factionUnits == null) return;

        for (int i = 0; i < localHq.factionUnits.Count; i++)
        {
            if (localHq.factionUnits[i].TryGetUnit(out Unit unit) && unit != null && !unit.disabled)
            {
                if (subscribedUnits.Add(unit))
                {
                    unit.onDisableUnit += OnUnitDisabledCallback;
                }
            }
        }
    }

    private void OnUnitDisabledCallback(Unit unit)
    {
        if (unit == null) return;

        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        bool isFriendly = localHq != null && CommanderGameAccess.IsFriendlyUnit(unit, localHq);

        if (isFriendly)
        {
            PostLossChatter(unit);
        }
        else
        {
            PostKillChatter(unit);
        }
    }

    internal void ReportDamaged(Unit unit, DamageInfo info)
    {
        if (!CommanderSettings.RadioChatterEnabled) return;
        if (unit == null || unit.disabled) return;

        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        if (localHq == null || !CommanderGameAccess.IsFriendlyUnit(unit, localHq)) return;

        float totalDamage = info.pierceDamage.Value + info.blastDamage.Value + info.fireDamage.Value + info.impactDamage.Value;
        if (totalDamage < 15f) return; // Ignore small ricochets

        if (Time.unscaledTime - lastChatterTime < CommanderSettings.RadioChatterCooldownSeconds) return;

        lastChatterTime = Time.unscaledTime;
        string callsign = GetCallsign(unit);
        string quote = UnderFireCallouts[UnityEngine.Random.Range(0, UnderFireCallouts.Length)];

        CommanderAlertService.PostTickerEvent($"[RADIO] {callsign}: \"{quote}\"", new Color(1f, 0.65f, 0.2f, 0.95f));
    }

    internal void ReportSamSpike(string radarName, Vector3 position)
    {
        if (!CommanderSettings.RadioChatterEnabled) return;
        if (Time.unscaledTime - lastChatterTime < CommanderSettings.RadioChatterCooldownSeconds) return;

        lastChatterTime = Time.unscaledTime;
        string callsign = CallSignList[UnityEngine.Random.Range(0, CallSignList.Length)];
        string quote = SamLockCallouts[UnityEngine.Random.Range(0, SamLockCallouts.Length)];

        CommanderAlertService.PostTickerEvent($"[RADIO] {callsign}: \"{quote}\"", new Color(1f, 0.3f, 0.25f, 0.95f));
    }

    private void PostLossChatter(Unit unit)
    {
        if (!CommanderSettings.RadioChatterEnabled) return;
        if (Time.unscaledTime - lastChatterTime < CommanderSettings.RadioChatterCooldownSeconds) return;

        lastChatterTime = Time.unscaledTime;
        string unitName = !string.IsNullOrEmpty(unit.unitName) ? unit.unitName : "Lead Unit";
        string template = UnitLostCallouts[UnityEngine.Random.Range(0, UnitLostCallouts.Length)];
        string quote = string.Format(template, unitName);

        string callsign = CallSignList[UnityEngine.Random.Range(0, CallSignList.Length)];
        CommanderAlertService.PostTickerEvent($"[RADIO] {callsign}: \"{quote}\"", new Color(1f, 0.25f, 0.2f, 0.95f));
    }

    private void PostKillChatter(Unit unit)
    {
        if (!CommanderSettings.RadioChatterEnabled) return;
        if (Time.unscaledTime - lastChatterTime < CommanderSettings.RadioChatterCooldownSeconds) return;

        lastChatterTime = Time.unscaledTime;
        string quote = TargetDestroyedCallouts[UnityEngine.Random.Range(0, TargetDestroyedCallouts.Length)];
        string callsign = CallSignList[UnityEngine.Random.Range(0, CallSignList.Length)];

        CommanderAlertService.PostTickerEvent($"[RADIO] {callsign}: \"{quote}\"", new Color(0.4f, 0.9f, 0.45f, 0.95f));
    }

    private string GetCallsign(Unit unit)
    {
        if (!string.IsNullOrEmpty(unit.unitName))
        {
            return unit.unitName;
        }

        string sign = CallSignList[callsignIndex % CallSignList.Length];
        callsignIndex++;
        return sign;
    }

    private void PruneDeadReferences()
    {
        if (subscribedUnits.Count == 0) return;

        tempPruneList.Clear();
        foreach (Unit unit in subscribedUnits)
        {
            if (unit == null || unit.disabled)
            {
                if (unit != null)
                {
                    tempPruneList.Add(unit);
                }
            }
        }

        for (int i = 0; i < tempPruneList.Count; i++)
        {
            Unit u = tempPruneList[i];
            if (u != null)
            {
                try
                {
                    u.onDisableUnit -= OnUnitDisabledCallback;
                }
                catch { }
                subscribedUnits.Remove(u);
            }
        }
    }

    internal void ResetSession()
    {
        foreach (Unit unit in subscribedUnits)
        {
            if (unit != null)
            {
                try
                {
                    unit.onDisableUnit -= OnUnitDisabledCallback;
                }
                catch { }
            }
        }

        subscribedUnits.Clear();
        tempPruneList.Clear();
        lastChatterTime = 0f;
    }
}
