Imports System.Drawing
Imports System.Runtime.InteropServices
Imports System.Windows.Forms

''' <summary>
''' The board window, shown to the audience (for example on a TV or projector).
''' It keeps a 16:9 shape; F11 or a double-click switches full screen on and off.
''' </summary>
Public Class BoardForm
    Inherits Form

    Public ReadOnly Property View As New BoardView With {.Dock = DockStyle.Fill}

    ''' <summary>Raised for every character typed while this window is active.</summary>
    Public Event KeyTyped(ch As Char)
    Public Event CommandKey(key As Keys)

    Private _savedBounds As Rectangle
    Private _fullScreen As Boolean

    Public Sub New()
        Text = "Letters Contest – Board"
        ClientSize = New Size(1280, 720)
        StartPosition = FormStartPosition.Manual
        Location = New Point(40, 40)
        BackColor = Color.Black
        KeyPreview = True
        Controls.Add(View)
        AddHandler View.DoubleClick, Sub() ToggleFullScreen()
    End Sub

    Public Sub ToggleFullScreen()
        If _fullScreen Then
            FormBorderStyle = FormBorderStyle.Sizable
            WindowState = FormWindowState.Normal
            Bounds = _savedBounds
        Else
            _savedBounds = Bounds
            FormBorderStyle = FormBorderStyle.None
            WindowState = FormWindowState.Normal
            Bounds = Screen.FromControl(Me).Bounds
        End If
        _fullScreen = Not _fullScreen
    End Sub

    Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
        MyBase.OnKeyDown(e)
        If e.KeyCode = Keys.F11 Then
            ToggleFullScreen() : e.Handled = True
        ElseIf e.KeyCode = Keys.Escape AndAlso _fullScreen AndAlso e.Shift Then
            ToggleFullScreen() : e.Handled = True
        Else
            RaiseEvent CommandKey(e.KeyCode)
        End If
    End Sub

    Protected Overrides Sub OnKeyPress(e As KeyPressEventArgs)
        MyBase.OnKeyPress(e)
        RaiseEvent KeyTyped(e.KeyChar)
        e.Handled = True
    End Sub

    ' The board window is only hidden when closed, so it can be shown again.
    Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
        If e.CloseReason = CloseReason.UserClosing Then
            e.Cancel = True
            Hide()
        End If
        MyBase.OnFormClosing(e)
    End Sub

    ' ---- keep the window 16:9 while the user drags its edges (Windows only)
    Private Const WM_SIZING As Integer = &H214
    Private Const WMSZ_LEFT As Integer = 1, WMSZ_RIGHT As Integer = 2, WMSZ_TOP As Integer = 3, WMSZ_BOTTOM As Integer = 6

    <StructLayout(LayoutKind.Sequential)>
    Private Structure RECT
        Public Left, Top, Right, Bottom As Integer
    End Structure

    Protected Overrides Sub WndProc(ByRef m As Message)
        If m.Msg = WM_SIZING AndAlso Not _fullScreen Then
            Dim r = Marshal.PtrToStructure(Of RECT)(m.LParam)
            Dim extraW = Width - ClientSize.Width, extraH = Height - ClientSize.Height
            Dim edge = m.WParam.ToInt32()
            Dim cw = r.Right - r.Left - extraW, ch = r.Bottom - r.Top - extraH
            If edge = WMSZ_LEFT OrElse edge = WMSZ_RIGHT Then
                r.Bottom = r.Top + CInt(cw * 9 / 16) + extraH
            Else
                Dim newW = CInt(ch * 16 / 9) + extraW
                If edge = WMSZ_TOP OrElse edge = WMSZ_BOTTOM Then
                    r.Right = r.Left + newW
                ElseIf edge = WMSZ_LEFT + WMSZ_TOP OrElse edge = WMSZ_LEFT + WMSZ_BOTTOM Then
                    r.Left = r.Right - newW
                Else
                    r.Right = r.Left + newW
                End If
            End If
            Marshal.StructureToPtr(r, m.LParam, False)
            m.Result = New IntPtr(1)
            Return
        End If
        MyBase.WndProc(m)
    End Sub
End Class
