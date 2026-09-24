using UnityEngine;
using System.Collections.Generic;

namespace NuclearOptionCommander;

internal sealed class CommanderPovCrewUi
{
    private const int PovWindowId = 0x434F5056;
    private const float Height = 34f;
    private const float LabelWidth = 76f;
    private const float ButtonWidth = 86f;
    private const float Padding = 4f;
    private const float BottomMargin = 74f;

    private readonly CommanderCameraFollowService cameraFollowService;
    private Rect menuRect;
    private bool positionInitialized;

    internal static CommanderPovCrewUi? Instance { get; private set; }

    internal CommanderPovCrewUi(CommanderCameraFollowService cameraFollowService)
    {
        this.cameraFollowService = cameraFollowService;
        Instance = this;
    }

    internal void ResetPosition()
    {
        positionInitialized = false;
    }

    internal bool ContainsScreenPoint(Vector2 screenPoint)
    {
        return IsVisible && menuRect.Contains(CommanderUiScale.ScreenToGui(screenPoint));
    }

    internal void Draw()
    {
        IReadOnlyList<PovCrewSeat> seats = cameraFollowService.PovCrewSeats;
        if (!cameraFollowService.PovMode || seats.Count == 0)
        {
            menuRect = Rect.zero;
            return;
        }

        CommanderUiTheme.Ensure();
        float width = LabelWidth + seats.Count * ButtonWidth + Padding * 2f;
        if (!positionInitialized)
        {
            menuRect = new Rect(
                (CommanderUiScale.Width - width) * 0.5f,
                CommanderUiScale.Height - BottomMargin - Height,
                width,
                Height);
            positionInitialized = true;
        }
        else
        {
            menuRect.width = width;
            menuRect.height = Height;
            menuRect = CommanderUiTheme.ClampWindow(menuRect, 6f);
        }

        menuRect = GUI.Window(PovWindowId, menuRect, DrawPovWindow, string.Empty, CommanderUiTheme.Panel);
    }

    private void DrawPovWindow(int windowId)
    {
        IReadOnlyList<PovCrewSeat> seats = cameraFollowService.PovCrewSeats;
        CommanderUiTheme.DrawMutedFrame(new Rect(0f, 0f, menuRect.width, menuRect.height));
        GUI.Label(
            new Rect(Padding + 4f, 3f, LabelWidth - 8f, Height - 6f),
            "POV SEAT",
            CommanderUiTheme.MutedLabel);

        for (int i = 0; i < seats.Count; i++)
        {
            PovCrewSeat seat = seats[i];
            bool oldEnabled = GUI.enabled;
            GUI.enabled = seat.IsAvailable;
            Rect buttonRect = new(
                Padding + LabelWidth + i * ButtonWidth,
                4f,
                ButtonWidth - 4f,
                Height - 8f);
            GUIStyle style = cameraFollowService.PovCrewIndex == i
                ? CommanderUiTheme.SelectedButton
                : CommanderUiTheme.Button;
            if (GUI.Button(buttonRect, seat.Label, style))
            {
                cameraFollowService.TryMoveToCrewPosition(i);
            }
            GUI.enabled = oldEnabled;
        }

        GUI.DragWindow(new Rect(0f, 0f, menuRect.width, menuRect.height));
    }

    private bool IsVisible
    {
        get
        {
            return cameraFollowService.PovMode
                && cameraFollowService.PovCrewSeats.Count > 0;
        }
    }
}
