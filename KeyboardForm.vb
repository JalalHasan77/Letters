Imports System.Drawing
Imports System.Windows.Forms

Public Enum KeyCommand
    Deselect
    TurnRed
    TurnGreen
End Enum

''' <summary>
''' On-screen Arabic keyboard. Clicking a letter selects that cell on the board;
''' the three bottom keys de-select it or give it to a team.
''' The window does not take the focus, so the physical keyboard keeps working.
''' </summary>
Public Class KeyboardForm
    Inherits Form

    Public Event LetterClicked(label As String)
    Public Event CommandClicked(cmd As KeyCommand)
    Public Event KeyTyped(ch As Char)
    Public Event CommandKey(key As Keys)

    Private ReadOnly _keys As New Dictionary(Of String, Button)
    Private ReadOnly btnDeselect As Button, btnRed As Button, btnGreen As Button
    Private ReadOnly lblNumber As New Label With {.AutoSize = False, .TextAlign = ContentAlignment.MiddleCenter,
                                                  .Font = New Font("Tahoma", 13, FontStyle.Bold), .Height = 44, .Width = 150}
    Private _fontName As String = ArabicKeys.DefaultLetterFont()

    Private Const KeyW As Integer = 62, KeyH As Integer = 58, Gap As Integer = 6

    Public Sub New()
        Text = "Arabic Keyboard"
        FormBorderStyle = FormBorderStyle.FixedToolWindow
        ShowInTaskbar = False
        TopMost = True
        KeyPreview = True
        BackColor = Color.FromArgb(40, 40, 46)
        StartPosition = FormStartPosition.Manual

        Dim y = Gap
        Dim maxW = 0
        For r = 0 To ArabicKeys.Rows.Length - 1
            Dim row = ArabicKeys.Rows(r)
            Dim x = Gap + r * (KeyW \ 3)          ' stagger rows like a real keyboard
            For Each k In row
                Dim b = MakeKey(k, x, y, KeyW, KeyH)
                Dim lbl = k
                AddHandler b.Click, Sub() RaiseEvent LetterClicked(lbl)
                _keys(k) = b
                x += KeyW + Gap
            Next
            maxW = Math.Max(maxW, x)
            y += KeyH + Gap
        Next

        ' bottom row: the three command keys
        Dim cmdW = (maxW - Gap - 4 * Gap - 150) \ 3
        btnDeselect = MakeKey("De-select", Gap, y, cmdW, KeyH)
        btnRed = MakeKey("Turn to Red", Gap + (cmdW + Gap), y, cmdW, KeyH)
        btnGreen = MakeKey("Turn to Green", Gap + 2 * (cmdW + Gap), y, cmdW, KeyH)
        For Each b In {btnDeselect, btnRed, btnGreen}
            b.Font = New Font("Tahoma", 12, FontStyle.Bold)
        Next
        btnDeselect.BackColor = Color.FromArgb(90, 90, 98)
        AddHandler btnDeselect.Click, Sub() RaiseEvent CommandClicked(KeyCommand.Deselect)
        AddHandler btnRed.Click, Sub() RaiseEvent CommandClicked(KeyCommand.TurnRed)
        AddHandler btnGreen.Click, Sub() RaiseEvent CommandClicked(KeyCommand.TurnGreen)
        lblNumber.Location = New Point(Gap + 3 * (cmdW + Gap) + Gap, y + (KeyH - lblNumber.Height) \ 2)
        lblNumber.ForeColor = Color.White
        Controls.Add(lblNumber)
        ClientSize = New Size(Math.Max(maxW, lblNumber.Right + Gap), y + KeyH + Gap)
    End Sub

    Private Function MakeKey(text As String, x As Integer, y As Integer, w As Integer, h As Integer) As Button
        Dim b As New Button With {.Text = ArabicKeys.Display(text), .Location = New Point(x, y), .Size = New Size(w, h),
            .FlatStyle = FlatStyle.Flat, .BackColor = Color.FromArgb(70, 70, 78), .ForeColor = Color.White,
            .Font = New Font(_fontName, 20, FontStyle.Bold), .TabStop = False, .UseMnemonic = False}
        b.FlatAppearance.BorderColor = Color.FromArgb(20, 20, 24)
        Controls.Add(b)
        Return b
    End Function

    ''' <summary>Letter font for the keys (the same font as the board).</summary>
    Public Sub SetLetterFont(name As String)
        _fontName = name
        For Each b In _keys.Values
            Dim old = b.Font
            b.Font = New Font(name, 20, FontStyle.Bold)
            old.Dispose()
        Next
    End Sub

    ''' <summary>
    ''' Colours the keys to match the board: keys with no cell are dimmed, keys for owned
    ''' cells take the team colour, and the selected letter is highlighted.
    ''' </summary>
    Public Sub UpdateKeys(b As Board, owner As Integer(), selected As Integer, redTeam As Integer, greenTeam As Integer,
                          blinkColor As Color, numberBuffer As String)
        Dim byLabel As New Dictionary(Of String, List(Of Integer))
        For Each c In b.Cells
            If c.Label = "" Then Continue For
            If Not byLabel.ContainsKey(c.Label) Then byLabel(c.Label) = New List(Of Integer)
            byLabel(c.Label).Add(c.Index)
        Next
        Dim hasNumbers = b.Cells.Any(Function(c) c.Label <> "" AndAlso Char.IsDigit(c.Label(0)))
        Dim selLabel = If(selected >= 0, b.Cells(selected).Label, "")
        For Each kv In _keys
            Dim k = kv.Key, btn = kv.Value
            Dim cells As List(Of Integer) = Nothing
            If Not byLabel.TryGetValue(k, cells) AndAlso Char.IsDigit(k(0)) AndAlso hasNumbers Then
                ' digit used only as part of longer numbers (board 4 bars)
                btn.Enabled = True
                btn.BackColor = Color.FromArgb(70, 70, 78)
                Continue For
            End If
            If cells Is Nothing Then
                btn.Enabled = False
                btn.BackColor = Color.FromArgb(50, 50, 56)
                Continue For
            End If
            btn.Enabled = True
            Dim owners = cells.Select(Function(i) owner(i)).Distinct().ToList()
            If k = selLabel Then
                btn.BackColor = blinkColor
            ElseIf owners.Count = 1 AndAlso owners(0) >= 0 Then
                btn.BackColor = b.Teams(owners(0)).Color
            Else
                btn.BackColor = Color.FromArgb(70, 70, 78)
            End If
        Next
        btnRed.Text = $"Turn to {b.Teams(redTeam).Name}" : btnRed.BackColor = b.Teams(redTeam).Color
        btnGreen.Text = $"Turn to {b.Teams(greenTeam).Name}" : btnGreen.BackColor = b.Teams(greenTeam).Color
        Dim canColour = selected >= 0
        btnRed.Enabled = canColour : btnGreen.Enabled = canColour : btnDeselect.Enabled = canColour
        lblNumber.Text = If(numberBuffer <> "", "No. " & numberBuffer, If(selLabel <> "", "Selected: " & ArabicKeys.Display(selLabel), ""))
    End Sub

    ' ---- do not steal the focus from the board or the control panel
    Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
        Get
            Return True
        End Get
    End Property

    Protected Overrides ReadOnly Property CreateParams As CreateParams
        Get
            Const WS_EX_NOACTIVATE As Integer = &H8000000
            Dim cp = MyBase.CreateParams
            cp.ExStyle = cp.ExStyle Or WS_EX_NOACTIVATE
            Return cp
        End Get
    End Property

    Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
        MyBase.OnKeyDown(e)
        RaiseEvent CommandKey(e.KeyCode)
    End Sub

    Protected Overrides Sub OnKeyPress(e As KeyPressEventArgs)
        MyBase.OnKeyPress(e)
        RaiseEvent KeyTyped(e.KeyChar)
        e.Handled = True
    End Sub

    Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
        If e.CloseReason = CloseReason.UserClosing Then
            e.Cancel = True
            Hide()
        End If
        MyBase.OnFormClosing(e)
    End Sub
End Class
