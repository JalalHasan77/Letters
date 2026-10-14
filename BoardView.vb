Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms

''' <summary>Draws a board and reports which cell was clicked.</summary>
Public Class BoardView
    Inherits Control

    Public Event CellClicked(cell As Integer, button As MouseButtons, shift As Boolean)

    Private _board As Board
    Public Property Board As Board
        Get
            Return _board
        End Get
        Set(value As Board)
            _board = value : Invalidate()
        End Set
    End Property

    Public Property Owner As Integer()
    Public Property SelectedCell As Integer = -1
    Public Property WinPath As List(Of Integer)
    Public Property WinTeam As Integer = -1
    Public Property ShowLabels As Boolean = True
    Public Property LabelFontName As String = ArabicKeys.DefaultLetterFont()
    Public Property LabelBold As Boolean = False

    ''' <summary>When True the selected cell is drawn in the blink colour (toggled by a timer).</summary>
    Public Property BlinkOn As Boolean
    Public Property BlinkColor As Color = Color.FromArgb(0, 122, 255)
    ''' <summary>Message shown on the board screen, e.g. "Red wins!". Empty for none.</summary>
    Public Property StatusText As String = ""
    Public Property StatusColor As Color = Color.Black
    Public Property StageColor As Color = Color.FromArgb(251, 248, 241)

    Private _scale As Single = 1, _offX As Single, _offY As Single
    Private _stage As RectangleF
    Private ReadOnly Ink As Color = Color.FromArgb(27, 27, 31)
    Private ReadOnly Edge As Color = Color.FromArgb(110, 105, 95)

    Public Sub New()
        SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or
                 ControlStyles.ResizeRedraw Or ControlStyles.UserPaint, True)
        BackColor = Color.FromArgb(251, 248, 241)
    End Sub

    ''' <summary>
    ''' The picture is always a 16:9 stage (black bars fill any extra space).
    ''' The board sits in the middle; team score cards sit on the left and right.
    ''' </summary>
    Private Sub UpdateTransform()
        Dim b = _board
        Dim sw As Single = Width, sh As Single = Width * 9.0F / 16.0F
        If sh > Height Then sh = Height : sw = Height * 16.0F / 9.0F
        _stage = New RectangleF((Width - sw) / 2, (Height - sh) / 2, sw, sh)
        Dim areaW = sw * 0.6F, areaH = sh * 0.92F
        _scale = Math.Min(areaW / b.Width, areaH / b.Height)
        If _scale <= 0 Then _scale = 0.01F
        _offX = _stage.X + (sw - b.Width * _scale) / 2
        _offY = _stage.Y + (sh - b.Height * _scale) / 2
    End Sub

    Private Sub DrawStage(g As Graphics)
        g.Clear(Color.Black)
        Using br As New SolidBrush(StageColor) : g.FillRectangle(br, _stage) : End Using
        ' team cards: team 0 on the left, team 1 on the right
        Dim cardW = _stage.Width * 0.15F, cardH = _stage.Height * 0.36F
        Dim y = _stage.Y + (_stage.Height - cardH) / 2
        For t = 0 To 1
            Dim x = If(t = 0, _stage.X + _stage.Width * 0.025F, _stage.Right - _stage.Width * 0.025F - cardW)
            Dim rc As New RectangleF(x, y, cardW, cardH)
            Using path = RoundRect(rc, cardW * 0.08F), br As New SolidBrush(_board.Teams(t).Color)
                g.FillPath(br, path)
            End Using
            Dim team = t
            Dim count = If(Owner Is Nothing, 0, Owner.Count(Function(o) o = team))
            Using fName As New Font(LabelFontName, cardH * 0.12F, FontStyle.Bold, GraphicsUnit.Pixel),
                  fNum As New Font(LabelFontName, cardH * 0.4F, FontStyle.Bold, GraphicsUnit.Pixel),
                  fSide As New Font(LabelFontName, cardH * 0.065F, FontStyle.Regular, GraphicsUnit.Pixel),
                  sf As New StringFormat With {.Alignment = StringAlignment.Center, .LineAlignment = StringAlignment.Center}
                g.DrawString(_board.Teams(t).Name, fName, Brushes.White, New RectangleF(x, y + cardH * 0.05F, cardW, cardH * 0.2F), sf)
                g.DrawString(count.ToString(), fNum, Brushes.White, New RectangleF(x, y + cardH * 0.22F, cardW, cardH * 0.55F), sf)
                g.DrawString(_board.Teams(t).Sides, fSide, Brushes.White, New RectangleF(x, y + cardH * 0.78F, cardW, cardH * 0.17F), sf)
            End Using
        Next
    End Sub

    Private Sub DrawStatus(g As Graphics)
        If String.IsNullOrEmpty(StatusText) Then Return
        Using f As New Font(LabelFontName, _stage.Height * 0.055F, FontStyle.Bold, GraphicsUnit.Pixel)
            Dim sz = g.MeasureString(StatusText, f)
            Dim rc As New RectangleF(_stage.X + (_stage.Width - sz.Width) / 2 - 24, _stage.Bottom - sz.Height - _stage.Height * 0.05F,
                                     sz.Width + 48, sz.Height + 12)
            Using path = RoundRect(rc, rc.Height / 2), br As New SolidBrush(Color.FromArgb(235, 27, 27, 31))
                g.FillPath(br, path)
            End Using
            Using br As New SolidBrush(If(StatusColor.GetBrightness() < 0.25F, Color.White, StatusColor)),
                  sf As New StringFormat With {.Alignment = StringAlignment.Center, .LineAlignment = StringAlignment.Center}
                g.DrawString(StatusText, f, br, rc, sf)
            End Using
        End Using
    End Sub

    Private Shared Function RoundRect(r As RectangleF, radius As Single) As GraphicsPath
        Dim p As New GraphicsPath()
        Dim d = radius * 2
        p.AddArc(r.X, r.Y, d, d, 180, 90)
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90)
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90)
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90)
        p.CloseFigure()
        Return p
    End Function

    Private Function OwnerOf(i As Integer) As Integer
        If Owner Is Nothing Then Return -1
        Return Owner(i)
    End Function

    Private Function FillFor(c As Cell) As Color
        If c.Index = SelectedCell AndAlso BlinkOn Then Return BlinkColor
        Dim o = OwnerOf(c.Index)
        Return If(o < 0, c.EmptyColor, _board.Teams(o).Color)
    End Function

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        MyBase.OnPaint(e)
        If _board Is Nothing Then Return
        UpdateTransform()
        Dim g = e.Graphics
        g.SmoothingMode = SmoothingMode.AntiAlias
        g.TextRenderingHint = Drawing.Text.TextRenderingHint.AntiAliasGridFit
        DrawStage(g)
        Dim baseState = g.Save()
        g.TranslateTransform(_offX, _offY)
        g.ScaleTransform(_scale, _scale)
        Dim onPath As New HashSet(Of Integer)
        If WinPath IsNot Nothing Then onPath.UnionWith(WinPath)

        ' 1. coloured sides and the board area
        For Each z In _board.Zones
            Using br As New SolidBrush(z.FillColor) : g.FillPolygon(br, z.Points) : End Using
        Next

        ' 2. links (Circles Only): thin white when idle, thick team colour when they connect
        For Each lk In _board.Links
            Dim t = LinkTeam(lk)
            If t < 0 Then
                Using pen As New Pen(Color.White, 7) : g.DrawLine(pen, lk.P1, lk.P2) : End Using
            End If
        Next
        For Each lk In _board.Links
            Dim t = LinkTeam(lk)
            If t < 0 Then Continue For
            If LinkOnPath(lk, onPath) Then
                Using pen As New Pen(Ink, 30) : g.DrawLine(pen, lk.P1, lk.P2) : End Using
            End If
            Using pen As New Pen(Ink, 18) : g.DrawLine(pen, lk.P1, lk.P2) : End Using
            Using pen As New Pen(_board.Teams(t).Color, 14) : g.DrawLine(pen, lk.P1, lk.P2) : End Using
        Next

        ' 3. bar cells
        For Each c In _board.Cells
            If c.Shape <> CellShape.Bar Then Continue For
            Dim poly = c.BarPolygon()
            Using br As New SolidBrush(FillFor(c)) : g.FillPolygon(br, poly) : End Using
            Dim w As Single = If(onPath.Contains(c.Index) OrElse c.Index = SelectedCell, 5, 1)
            Dim col = If(onPath.Contains(c.Index) OrElse c.Index = SelectedCell, Ink, Edge)
            Using pen As New Pen(col, w)
                If c.Index = SelectedCell Then pen.DashStyle = DashStyle.Dash
                g.DrawPolygon(pen, poly)
            End Using
            If ShowLabels AndAlso c.Label <> "" Then DrawLabel(g, c)
        Next

        ' 4. border markers
        For Each n In _board.Notches
            Dim r = n.Radius
            Using br As New SolidBrush(_board.Teams(n.Team).Color)
                g.FillEllipse(br, n.Center.X - r, n.Center.Y - r, 2 * r, 2 * r)
            End Using
            Using pen As New Pen(Color.White, 3) : g.DrawEllipse(pen, n.Center.X - r, n.Center.Y - r, 2 * r, 2 * r) : End Using
        Next

        ' 5. polygon and circle cells
        For Each c In _board.Cells
            If c.Shape = CellShape.Bar Then Continue For
            Dim highlight = onPath.Contains(c.Index)
            Dim sel = (c.Index = SelectedCell)
            Using br As New SolidBrush(FillFor(c))
                If c.Shape = CellShape.Circle Then
                    Dim r = c.Radius
                    If highlight Then
                        Using hb As New SolidBrush(Ink) : g.FillEllipse(hb, c.Center.X - r - 8, c.Center.Y - r - 8, 2 * r + 16, 2 * r + 16) : End Using
                    End If
                    g.FillEllipse(br, c.Center.X - r, c.Center.Y - r, 2 * r, 2 * r)
                    Using pen As New Pen(If(sel, Ink, Edge), If(sel, 5.0F, 1.2F))
                        If sel Then pen.DashStyle = DashStyle.Dash
                        g.DrawEllipse(pen, c.Center.X - r, c.Center.Y - r, 2 * r, 2 * r)
                    End Using
                Else
                    g.FillPolygon(br, c.Points)
                    Using pen As New Pen(If(highlight OrElse sel, Ink, Color.Black), If(highlight OrElse sel, 6.0F, 2.0F))
                        If sel AndAlso Not highlight Then pen.DashStyle = DashStyle.Dash
                        g.DrawPolygon(pen, c.Points)
                    End Using
                End If
            End Using
            If ShowLabels AndAlso c.Label <> "" Then DrawLabel(g, c)
        Next
        g.Restore(baseState)
        DrawStatus(g)
    End Sub

    Private Sub DrawLabel(g As Graphics, c As Cell)
        Dim size As Single
        If c.Shape = CellShape.Circle Then
            size = c.Radius * 0.95F
        ElseIf c.Shape = CellShape.Bar Then
            size = c.Thickness * 0.62F
        Else
            Dim minX = c.Points.Min(Function(p) p.X), maxX = c.Points.Max(Function(p) p.X)
            size = (maxX - minX) * If(c.Points.Length = 4, 0.6F, 0.4F)   ' small squares get relatively larger letters
        End If
        Dim blinking = (c.Index = SelectedCell AndAlso BlinkOn)
        Dim col = If(OwnerOf(c.Index) < 0 AndAlso Not blinking, _board.LabelColor, Color.White)
        Using f As New Font(LabelFontName, size, If(LabelBold, FontStyle.Bold, FontStyle.Regular), GraphicsUnit.Pixel),
              br As New SolidBrush(col),
              sf As New StringFormat With {.Alignment = StringAlignment.Center, .LineAlignment = StringAlignment.Center}
            g.DrawString(ArabicKeys.Display(c.Label), f, br, c.Center, sf)
        End Using
    End Sub

    ''' <summary>Team whose colour a link should show, or -1 if it is not lit.</summary>
    Private Function LinkTeam(lk As Link) As Integer
        Dim a = OwnerOf(lk.CellA)
        If a < 0 Then Return -1
        If lk.CellB >= 0 Then Return If(OwnerOf(lk.CellB) = a, a, -1)
        Return If(lk.SideTeam = a, a, -1)
    End Function

    Private Function LinkOnPath(lk As Link, onPath As HashSet(Of Integer)) As Boolean
        If WinPath Is Nothing OrElse WinPath.Count = 0 Then Return False
        If lk.CellB >= 0 Then
            Dim ia = WinPath.IndexOf(lk.CellA), ib = WinPath.IndexOf(lk.CellB)
            Return ia >= 0 AndAlso ib >= 0 AndAlso Math.Abs(ia - ib) = 1
        End If
        If lk.SideTeam <> WinTeam Then Return False
        Dim t = _board.Teams(WinTeam)
        Return (lk.CellA = WinPath(0) AndAlso t.Side1.Contains(lk.CellA)) OrElse
               (lk.CellA = WinPath(WinPath.Count - 1) AndAlso t.Side2.Contains(lk.CellA))
    End Function

    ' ------------------------------------------------------------ hit testing
    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)
        If _board Is Nothing Then Return
        UpdateTransform()
        Dim pt As New PointF((e.X - _offX) / _scale, (e.Y - _offY) / _scale)
        Dim hit = HitTest(pt)
        If hit >= 0 Then RaiseEvent CellClicked(hit, e.Button, (ModifierKeys And Keys.Shift) = Keys.Shift)
    End Sub

    Private Function HitTest(pt As PointF) As Integer
        ' circles and polygons sit on top of bars, so test them first
        For Each c In _board.Cells
            Select Case c.Shape
                Case CellShape.Circle
                    Dim dx = pt.X - c.Center.X, dy = pt.Y - c.Center.Y
                    If dx * dx + dy * dy <= c.Radius * c.Radius Then Return c.Index
                Case CellShape.Polygon
                    If Inside(c.Points, pt) Then Return c.Index
            End Select
        Next
        For Each c In _board.Cells
            If c.Shape = CellShape.Bar AndAlso Inside(c.BarPolygon(), pt) Then Return c.Index
        Next
        Return -1
    End Function

    Private Shared Function Inside(poly As PointF(), p As PointF) As Boolean
        Dim result = False
        Dim j = poly.Length - 1
        For i = 0 To poly.Length - 1
            If (poly(i).Y > p.Y) <> (poly(j).Y > p.Y) AndAlso
               p.X < (poly(j).X - poly(i).X) * (p.Y - poly(i).Y) / (poly(j).Y - poly(i).Y) + poly(i).X Then
                result = Not result
            End If
            j = i
        Next
        Return result
    End Function
End Class
