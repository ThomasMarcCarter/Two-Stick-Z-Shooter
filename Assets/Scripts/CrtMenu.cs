using UnityEngine;
using UnityEngine.InputSystem;

// Selection for CrtButtons laid out in one or more columns. Up/down (stick, d-pad or arrow keys)
// moves within a column, left/right moves between columns, hovering with the mouse moves the
// highlight too, and Submit (A / Enter) or a click activates it. With a controller a button
// starts highlighted so there's always a selection.
// Buttons are numbered column by column: all of the first column, then the next, and so on.
public class CrtMenu
{
    private const float FirstRepeatDelay = 0.35f;
    private const float RepeatDelay = 0.15f;

    private readonly CrtButton[][] columns;
    private readonly int startColumn;
    private readonly int startRow;
    private int column = -1;
    private int row;
    private float nextMove;
    private bool held;

    // A single column.
    public CrtMenu(params CrtButton[] buttons) : this(new[] { buttons }, 0, 0)
    {
    }

    // startColumn/startRow: where the controller highlight begins when the menu opens.
    public CrtMenu(CrtButton[][] menuColumns, int firstColumn, int firstRow)
    {
        columns = menuColumns;
        startColumn = firstColumn;
        startRow = firstRow;
    }

    public void Open()
    {
        column = GameInput.UsingGamepad ? startColumn : -1;
        row = startRow;

        // Ignore a stick or d-pad already held when the menu appears.
        held = true;
        nextMove = Time.unscaledTime + FirstRepeatDelay;
        Refresh();
    }

    // Call every frame while the menu is up. Returns the number of the button activated this
    // frame (counting column by column), or -1.
    public int Update()
    {
        int hoveredColumn = -1;
        int hoveredRow = -1;

        if (!GameInput.UsingGamepad)
        {
            for (int c = 0; c < columns.Length; c++)
            {
                for (int r = 0; r < columns[c].Length; r++)
                {
                    if (columns[c][r].IsPointerOver())
                    {
                        hoveredColumn = c;
                        hoveredRow = r;
                    }
                }
            }

            if (GameInput.PointerMovedThisFrame || hoveredColumn >= 0)
            {
                column = hoveredColumn;
                row = Mathf.Max(0, hoveredRow);
            }
        }

        Step(GameInput.Navigate.ReadValue<Vector2>());

        if (GameInput.UsingGamepad && column < 0)
        {
            column = startColumn;
            row = startRow;
        }

        Refresh();

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && hoveredColumn >= 0)
            return Number(hoveredColumn, hoveredRow);

        if (GameInput.Submit.WasPressedThisFrame() && column >= 0)
            return Number(column, row);

        return -1;
    }

    // Move the highlight with a held stick, repeating like a key held on a keyboard.
    private void Step(Vector2 input)
    {
        if (input.magnitude < 0.5f)
        {
            held = false;
            return;
        }

        float now = Time.unscaledTime;

        if (held && now < nextMove)
            return;

        nextMove = now + (held ? RepeatDelay : FirstRepeatDelay);
        held = true;

        if (column < 0)
        {
            column = startColumn;
            row = startRow;
            return;
        }

        if (Mathf.Abs(input.y) >= Mathf.Abs(input.x))
        {
            int length = columns[column].Length;
            int direction = input.y > 0f ? -1 : 1;   // up the screen is earlier in the column
            row = (row + direction + length) % length;
        }
        else
        {
            column = Mathf.Clamp(column + (input.x > 0f ? 1 : -1), 0, columns.Length - 1);
            row = Mathf.Min(row, columns[column].Length - 1);
        }
    }

    private int Number(int c, int r)
    {
        int number = r;

        for (int i = 0; i < c; i++)
            number += columns[i].Length;

        return number;
    }

    private void Refresh()
    {
        for (int c = 0; c < columns.Length; c++)
        {
            for (int r = 0; r < columns[c].Length; r++)
                columns[c][r].SetHighlighted(c == column && r == row);
        }
    }
}
