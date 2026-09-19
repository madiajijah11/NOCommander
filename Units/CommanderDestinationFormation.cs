using NuclearOption.Networking;
using UnityEngine;

namespace NuclearOptionCommander;

internal enum FormationShape
{
    Ring = 0,
    Line = 1,
    Column = 2,
    Wedge = 3,
    Box = 4,
    Echelon = 5
}

internal static class CommanderDestinationFormation
{
    private const int SlotsPerRing = 8;

    internal static GlobalPosition ApplyOffset(
        GlobalPosition center,
        int slotIndex,
        float spacing,
        FormationShape shape = FormationShape.Ring,
        Vector3? forwardDirection = null)
    {
        if (slotIndex <= 0 || spacing <= 0f)
        {
            return center;
        }

        Vector3 fwd = forwardDirection.HasValue && forwardDirection.Value.sqrMagnitude > 0.01f
            ? forwardDirection.Value.normalized
            : Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
        if (right.sqrMagnitude < 0.01f)
        {
            right = Vector3.right;
        }

        Vector3 offset;

        switch (shape)
        {
            case FormationShape.Line:
            {
                // Alternating left and right along the right-vector
                int side = (slotIndex % 2 == 1) ? 1 : -1;
                int rank = (slotIndex + 1) / 2;
                offset = right * (side * rank * spacing);
                break;
            }

            case FormationShape.Column:
            {
                // Trailing behind center along -fwd
                offset = -fwd * (slotIndex * spacing);
                break;
            }

            case FormationShape.Wedge:
            {
                // V-formation: stepped back and outward
                int side = (slotIndex % 2 == 1) ? 1 : -1;
                int tier = (slotIndex + 1) / 2;
                offset = (right * (side * tier * spacing)) - (fwd * (tier * spacing * 0.85f));
                break;
            }

            case FormationShape.Box:
            {
                // 3 columns grid
                int col = slotIndex % 3 - 1; // -1, 0, 1
                int row = slotIndex / 3;
                offset = (right * (col * spacing)) - (fwd * (row * spacing));
                break;
            }

            case FormationShape.Echelon:
            {
                // Diagonal stepped to the right
                offset = (right * (slotIndex * spacing * 0.9f)) - (fwd * (slotIndex * spacing * 0.75f));
                break;
            }

            case FormationShape.Ring:
            default:
            {
                int zeroBasedSlot = slotIndex - 1;
                int ring = zeroBasedSlot / SlotsPerRing + 1;
                int slotInRing = zeroBasedSlot % SlotsPerRing;
                float angle = slotInRing * (360f / SlotsPerRing);
                if ((ring & 1) == 0)
                {
                    angle += 360f / (SlotsPerRing * 2f);
                }

                offset = Quaternion.Euler(0f, angle, 0f) * fwd * (spacing * ring);
                break;
            }
        }

        return center + offset;
    }
}
