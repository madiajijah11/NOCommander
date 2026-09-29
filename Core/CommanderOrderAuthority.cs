using System.Collections.Generic;
using NuclearOption.Networking;
using UnityEngine;

namespace NuclearOptionCommander;

internal enum CommanderOrderKind
{
    Move,
    Attack,
    Hold,
    Retreat,
}

internal enum CommanderOrderResult
{
    Accepted,
    NotServer,
    InvalidSender,
    InvalidUnit,
    Duplicate,
    Stale,
}

internal readonly struct CommanderOrderEnvelope
{
    internal readonly uint CommandId;
    internal readonly int SessionToken;
    internal readonly CommanderOrderKind Kind;
    internal readonly Unit Unit;
    internal readonly GlobalPosition Destination;
    internal readonly float CreatedAt;

    internal CommanderOrderEnvelope(
        uint commandId,
        int sessionToken,
        CommanderOrderKind kind,
        Unit unit,
        GlobalPosition destination,
        float createdAt)
    {
        CommandId = commandId;
        SessionToken = sessionToken;
        Kind = kind;
        Unit = unit;
        Destination = destination;
        CreatedAt = createdAt;
    }
}

internal sealed class CommanderOrderAuthority
{
    private const int AcceptedIdCapacity = 256;
    private const float MaxOrderAgeSeconds = 10f;
    private static int sessionCounter;
    private readonly HashSet<uint> acceptedIds = new();
    private readonly Queue<uint> acceptedIdOrder = new();
    private uint nextCommandId;
    private int currentSessionToken;

    internal int CurrentSessionToken => currentSessionToken;

    internal void BeginSession()
    {
        ResetSession();
        int token = unchecked(Time.frameCount + ++sessionCounter);
        currentSessionToken = token == 0 ? 1 : token;
    }

    internal void ResetSession()
    {
        acceptedIds.Clear();
        acceptedIdOrder.Clear();
        nextCommandId = 0;
        currentSessionToken = 0;
    }

    internal uint NextCommandId()
    {
        nextCommandId++;
        if (nextCommandId == 0)
        {
            nextCommandId = 1;
        }

        return nextCommandId;
    }

    internal bool TryAccept(CommanderOrderEnvelope envelope, out CommanderOrderResult result)
    {
        if (NetworkManagerNuclearOption.i == null || !NetworkManagerNuclearOption.i.Server.Active)
        {
            result = CommanderOrderResult.NotServer;
            return false;
        }

        if (envelope.CommandId == 0 || currentSessionToken == 0 || envelope.SessionToken != currentSessionToken)
        {
            result = CommanderOrderResult.InvalidSender;
            return false;
        }

        if (envelope.Unit == null)
        {
            result = CommanderOrderResult.InvalidUnit;
            return false;
        }

        if (envelope.Unit.disabled)
        {
            result = CommanderOrderResult.InvalidUnit;
            return false;
        }

        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        if (localHq == null || !CommanderGameAccess.IsFriendlyUnit(envelope.Unit, localHq))
        {
            result = CommanderOrderResult.InvalidSender;
            return false;
        }

        if (acceptedIds.Contains(envelope.CommandId))
        {
            result = CommanderOrderResult.Duplicate;
            return false;
        }

        float now = Time.unscaledTime;
        if (float.IsNaN(envelope.CreatedAt)
            || float.IsInfinity(envelope.CreatedAt)
            || envelope.CreatedAt > now
            || envelope.CreatedAt < now - MaxOrderAgeSeconds)
        {
            result = CommanderOrderResult.Stale;
            return false;
        }

        acceptedIds.Add(envelope.CommandId);
        acceptedIdOrder.Enqueue(envelope.CommandId);
        if (acceptedIdOrder.Count > AcceptedIdCapacity)
        {
            acceptedIds.Remove(acceptedIdOrder.Dequeue());
        }

        result = CommanderOrderResult.Accepted;
        return true;
    }
}
