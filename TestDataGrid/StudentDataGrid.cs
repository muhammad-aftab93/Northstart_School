using EnterpriseUI.Grids;
using EnterpriseUI.WinForms;

namespace TestDataGrid;

public class StudentDataGrid : EnterpriseDataGrid<Student>
{
    public event Action<Student, Point>? RowContextMenuRequested;
    public event Action<Student>? EditRequested;

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            var screenPoint = PointToScreen(e.Location);
            var hit = AccessibilityObject.HitTest(screenPoint.X, screenPoint.Y);
            if (hit?.Role is AccessibleRole.Cell or AccessibleRole.Row)
            {
                hit.Select(AccessibleSelection.TakeSelection | AccessibleSelection.TakeFocus);
                if (TryGetFocusedStudent(out var student))
                    RowContextMenuRequested?.Invoke(student, e.Location);
                return;
            }
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        var screenPoint = PointToScreen(e.Location);
        var hit = AccessibilityObject.HitTest(screenPoint.X, screenPoint.Y);
        if (EditMode is GridEditMode.Form or GridEditMode.PopupModal && e.Button == MouseButtons.Left && hit?.Role is AccessibleRole.Cell or AccessibleRole.Row)
        {
            hit.Select(AccessibleSelection.TakeSelection | AccessibleSelection.TakeFocus);
            if (TryGetFocusedStudent(out var student))
            {
                EditRequested?.Invoke(student);
                return;
            }
        }
        base.OnMouseDoubleClick(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (TryGetFocusedStudent(out var student))
        {
            if (EditMode is GridEditMode.Form or GridEditMode.PopupModal && e.KeyCode is Keys.F2 or Keys.Enter)
            {
                e.Handled = e.SuppressKeyPress = true;
                EditRequested?.Invoke(student);
                return;
            }
            if (e.KeyCode == Keys.Apps || (e.Shift && e.KeyCode == Keys.F10))
            {
                e.Handled = e.SuppressKeyPress = true;
                var bounds = AccessibilityObject.GetFocused()?.Bounds ?? Rectangle.Empty;
                var location = bounds.IsEmpty ? new Point(24, 80) : PointToClient(new Point(bounds.Left, bounds.Bottom));
                RowContextMenuRequested?.Invoke(student, location);
                return;
            }
        }
        base.OnKeyDown(e);
    }

    private bool TryGetFocusedStudent(out Student student)
    {
        if (TryGetRowAt(Selection.FocusedRow, out var row) && row.Kind == GridRowKind.Data && row.Item != null)
        {
            student = row.Item;
            return true;
        }
        student = null!;
        return false;
    }
}