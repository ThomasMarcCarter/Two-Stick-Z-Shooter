using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The SettingsMenu's input: rebinding a control, and moving the selection with the pointer,
// the keyboard or a gamepad.
public partial class SettingsMenu
{
    // ---------- Rebinding ----------

    private void BeginRebind(Cell cell)
    {
        pendingRebind = null;

        rebinding = GameInput.StartRebind(cell.Action, cell.BindingIndex, () =>
        {
            rebinding = null;
            ignoreCancelUntilFrame = Time.frameCount + 1;   // the Esc that cancelled shouldn't also close the menu
            status.text = "";
            RefreshLabels();
        });
    }

    // ---------- Selection ----------

    private Cell HandlePointer()
    {
        if (GameInput.UsingGamepad)
            return null;

        Cell hovered = null;
        hoverArrow = 0;

        for (int r = 0; r < grid.Count; r++)
        {
            for (int c = 0; c < grid[r].Length; c++)
            {
                Cell cell = grid[r][c];
                int arrow = cell.Left != null && cell.Left.IsPointerOver() ? -1
                    : cell.Right != null && cell.Right.IsPointerOver() ? 1 : 0;

                if (arrow != 0 || (cell.Button != null && cell.Button.IsPointerOver()))
                {
                    hovered = cell;
                    hoverArrow = arrow;

                    if (GameInput.PointerMovedThisFrame)
                    {
                        row = r;
                        column = c;
                    }
                }
            }
        }

        if (hovered == null && GameInput.PointerMovedThisFrame)
            row = -1;

        return hovered;
    }

    private void HandleNavigation()
    {
        Vector2 input = GameInput.Navigate.ReadValue<Vector2>();

        if (input.magnitude < 0.5f)
        {
            held = false;

            if (GameInput.UsingGamepad && row < 0)
                row = 0;

            return;
        }

        float now = Time.unscaledTime;

        if (held && now < nextMove)
            return;

        nextMove = now + (held ? 0.15f : 0.35f);
        held = true;

        if (row < 0)
        {
            row = 0;
            return;
        }

        if (Mathf.Abs(input.y) >= Mathf.Abs(input.x))
        {
            row = (row + (input.y > 0f ? -1 : 1) + grid.Count) % grid.Count;
        }
        else
        {
            Cell current = grid[row][Mathf.Min(column, grid[row].Length - 1)];

            // Left / right on an option changes it rather than moving across.
            if (current.Step != null)
            {
                current.Step(input.x > 0f ? 1 : -1);
                RefreshLabels();
                return;
            }

            column = Mathf.Clamp(column + (input.x > 0f ? 1 : -1), 0, 1);
        }

        // Land on a cell that can be picked; fixed cells and single-button rows fall back.
        column = Mathf.Min(column, grid[row].Length - 1);

        if (grid[row][column].Button == null)
            column = grid[row].Length > 1 && grid[row][1 - column].Button != null ? 1 - column : column;
    }

    private Cell Selected()
    {
        if (row < 0 || row >= grid.Count)
            return null;

        Cell cell = grid[row][Mathf.Min(column, grid[row].Length - 1)];
        return cell.Button != null ? cell : null;
    }

    private void Highlight()
    {
        Cell selected = Selected();

        foreach (Cell[] cells in grid)
        {
            foreach (Cell cell in cells)
            {
                // The open page's tab stays lit, unless the cursor is on the tab row choosing another.
                bool openTab = cell.Tab == page && row != 0;

                if (cell.Button != null)
                    cell.Button.SetHighlighted(cell == selected || openTab);

                if (cell.Left != null)
                {
                    cell.Left.SetHighlighted(cell == selected && hoverArrow == -1);
                    cell.Right.SetHighlighted(cell == selected && hoverArrow == 1);
                }
            }
        }
    }
}
