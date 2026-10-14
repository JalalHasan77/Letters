Imports System.Drawing
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms

''' <summary>
''' Control panel: pick a board, run the contest, calculate win chances.
''' The board itself is shown in a separate 16:9 window (BoardForm) and letters can be
''' chosen with the physical keyboard or the on-screen Arabic keyboard (KeyboardForm).
''' </summary>
Public Class MainForm
    Inherits Form

    ' ---- state
    Private _board As Board
    Private _owner As Integer()
    Private ReadOnly _history As New Stack(Of (Cell As Integer, Previous As Integer))
    Private _gameOver As Boolean
    Private ReadOnly _rnd As New Random()
    Private _cts As CancellationTokenSource

    ' Font used for the whole window (the same family as the cell letters by default)
    Private Const UiFontName As String = "Tahoma"
    Private _switchingModel As Boolean

    ' ---- windows
    Private ReadOnly boardWin As New BoardForm()
    Private ReadOnly keyboardWin As New KeyboardForm()
    Private ReadOnly view As BoardView = boardWin.View
    Private ReadOnly blinkTimer As New System.Windows.Forms.Timer With {.Interval = 450}
    Private ReadOnly numberTimer As New System.Windows.Forms.Timer With {.Interval = 1500}
    Private _numberBuffer As String = ""
    Private _redTeam As Integer = 0, _greenTeam As Integer = 1

    ' ---- controls
    Private ReadOnly btnShowBoard As New Button With {.Text = "Show board", .AutoSize = True}
    Private ReadOnly btnFullScreen As New Button With {.Text = "Board full screen", .AutoSize = True}
    Private ReadOnly btnKeyboard As New Button With {.Text = "Show Arabic keyboard", .AutoSize = True}
    Private ReadOnly btnDeselect As New Button With {.Text = "De-select", .AutoSize = True}
    Private ReadOnly btnTurnRed As New Button With {.Text = "Turn to Red", .AutoSize = True, .ForeColor = Color.White}
    Private ReadOnly btnTurnGreen As New Button With {.Text = "Turn to Green", .AutoSize = True, .ForeColor = Color.White}
    Private ReadOnly cboModel As New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList, .Width = 320}
    Private ReadOnly cboPose As New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList, .Width = 200}
    Private ReadOnly cboFont As New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList, .Width = 170}
    Private ReadOnly chkBold As New CheckBox With {.Text = "Bold", .AutoSize = True, .Margin = New Padding(8, 6, 0, 0)}
    Private ReadOnly lblDesc As New Label With {.AutoSize = True, .MaximumSize = New Size(320, 0), .ForeColor = Color.FromArgb(92, 90, 85)}
    Private ReadOnly lblTeam0 As New Label With {.AutoSize = True, .Font = New Font(UiFontName, 11, FontStyle.Bold)}
    Private ReadOnly lblTeam1 As New Label With {.AutoSize = True, .Font = New Font(UiFontName, 11, FontStyle.Bold)}
    Private ReadOnly lblStatus As New Label With {.AutoSize = True, .MaximumSize = New Size(320, 0), .Font = New Font(UiFontName, 13, FontStyle.Bold)}
    Private ReadOnly lblHelp As New Label With {.AutoSize = True, .MaximumSize = New Size(320, 0), .ForeColor = Color.FromArgb(92, 90, 85)}
    Private ReadOnly btnPick As New Button With {.Text = "Pick random cell", .AutoSize = True}
    Private ReadOnly btnUndo As New Button With {.Text = "Undo", .AutoSize = True}
    Private ReadOnly btnNew As New Button With {.Text = "New game", .AutoSize = True}
    Private ReadOnly chkLabels As New CheckBox With {.Text = "Show letters on cells", .Checked = True, .AutoSize = True}
    Private ReadOnly btnAuto As New Button With {.Text = "Auto-play demo", .AutoSize = True}
    Private ReadOnly nudSpeed As New NumericUpDown With {.Minimum = 50, .Maximum = 3000, .Value = 400, .Increment = 50, .Width = 70}
    Private ReadOnly autoTimer As New System.Windows.Forms.Timer()

    Private ReadOnly lblP As New Label With {.AutoSize = True}
    Private ReadOnly nudP As New NumericUpDown With {.Minimum = 1, .Maximum = 99, .Value = 50, .Width = 70}
    Private ReadOnly nudGames As New NumericUpDown With {.Minimum = 1000, .Maximum = 100000000, .Value = 1000000, .Increment = 100000, .Width = 110, .ThousandsSeparator = True}
    Private ReadOnly chkExact As New CheckBox With {.Text = "Exact count when possible (28 cells or fewer)", .Checked = True, .AutoSize = True}
    Private ReadOnly btnCalc As New Button With {.Text = "Calculate win chances", .AutoSize = True}
    Private ReadOnly btnCancel As New Button With {.Text = "Cancel", .AutoSize = True, .Enabled = False}
    Private ReadOnly bar As New ProgressBar With {.Width = 320, .Height = 14, .Maximum = 1000}
    Private ReadOnly lblRes0 As New Label With {.AutoSize = True, .Font = New Font(UiFontName, 11, FontStyle.Bold)}
    Private ReadOnly lblRes1 As New Label With {.AutoSize = True, .Font = New Font(UiFontName, 11, FontStyle.Bold)}
    Private ReadOnly lblResNone As New Label With {.AutoSize = True, .Font = New Font(UiFontName, 11, FontStyle.Bold), .ForeColor = Color.FromArgb(120, 116, 108)}
    Private ReadOnly lblResMethod As New Label With {.AutoSize = True, .MaximumSize = New Size(320, 0), .ForeColor = Color.FromArgb(92, 90, 85), .Margin = New Padding(0, 4, 0, 24)}

    Public Sub New()
        Text = "Letters Contest – Control Panel"
        Size = New Size(410, 900)
        MinimumSize = New Size(410, 500)
        StartPosition = FormStartPosition.Manual
        KeyPreview = True
        Font = New Font(UiFontName, 9.5F)
        BackColor = Color.FromArgb(251, 248, 241)
        BuildLayout()
        cboModel.Items.AddRange(BoardFactory.ModelNames)
        cboPose.Items.AddRange({"Horizontal (flat top)", "Vertical (pointy top)"})
        For Each ff In FontFamily.Families
            cboFont.Items.Add(ff.Name)
        Next
        Dim startFont = ArabicKeys.DefaultLetterFont()
        If Not cboFont.Items.Contains(startFont) Then startFont = "Arial"
        cboFont.SelectedItem = startFont
        If cboFont.SelectedIndex < 0 AndAlso cboFont.Items.Count > 0 Then cboFont.SelectedIndex = 0
        If cboFont.SelectedItem IsNot Nothing Then view.LabelFontName = CStr(cboFont.SelectedItem)
        AddHandler cboModel.SelectedIndexChanged, Sub() OnModelChanged()
        AddHandler cboPose.SelectedIndexChanged, Sub()
                                                     If Not _switchingModel Then LoadModel(cboModel.SelectedIndex)
                                                 End Sub
        AddHandler cboFont.SelectedIndexChanged, Sub()
                                                     If cboFont.SelectedItem IsNot Nothing Then
                                                         view.LabelFontName = CStr(cboFont.SelectedItem)
                                                         keyboardWin.SetLetterFont(view.LabelFontName)
                                                     End If
                                                     view.Invalidate()
                                                 End Sub
        AddHandler chkBold.CheckedChanged, Sub()
                                               view.LabelBold = chkBold.Checked
                                               view.Invalidate()
                                           End Sub
        AddHandler view.CellClicked, AddressOf OnCellClicked
        AddHandler btnNew.Click, Sub() LoadModel(cboModel.SelectedIndex)
        AddHandler btnUndo.Click, Sub() Undo()
        AddHandler btnPick.Click, Sub() PickRandomCell()
        AddHandler chkLabels.CheckedChanged, Sub()
                                                 view.ShowLabels = chkLabels.Checked
                                                 view.Invalidate()
                                             End Sub
        AddHandler btnAuto.Click, Sub() ToggleAuto()
        AddHandler autoTimer.Tick, AddressOf AutoStep
        AddHandler btnCalc.Click, AddressOf Calculate
        AddHandler btnCancel.Click, Sub() _cts?.Cancel()

        ' board window and keyboard
        AddHandler btnShowBoard.Click, Sub()
                                           boardWin.Show()
                                           boardWin.BringToFront()
                                       End Sub
        AddHandler btnFullScreen.Click, Sub()
                                            boardWin.Show()
                                            boardWin.ToggleFullScreen()
                                        End Sub
        AddHandler btnKeyboard.Click, Sub() ToggleKeyboard()
        AddHandler btnDeselect.Click, Sub() RunCommand(KeyCommand.Deselect)
        AddHandler btnTurnRed.Click, Sub() RunCommand(KeyCommand.TurnRed)
        AddHandler btnTurnGreen.Click, Sub() RunCommand(KeyCommand.TurnGreen)
        AddHandler keyboardWin.LetterClicked, Sub(lbl) SelectByKey(lbl)
        AddHandler keyboardWin.CommandClicked, Sub(c) RunCommand(c)
        AddHandler keyboardWin.KeyTyped, Sub(ch) HandleChar(ch)
        AddHandler keyboardWin.CommandKey, Sub(k) HandleCommandKey(k)
        AddHandler boardWin.KeyTyped, Sub(ch) HandleChar(ch)
        AddHandler boardWin.CommandKey, Sub(k) HandleCommandKey(k)
        AddHandler keyboardWin.VisibleChanged, Sub() btnKeyboard.Text = If(keyboardWin.Visible, "Hide Arabic keyboard", "Show Arabic keyboard")
        AddHandler blinkTimer.Tick, Sub()
                                        view.BlinkOn = Not view.BlinkOn AndAlso view.SelectedCell >= 0
                                        view.Invalidate()
                                    End Sub
        AddHandler numberTimer.Tick, Sub()
                                         numberTimer.Stop()
                                         _numberBuffer = ""
                                         RefreshKeyboard()
                                     End Sub
        blinkTimer.Start()
        cboModel.SelectedIndex = 0
    End Sub

    Protected Overrides Sub OnShown(e As EventArgs)
        MyBase.OnShown(e)
        ' control panel on the right of the screen, board on the left, keyboard below the board
        Dim wa = Screen.FromControl(Me).WorkingArea
        Location = New Point(wa.Right - Width - 10, wa.Top + 10)
        boardWin.Location = New Point(wa.Left + 10, wa.Top + 10)
        Dim maxBoardW = Math.Max(640, wa.Width - Width - 40)
        If boardWin.Width > maxBoardW Then boardWin.ClientSize = New Size(maxBoardW, maxBoardW * 9 \ 16)
        boardWin.Show()
        keyboardWin.Location = New Point(wa.Left + 10, Math.Min(boardWin.Bottom + 10, wa.Bottom - keyboardWin.Height - 10))
        Activate()
    End Sub

    Protected Overrides Sub OnFormClosed(e As FormClosedEventArgs)
        blinkTimer.Stop()
        MyBase.OnFormClosed(e)
        Application.Exit()
    End Sub

    ' ================================================================ layout
    Private Sub BuildLayout()
        Dim side As New Panel With {.Dock = DockStyle.Fill, .AutoScroll = True,
                                    .Padding = New Padding(14, 10, 10, 10), .BackColor = Color.White}
        Dim flow As New FlowLayoutPanel With {.Dock = DockStyle.Top, .AutoSize = True,
                                              .FlowDirection = FlowDirection.TopDown, .WrapContents = False}
        side.Controls.Add(flow)

        Dim heading = Function(t As String) New Label With {.Text = t, .AutoSize = True,
            .Font = New Font(UiFontName, 12, FontStyle.Bold), .Margin = New Padding(0, 14, 0, 4)}

        flow.Controls.Add(New Label With {.Text = "Letters Contest Boards", .AutoSize = True,
                                          .Font = New Font(UiFontName, 16, FontStyle.Bold)})
        flow.Controls.Add(New Label With {.Text = "Board", .AutoSize = True, .Margin = New Padding(0, 8, 0, 2)})
        flow.Controls.Add(cboModel)
        flow.Controls.Add(Row(btnShowBoard, btnFullScreen))
        flow.Controls.Add(Row(New Label With {.Text = "Hexagon pose", .AutoSize = True, .Margin = New Padding(0, 7, 6, 0)}, cboPose))
        flow.Controls.Add(lblDesc)

        flow.Controls.Add(heading("Contest"))
        flow.Controls.Add(lblTeam0)
        flow.Controls.Add(lblTeam1)
        flow.Controls.Add(lblStatus)
        flow.Controls.Add(lblHelp)
        flow.Controls.Add(Row(btnKeyboard))
        flow.Controls.Add(Row(btnDeselect, btnTurnRed, btnTurnGreen))
        flow.Controls.Add(Row(btnPick, btnUndo, btnNew))
        flow.Controls.Add(chkLabels)
        flow.Controls.Add(Row(New Label With {.Text = "Letters font", .AutoSize = True, .Margin = New Padding(0, 7, 6, 0)}, cboFont, chkBold))
        flow.Controls.Add(Row(btnAuto, New Label With {.Text = "Speed (ms)", .AutoSize = True, .Margin = New Padding(8, 8, 2, 0)}, nudSpeed))

        flow.Controls.Add(heading("Win chances"))
        flow.Controls.Add(Row(lblP, nudP))
        flow.Controls.Add(Row(New Label With {.Text = "Games to simulate", .AutoSize = True, .Margin = New Padding(0, 6, 4, 0)}, nudGames))
        flow.Controls.Add(chkExact)
        flow.Controls.Add(Row(btnCalc, btnCancel))
        flow.Controls.Add(bar)
        flow.Controls.Add(lblRes0)
        flow.Controls.Add(lblRes1)
        flow.Controls.Add(lblResNone)
        flow.Controls.Add(lblResMethod)

        Controls.Add(side)
    End Sub

    Private Shared Function Row(ParamArray cs As Control()) As FlowLayoutPanel
        Dim r As New FlowLayoutPanel With {.AutoSize = True, .FlowDirection = FlowDirection.LeftToRight,
                                           .WrapContents = True, .MaximumSize = New Size(330, 0), .Margin = New Padding(0, 2, 0, 2)}
        r.Controls.AddRange(cs)
        Return r
    End Function

    ' ================================================================ contest
    Private Sub OnModelChanged()
        Dim model = cboModel.SelectedIndex
        _switchingModel = True
        cboPose.SelectedIndex = CInt(BoardFactory.DefaultPose(model))
        cboPose.Enabled = BoardFactory.HasPose(model)
        _switchingModel = False
        LoadModel(model)
    End Sub

    Private Sub LoadModel(model As Integer)
        If model < 0 Then Return
        StopAuto()
        Dim pose = If(cboPose.SelectedIndex = 1, HexPose.Vertical, HexPose.Horizontal)
        _board = BoardFactory.Create(model, pose)
        _owner = Enumerable.Repeat(-1, _board.CellCount).ToArray()
        _history.Clear()
        _gameOver = False
        view.Board = _board
        view.Owner = _owner
        view.SelectedCell = -1
        view.WinPath = Nothing
        view.WinTeam = -1
        lblDesc.Text = _board.Description
        Dim t0 = _board.Teams(0), t1 = _board.Teams(1)
        lblTeam0.ForeColor = t0.Color : lblTeam1.ForeColor = t1.Color
        lblRes0.ForeColor = t0.Color : lblRes1.ForeColor = t1.Color
        _greenTeam = If(t1.Name = "Green", 1, 0)
        _redTeam = 1 - _greenTeam
        Dim tr = _board.Teams(_redTeam), tg = _board.Teams(_greenTeam)
        btnTurnRed.Text = $"Turn to {tr.Name}" : btnTurnRed.BackColor = tr.Color
        btnTurnGreen.Text = $"Turn to {tg.Name}" : btnTurnGreen.BackColor = tg.Color
        lblHelp.Text = "Type a letter (Arabic or English keyboard) or click it on the Arabic keyboard to select it. " &
                       $"F1 or Enter: {tr.Name}.  F2 or Space: {tg.Name}.  Esc: de-select. " &
                       $"On the board: left-click = {t0.Name}, right-click = {t1.Name}, Shift+click = clear."
        _numberBuffer = ""
        lblP.Text = $"{t0.Name} answers faster (%)"
        lblP.Margin = New Padding(0, 6, 4, 0)
        lblRes0.Text = "" : lblRes1.Text = "" : lblResNone.Text = "" : lblResMethod.Text = "" : bar.Value = 0
        UpdateGame()
    End Sub

    Private Sub OnCellClicked(cell As Integer, button As MouseButtons, shift As Boolean)
        If shift Then
            SetOwner(cell, -1)
        ElseIf button = MouseButtons.Left Then
            SetOwner(cell, 0)
        ElseIf button = MouseButtons.Right Then
            SetOwner(cell, 1)
        End If
    End Sub

    Private Sub SetOwner(cell As Integer, team As Integer)
        If _gameOver AndAlso team >= 0 Then Return
        If _owner(cell) = team Then Return
        _history.Push((cell, _owner(cell)))
        _owner(cell) = team
        view.SelectedCell = -1
        UpdateGame()
    End Sub

    ' ================================================================ keyboard selection
    Private Sub ToggleKeyboard()
        If keyboardWin.Visible Then
            keyboardWin.Hide()
        Else
            keyboardWin.Show()
            RefreshKeyboard()
        End If
    End Sub

    ''' <summary>A character typed on the physical keyboard in any of the three windows.</summary>
    Private Sub HandleChar(ch As Char)
        If ch = ChrW(27) Then RunCommand(KeyCommand.Deselect) : Return
        If ch = ChrW(13) Then RunCommand(KeyCommand.TurnRed) : Return
        If ch = " "c Then RunCommand(KeyCommand.TurnGreen) : Return
        Dim lbl = ArabicKeys.Translate(ch)
        If lbl IsNot Nothing Then SelectByKey(lbl)
    End Sub

    Private Sub HandleCommandKey(k As Keys)
        Select Case k
            Case Keys.F1 : RunCommand(KeyCommand.TurnRed)
            Case Keys.F2 : RunCommand(KeyCommand.TurnGreen)
            Case Keys.F11 : boardWin.ToggleFullScreen()
        End Select
    End Sub

    Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
        MyBase.OnKeyDown(e)
        If TypeOf ActiveControl Is NumericUpDown Then Return
        HandleCommandKey(e.KeyCode)
    End Sub

    Protected Overrides Sub OnKeyPress(e As KeyPressEventArgs)
        MyBase.OnKeyPress(e)
        ' let number boxes receive digits; everything else goes to the board
        If TypeOf ActiveControl Is NumericUpDown AndAlso (Char.IsDigit(e.KeyChar) OrElse e.KeyChar = ChrW(8)) Then Return
        Dim cb = TryCast(ActiveControl, ComboBox)
        If cb IsNot Nothing AndAlso cb.DroppedDown Then Return
        If (TypeOf ActiveControl Is Button OrElse TypeOf ActiveControl Is CheckBox) AndAlso
           (e.KeyChar = " "c OrElse e.KeyChar = ChrW(13)) Then Return
        HandleChar(e.KeyChar)
        e.Handled = True
    End Sub

    ''' <summary>
    ''' Selects the cell with this label. Digits build up a bar number (e.g. 1 then 2 = 12).
    ''' Pressing the same letter again moves to the next cell with that letter, if there is one.
    ''' </summary>
    Private Sub SelectByKey(key As String)
        If _board Is Nothing Then Return
        Dim wanted = key
        If Char.IsDigit(key(0)) Then
            _numberBuffer &= key
            If _numberBuffer.Length > 3 Then _numberBuffer = key
            numberTimer.Stop() : numberTimer.Start()
            wanted = _numberBuffer.TrimStart("0"c)
            If wanted = "" Then RefreshKeyboard() : Return
        Else
            _numberBuffer = ""
        End If
        Dim matches = _board.Cells.Where(Function(c) c.Label = wanted).Select(Function(c) c.Index).ToList()
        If matches.Count = 0 AndAlso Char.IsDigit(key(0)) AndAlso wanted <> key Then
            ' the number typed so far does not exist (e.g. "12" on a board with only 1-9): start again from this digit
            _numberBuffer = key : wanted = key
            matches = _board.Cells.Where(Function(c) c.Label = wanted).Select(Function(c) c.Index).ToList()
        End If
        If matches.Count = 0 Then RefreshKeyboard() : Return
        Dim pos = matches.IndexOf(view.SelectedCell)
        view.SelectedCell = matches((pos + 1) Mod matches.Count)
        view.BlinkOn = True
        lblStatus.ForeColor = Color.FromArgb(0, 102, 214)
        lblStatus.Text = $"Selected: {ArabicKeys.Display(wanted)}" & If(matches.Count > 1, $"  ({(pos + 1) Mod matches.Count + 1} of {matches.Count})", "")
        view.Invalidate()
        RefreshKeyboard()
    End Sub

    Private Sub RunCommand(cmd As KeyCommand)
        Dim sel = view.SelectedCell
        Select Case cmd
            Case KeyCommand.Deselect
                view.SelectedCell = -1
                _numberBuffer = ""
                UpdateGame()
            Case KeyCommand.TurnRed
                If sel >= 0 Then ColourCell(sel, _redTeam)
            Case KeyCommand.TurnGreen
                If sel >= 0 Then ColourCell(sel, _greenTeam)
        End Select
    End Sub

    ''' <summary>Gives a cell to a team (also allowed after the game ends, to correct mistakes).</summary>
    Private Sub ColourCell(cell As Integer, team As Integer)
        _numberBuffer = ""
        If _owner(cell) <> team Then
            _history.Push((cell, _owner(cell)))
            _owner(cell) = team
        End If
        view.SelectedCell = -1
        UpdateGame()
    End Sub

    Private Sub RefreshKeyboard()
        If _board Is Nothing Then Return
        keyboardWin.UpdateKeys(_board, _owner, view.SelectedCell, _redTeam, _greenTeam, view.BlinkColor, _numberBuffer)
        Dim hasSel = view.SelectedCell >= 0
        btnDeselect.Enabled = hasSel : btnTurnRed.Enabled = hasSel : btnTurnGreen.Enabled = hasSel
    End Sub

    Private Sub Undo()
        If _history.Count = 0 Then Return
        Dim h = _history.Pop()
        _owner(h.Cell) = h.Previous
        UpdateGame()
    End Sub

    Private Sub PickRandomCell()
        If _gameOver Then Return
        Dim empty = Enumerable.Range(0, _board.CellCount).Where(Function(i) _owner(i) < 0).ToList()
        If empty.Count = 0 Then Return
        view.SelectedCell = empty(_rnd.Next(empty.Count))
        view.BlinkOn = True
        Dim lbl = _board.Cells(view.SelectedCell).Label
        lblStatus.ForeColor = Color.FromArgb(0, 102, 214)
        lblStatus.Text = If(lbl <> "", $"Selected: {ArabicKeys.Display(lbl)}", "Selected: the blinking cell")
        view.Invalidate()
        RefreshKeyboard()
    End Sub

    ''' <summary>Re-checks the board for a winner and refreshes all labels.</summary>
    Private Sub UpdateGame()
        _gameOver = False
        view.WinPath = Nothing : view.WinTeam = -1
        Dim c0 = _owner.Count(Function(o) o = 0), c1 = _owner.Count(Function(o) o = 1)
        Dim left = _owner.Count(Function(o) o < 0)
        lblTeam0.Text = $"{_board.Teams(0).Name} ({_board.Teams(0).Sides}): {c0} cells"
        lblTeam1.Text = $"{_board.Teams(1).Name} ({_board.Teams(1).Sides}): {c1} cells"
        For t = 0 To 1
            Dim path = Engine.FindWinPath(_board, _owner, t)
            If path IsNot Nothing Then
                _gameOver = True
                view.WinPath = path : view.WinTeam = t
                lblStatus.ForeColor = _board.Teams(t).Color
                lblStatus.Text = $"{_board.Teams(t).Name} wins!"
                view.StatusText = lblStatus.Text : view.StatusColor = _board.Teams(t).Color
                StopAuto()
                RefreshKeyboard()
                view.Invalidate()
                Return
            End If
        Next
        If left = 0 OrElse (Not Engine.CanStillWin(_board, _owner, 0) AndAlso Not Engine.CanStillWin(_board, _owner, 1)) Then
            _gameOver = True
            lblStatus.ForeColor = Color.FromArgb(120, 116, 108)
            lblStatus.Text = If(left = 0, "Board full: no winner", "No winner possible: both teams are blocked")
            view.StatusText = lblStatus.Text : view.StatusColor = Color.White
            StopAuto()
        Else
            lblStatus.ForeColor = Color.FromArgb(27, 27, 31)
            lblStatus.Text = $"{left} cells left"
            view.StatusText = ""
        End If
        view.Invalidate()
        RefreshKeyboard()
    End Sub

    ' ---- auto-play: random cells, won by team 0 with the chance set below
    Private Sub ToggleAuto()
        If autoTimer.Enabled Then
            StopAuto()
        Else
            If _gameOver Then LoadModel(cboModel.SelectedIndex)
            autoTimer.Interval = CInt(nudSpeed.Value)
            autoTimer.Start()
            btnAuto.Text = "Stop auto-play"
        End If
    End Sub

    Private Sub StopAuto()
        autoTimer.Stop()
        btnAuto.Text = "Auto-play demo"
    End Sub

    Private Sub AutoStep(sender As Object, e As EventArgs)
        autoTimer.Interval = CInt(nudSpeed.Value)
        If _gameOver Then StopAuto() : Return
        Dim empty = Enumerable.Range(0, _board.CellCount).Where(Function(i) _owner(i) < 0).ToList()
        If empty.Count = 0 Then StopAuto() : Return
        Dim cell = empty(_rnd.Next(empty.Count))
        Dim team = If(_rnd.NextDouble() < CDbl(nudP.Value) / 100, 0, 1)
        SetOwner(cell, team)
    End Sub

    ' ================================================================ statistics
    Private Async Sub Calculate(sender As Object, e As EventArgs)
        Dim b = _board
        Dim p0 = CDbl(nudP.Value) / 100
        Dim games = CLng(nudGames.Value)
        Dim exact = chkExact.Checked AndAlso b.CellCount <= Engine.MaxExactCells
        _cts = New CancellationTokenSource()
        Dim ct = _cts.Token
        btnCalc.Enabled = False : btnCancel.Enabled = True : cboModel.Enabled = False
        bar.Value = 0
        lblResMethod.Text = If(exact, "Counting every possible board…", $"Simulating {games:N0} games…")
        Dim prog As New Progress(Of Double)(Sub(v) bar.Value = Math.Max(0, Math.Min(1000, CInt(v * 1000))))
        Try
            Dim r = Await Task.Run(Function()
                                       If exact Then Return Engine.Exact(b, p0, prog, ct)
                                       Return Engine.MonteCarlo(b, games, p0, prog, ct)
                                   End Function)
            bar.Value = 1000
            lblRes0.Text = $"{b.Teams(0).Name} ({b.Teams(0).Sides}): {r.Team0Win:P2}"
            lblRes1.Text = $"{b.Teams(1).Name} ({b.Teams(1).Sides}): {r.Team1Win:P2}"
            lblResNone.Text = $"No winner: {r.NoWinner:P2}"
            lblResMethod.Text = If(r.IsExact,
                $"Exact: all {r.Games:N0} ways to split {b.CellCount} cells, with {b.Teams(0).Name} winning each question {p0:P0} of the time.",
                $"Simulated: {r.Games:N0} random games, with {b.Teams(0).Name} winning each question {p0:P0} of the time.")
        Catch ex As OperationCanceledException
            lblResMethod.Text = "Cancelled."
            bar.Value = 0
        Finally
            btnCalc.Enabled = True : btnCancel.Enabled = False : cboModel.Enabled = True
        End Try
    End Sub
End Class
