Imports System.Drawing

' ---------------------------------------------------------------------------
'  Builds the five board models.
'  All coordinates are in "board units"; the view scales them to fit.
' ---------------------------------------------------------------------------
Public Enum HexPose
    Horizontal   ' flat top: hexagons lie on a flat side
    Vertical     ' pointy top: hexagons stand on a corner
End Enum

Public Module BoardFactory

    Public ReadOnly RedColor As Color = Color.FromArgb(210, 43, 63)
    Public ReadOnly GreenColor As Color = Color.FromArgb(30, 148, 80)
    Public ReadOnly GoldCell As Color = Color.FromArgb(246, 196, 83)
    Public ReadOnly YellowBoard As Color = Color.FromArgb(247, 236, 92)

    ''' <summary>All 28 Arabic letters, in alphabetical order.</summary>
    Public ReadOnly ArabicLetters As String() =
        "أ ب ت ث ج ح خ د ذ ر ز س ش ص ض ط ظ ع غ ف ق ك ل م ن ه و ي".Split(" "c)

    ''' <summary>Labels for n cells: Arabic letters first, then 1, 2, 3 ... once the letters run out.</summary>
    Public Function MakeLabels(n As Integer) As String()
        Dim result(n - 1) As String
        For i = 0 To n - 1
            result(i) = If(i < ArabicLetters.Length, ArabicLetters(i), (i - ArabicLetters.Length + 1).ToString())
        Next
        Return result
    End Function

    ''' <summary>25 Arabic letters used as cell labels.</summary>
    Private ReadOnly _rnd As New Random()

    ''' <summary>
    ''' 25 labels for the hexagon and octagon boards, chosen at random from the 28 Arabic
    ''' letters plus the digits 1 to 9 (37 in all). A new mix is drawn for every new game.
    ''' </summary>
    Public Function MixedLabels() As String()
        Dim pool = ArabicLetters.Concat("1 2 3 4 5 6 7 8 9".Split(" "c)).ToList()
        For i = pool.Count - 1 To 1 Step -1        ' shuffle the whole pool, then keep the first 25
            Dim j = _rnd.Next(i + 1)
            Dim t = pool(i) : pool(i) = pool(j) : pool(j) = t
        Next
        Return pool.Take(25).ToArray()
    End Function

    Public ReadOnly Letters25 As String() =
        "ب ج ر ط ك م ض ت ش ع ن ح ق ز س ذ ف ص ل خ غ ث د ظ أ".Split(" "c)

    Public ReadOnly ModelNames As String() = {
        "1. Hexagons (original rectangle)",
        "2. Hexagons (diamond)",
        "3. Octagons & Squares",
        "4. Circles & Bars (bars are cells)",
        "5. Circles Only (bars are links)",
        "6. Circles Only (No Bars)"}

    ''' <summary>True for the two hexagon boards, which can be shown in either pose.</summary>
    Public Function HasPose(model As Integer) As Boolean
        Return model = 0 OrElse model = 1
    End Function

    ''' <summary>The pose each hexagon board is drawn in by default.</summary>
    Public Function DefaultPose(model As Integer) As HexPose
        Return If(model = 1, HexPose.Vertical, HexPose.Horizontal)
    End Function

    Public Function Create(model As Integer, Optional pose As HexPose = HexPose.Horizontal) As Board
        Dim b As Board
        Select Case model
            Case 0
                b = HexRectangle()
                If pose = HexPose.Vertical Then
                    Transpose(b)
                    b.Description = "Hexagons standing upright (pointy top). Red joins left ↔ right, Green joins top ↕ bottom."
                End If
            Case 1
                b = HexDiamond()
                If pose = HexPose.Horizontal Then
                    Transpose(b)
                    b.Teams(0).Sides = "left ↔ right" : b.Teams(1).Sides = "slanted top ↕ bottom"
                    b.Description = "Diamond of hexagons lying flat (flat top). Red joins left ↔ right, Green joins the slanted top ↕ bottom."
                End If
            Case 2 : b = OctagonsSquares()
            Case 3 : b = CirclesAndBars(barsAreCells:=True)
            Case 4 : b = CirclesAndBars(barsAreCells:=False)
            Case Else : b = CirclesNoBars()
        End Select
        Return b
    End Function

    ''' <summary>
    ''' Switches hexagons between flat-top and pointy-top by mirroring the board across
    ''' its diagonal (x and y swapped). Each team keeps its colour on the same sides of
    ''' the screen, so the two teams swap the kind of path they need.
    ''' </summary>
    Private Sub Transpose(b As Board)
        Dim T = Function(p As PointF) New PointF(p.Y, p.X)
        For Each c In b.Cells
            If c.Points IsNot Nothing Then c.Points = c.Points.Select(T).ToArray()
            c.Center = T(c.Center) : c.P1 = T(c.P1) : c.P2 = T(c.P2)
        Next
        For Each z In b.Zones
            z.Points = z.Points.Select(T).ToArray()
            If z.Team >= 0 Then
                z.Team = 1 - z.Team
                z.FillColor = b.Teams(z.Team).Color
            End If
        Next
        For Each nt In b.Notches
            nt.Center = T(nt.Center) : nt.Team = 1 - nt.Team
        Next
        For Each lk In b.Links
            lk.P1 = T(lk.P1) : lk.P2 = T(lk.P2)
            If lk.SideTeam >= 0 Then lk.SideTeam = 1 - lk.SideTeam
        Next
        Dim w = b.Width : b.Width = b.Height : b.Height = w
        Dim a1 = b.Teams(0).Side1, a2 = b.Teams(0).Side2
        b.Teams(0).Side1 = b.Teams(1).Side1 : b.Teams(0).Side2 = b.Teams(1).Side2
        b.Teams(1).Side1 = a1 : b.Teams(1).Side2 = a2
    End Sub

    Private Sub SetTeam(b As Board, t As Integer, name As String, col As Color, sides As String)
        b.Teams(t).Name = name : b.Teams(t).Color = col : b.Teams(t).Sides = sides
    End Sub

    Private Function Poly(cx As Single, cy As Single, r As Single, n As Integer, startDeg As Double) As PointF()
        Dim pts(n - 1) As PointF
        For k = 0 To n - 1
            Dim a = (startDeg + k * 360.0 / n) * Math.PI / 180
            pts(k) = New PointF(CSng(cx + r * Math.Cos(a)), CSng(cy + r * Math.Sin(a)))
        Next
        Return pts
    End Function

    Private Function P(x As Single, y As Single) As PointF
        Return New PointF(x, y)
    End Function

    ' ---------------------------------------------------------------- Model 1
    ' Flat-top hexagons in 5 columns; the 2nd and 4th columns sit half a cell higher.
    Private Function HexRectangle() As Board
        Dim b As New Board With {.Name = ModelNames(0),
            .Description = "The original board. Red joins left ↔ right, Green joins top ↕ bottom."}
        SetTeam(b, 0, "Red", RedColor, "left ↔ right")
        SetTeam(b, 1, "Green", GreenColor, "top ↕ bottom")
        Dim s As Single = 50, h = CSng(Math.Sqrt(3) * s), ox As Single = 60, oy As Single = 50
        b.Width = 2 * ox + 2 * s + 4 * 1.5F * s : b.Height = 2 * oy + 5.5F * h
        b.Zones.Add(New Zone With {.Points = {P(0, 0), P(b.Width, 0), P(b.Width, b.Height), P(0, b.Height)}, .Team = 1, .FillColor = GreenColor})
        ' red reaches the middle of the outer columns, so it touches every side cell
        Dim xl = ox + s, xr = b.Width - ox - s
        ' red and green meet on 45-degree lines running in from each corner
        Dim W0 = b.Width, H0 = b.Height
        b.Zones.Add(New Zone With {.Points = {P(0, 0), P(xl, xl), P(xl, H0 - xl), P(0, H0)}, .Team = 0, .FillColor = RedColor})
        b.Zones.Add(New Zone With {.Points = {P(W0, 0), P(W0, H0), P(W0 - xl, H0 - xl), P(W0 - xl, xl)}, .Team = 0, .FillColor = RedColor})
        Dim id(4, 4) As Integer
        Dim mixed = MixedLabels()
        For c = 0 To 4
            For r = 0 To 4
                Dim cx = ox + s + c * 1.5F * s
                Dim cy = oy + h / 2 + r * h + If(c Mod 2 = 1, 0, h / 2)
                id(c, r) = b.AddCell(New Cell With {.Shape = CellShape.Polygon, .Center = P(cx, cy),
                    .Points = Poly(cx, cy, s, 6, 0), .Label = mixed(c * 5 + r), .EmptyColor = GoldCell})
            Next
        Next
        b.InitNeighbors()
        For c = 0 To 4
            For r = 0 To 4
                If r > 0 Then b.Connect(id(c, r), id(c, r - 1))
                For Each c2 In {c - 1, c + 1}
                    If c2 < 0 OrElse c2 > 4 Then Continue For
                    Dim high = (c Mod 2 = 1)
                    For Each r2 In If(high, {r - 1, r}, {r, r + 1})
                        If r2 >= 0 AndAlso r2 <= 4 Then b.Connect(id(c, r), id(c2, r2))
                    Next
                Next
            Next
        Next
        For k = 0 To 4
            b.Teams(0).Side1.Add(id(0, k)) : b.Teams(0).Side2.Add(id(4, k))
            b.Teams(1).Side1.Add(id(k, 0)) : b.Teams(1).Side2.Add(id(k, 4))
        Next
        Return b
    End Function

    ' ---------------------------------------------------------------- Model 2
    ' Pointy-top hexagons, each row shifted half a cell right: a rhombus (Hex board).
    Private Function HexDiamond() As Board
        Dim b As New Board With {.Name = ModelNames(1),
            .Description = "Same 25 hexagons in a diamond. Red joins the slanted sides, Green joins top ↕ bottom."}
        SetTeam(b, 0, "Red", RedColor, "slanted sides")
        SetTeam(b, 1, "Green", GreenColor, "top ↕ bottom")
        Dim s As Single = 46, w = CSng(Math.Sqrt(3) * s), vs = 1.5F * s, pad As Single = 150
        Dim cxf = Function(c As Integer, r As Integer) pad + w / 2 + c * w + r * w / 2
        Dim cyf = Function(r As Integer) pad + s + r * vs
        b.Width = pad * 2 + 7 * w : b.Height = pad * 2 + 2 * s + 4 * vs
        ' corners sit just outside the corner cells, so the coloured sides touch every edge cell
        Dim dOff = 0.4F * w, t = 0.8F * s / vs
        Dim TL = P(cxf(0, 0) - dOff - t * w / 2, cyf(0) - 0.8F * s), TR = P(cxf(4, 0) + dOff - t * w / 2, cyf(0) - 0.8F * s)
        Dim BL = P(cxf(0, 4) - dOff + t * w / 2, cyf(4) + 0.8F * s), BR = P(cxf(4, 4) + dOff + t * w / 2, cyf(4) + 0.8F * s)
        Dim C0 = P((TL.X + BR.X) / 2, (TL.Y + BR.Y) / 2)
        ' widen the coloured border: push each corner outward from the centre
        Dim grow = Function(q As PointF) P(C0.X + (q.X - C0.X) * 1.3F, C0.Y + (q.Y - C0.Y) * 1.3F)
        TL = grow(TL) : TR = grow(TR) : BL = grow(BL) : BR = grow(BR)
        b.Zones.Add(New Zone With {.Points = {TL, TR, C0}, .Team = 1, .FillColor = GreenColor})
        b.Zones.Add(New Zone With {.Points = {BL, BR, C0}, .Team = 1, .FillColor = GreenColor})
        b.Zones.Add(New Zone With {.Points = {TL, BL, C0}, .Team = 0, .FillColor = RedColor})
        b.Zones.Add(New Zone With {.Points = {TR, BR, C0}, .Team = 0, .FillColor = RedColor})
        Dim id(4, 4) As Integer
        Dim mixed = MixedLabels()
        For r = 0 To 4
            For c = 0 To 4
                Dim x = cxf(c, r), y = cyf(r)
                id(c, r) = b.AddCell(New Cell With {.Shape = CellShape.Polygon, .Center = P(x, y),
                    .Points = Poly(x, y, s, 6, 30), .Label = mixed(r * 5 + c), .EmptyColor = GoldCell})
            Next
        Next
        b.InitNeighbors()
        Dim dirs = {(1, 0), (-1, 0), (0, -1), (1, -1), (-1, 1), (0, 1)}
        For r = 0 To 4
            For c = 0 To 4
                For Each d In dirs
                    Dim c2 = c + d.Item1, r2 = r + d.Item2
                    If c2 >= 0 AndAlso c2 <= 4 AndAlso r2 >= 0 AndAlso r2 <= 4 Then b.Connect(id(c, r), id(c2, r2))
                Next
            Next
        Next
        For k = 0 To 4
            b.Teams(0).Side1.Add(id(0, k)) : b.Teams(0).Side2.Add(id(4, k))
            b.Teams(1).Side1.Add(id(k, 0)) : b.Teams(1).Side2.Add(id(k, 4))
        Next
        Return b
    End Function

    ' ---------------------------------------------------------------- Model 3
    ' Truncated-square tiling: octagons where (i+j) is even, squares where it is odd.
    Private Function OctagonsSquares() As Board
        Dim b As New Board With {.Name = ModelNames(2),
            .Description = "13 octagons + 12 squares. Red joins top ↕ bottom, Green joins left ↔ right."}
        SetTeam(b, 0, "Red", RedColor, "top ↕ bottom")
        SetTeam(b, 1, "Green", GreenColor, "left ↔ right")
        Dim pitch As Single = 90, w8 = CSng(pitch * Math.Sqrt(2)), sq = CSng(w8 / (1 + Math.Sqrt(2))), m As Single = 40
        b.Width = 2 * m + 4 * pitch + w8 : b.Height = b.Width
        Dim Wd = b.Width, Ht = b.Height
        b.Zones.Add(New Zone With {.Points = {P(0, 0), P(Wd, 0), P(Wd, Ht), P(0, Ht)}, .Team = 0, .FillColor = RedColor})
        b.Zones.Add(New Zone With {.Points = {P(0, 0), P(Wd / 2, Ht / 2), P(0, Ht)}, .Team = 1, .FillColor = GreenColor})
        b.Zones.Add(New Zone With {.Points = {P(Wd, 0), P(Wd / 2, Ht / 2), P(Wd, Ht)}, .Team = 1, .FillColor = GreenColor})
        Dim o0 = m + w8 / 2
        ' black fills the spaces between cells; the outer halves of edge cells reach the coloured sides
        Dim e0 = o0, e1 = o0 + 4 * pitch
        b.Zones.Add(New Zone With {.Points = {P(e0, e0), P(e1, e0), P(e1, e1), P(e0, e1)}, .Team = -1, .FillColor = Color.Black})
        Dim id(4, 4) As Integer
        Dim mixed = MixedLabels()
        For j = 0 To 4
            For i = 0 To 4
                Dim cx = o0 + i * pitch, cy = o0 + j * pitch
                Dim pts As PointF()
                If (i + j) Mod 2 = 0 Then
                    Dim r = w8 / 2 * 0.975F, a = CSng(r * Math.Tan(Math.PI / 8))
                    pts = {P(cx + a, cy - r), P(cx + r, cy - a), P(cx + r, cy + a), P(cx + a, cy + r),
                           P(cx - a, cy + r), P(cx - r, cy + a), P(cx - r, cy - a), P(cx - a, cy - r)}
                Else
                    Dim k = sq * 0.94F / 2
                    pts = {P(cx - k, cy - k), P(cx + k, cy - k), P(cx + k, cy + k), P(cx - k, cy + k)}
                End If
                id(i, j) = b.AddCell(New Cell With {.Shape = CellShape.Polygon, .Center = P(cx, cy), .Points = pts,
                    .Label = mixed(j * 5 + i), .EmptyColor = YellowBoard})
            Next
        Next
        b.InitNeighbors()
        For j = 0 To 4
            For i = 0 To 4
                If i < 4 Then b.Connect(id(i, j), id(i + 1, j))
                If j < 4 Then b.Connect(id(i, j), id(i, j + 1))
                If (i + j) Mod 2 = 0 Then          ' octagons also touch octagons diagonally
                    If i < 4 AndAlso j < 4 Then b.Connect(id(i, j), id(i + 1, j + 1))
                    If i > 0 AndAlso j < 4 Then b.Connect(id(i, j), id(i - 1, j + 1))
                End If
            Next
        Next
        For k = 0 To 4   ' edge squares are taken to touch the border
            b.Teams(0).Side1.Add(id(k, 0)) : b.Teams(0).Side2.Add(id(k, 4))
            b.Teams(1).Side1.Add(id(0, k)) : b.Teams(1).Side2.Add(id(4, k))
        Next
        Return b
    End Function

    ' ------------------------------------------------------------ Models 4 & 5
    ' 9 large circles on a 3x3 grid, 16 small circles on a 4x4 grid, joined by
    ' horizontal / vertical bars (large-large) and diagonal bars (small-large).
    ' Green joins top ↕ bottom, Red joins left ↔ right.
    Private Structure EndRef
        Public Cell As Integer      ' -1 when the end is a side of the board
        Public Team As Integer
        Public Side As Integer      ' 1 or 2
    End Structure

    Private Function CirclesAndBars(barsAreCells As Boolean) As Board
        Dim b As New Board With {.Name = ModelNames(If(barsAreCells, 3, 4))}
        b.Description = If(barsAreCells,
            "25 circles + 76 bars, all of them cells. A chain needs every circle and bar along it.",
            "Only the 25 circles are cells. A bar lights up when it joins two circles of the same team.")
        SetTeam(b, 0, "Green", GreenColor, "top ↕ bottom")
        SetTeam(b, 1, "Red", RedColor, "left ↔ right")
        Dim st As Single = 140, m As Single = 40
        b.Width = 2 * m + 4 * st : b.Height = b.Width
        Dim Wd = b.Width, Ht = b.Height
        Dim LP = Function(x As Single, y As Single) P(m + x * st, m + y * st)
        b.Zones.Add(New Zone With {.Points = {P(0, 0), P(Wd, 0), P(Wd, Ht), P(0, Ht)}, .Team = 1, .FillColor = RedColor})
        b.Zones.Add(New Zone With {.Points = {P(0, 0), P(Wd, 0), P(Wd / 2, Ht / 2)}, .Team = 0, .FillColor = GreenColor})
        b.Zones.Add(New Zone With {.Points = {P(0, Ht), P(Wd, Ht), P(Wd / 2, Ht / 2)}, .Team = 0, .FillColor = GreenColor})
        Dim a0 = m, a1 = Wd - m, cut = 1.15F * st
        b.Zones.Add(New Zone With {.Team = -1, .FillColor = YellowBoard, .Points = {
            P(a0 + cut, a0), P(a1 - cut, a0), P(a1, a0 + cut), P(a1, a1 - cut),
            P(a1 - cut, a1), P(a0 + cut, a1), P(a0, a1 - cut), P(a0, a0 + cut)}})
        For k = 1 To 3
            b.Notches.Add(New Notch With {.Center = LP(k, 0), .Radius = 16, .Team = 0})
            b.Notches.Add(New Notch With {.Center = LP(k, 4), .Radius = 16, .Team = 0})
            b.Notches.Add(New Notch With {.Center = LP(0, k), .Radius = 16, .Team = 1})
            b.Notches.Add(New Notch With {.Center = LP(4, k), .Radius = 16, .Team = 1})
        Next

        ' circles first (indices 0..24)
        Dim big(4, 4) As Integer, small(3, 3) As Integer
        Dim n = 0
        For j = 1 To 3
            For i = 1 To 3
                big(i, j) = b.AddCell(New Cell With {.Shape = CellShape.Circle, .Center = LP(i, j), .Radius = 32, .EmptyColor = Color.White})
            Next
        Next
        For bb = 0 To 3
            For a = 0 To 3
                small(a, bb) = b.AddCell(New Cell With {.Shape = CellShape.Circle, .Center = LP(a + 0.5F, bb + 0.5F), .Radius = 27, .EmptyColor = Color.White})
            Next
        Next
        ' Circles Only (model 5): 25 random labels from the Arabic letters + digits 1-9, new each game.
        ' Circles & Bars (model 4): circles keep letters, because its bars already carry the numbers.
        Dim circleLabels = If(barsAreCells, Letters25, MixedLabels())
        For k = 0 To 24 : b.Cells(k).Label = circleLabels(k) : Next

        ' lattice point (i, j) -> a large circle, a side of the board, or nothing (corner)
        Dim pointRef = Function(i As Integer, j As Integer) As EndRef?
                           If (i = 0 OrElse i = 4) AndAlso (j = 0 OrElse j = 4) Then Return Nothing
                           If j = 0 Then Return New EndRef With {.Cell = -1, .Team = 0, .Side = 1}
                           If j = 4 Then Return New EndRef With {.Cell = -1, .Team = 0, .Side = 2}
                           If i = 0 Then Return New EndRef With {.Cell = -1, .Team = 1, .Side = 1}
                           If i = 4 Then Return New EndRef With {.Cell = -1, .Team = 1, .Side = 2}
                           Return New EndRef With {.Cell = big(i, j), .Team = -1}
                       End Function

        ' collect every bar as (end A, end B, start point, end point)
        Dim bars As New List(Of (A As EndRef, B As EndRef, P1 As PointF, P2 As PointF))
        For j = 1 To 3
            For i = 0 To 3
                bars.Add((pointRef(i, j).Value, pointRef(i + 1, j).Value, LP(i, j), LP(i + 1, j)))
            Next
        Next
        For i = 1 To 3
            For j = 0 To 3
                bars.Add((pointRef(i, j).Value, pointRef(i, j + 1).Value, LP(i, j), LP(i, j + 1)))
            Next
        Next
        Dim isCorner = Function(a As Integer, c As Integer) (a = 0 OrElse a = 3) AndAlso (c = 0 OrElse c = 3)
        For bb = 0 To 3
            For a = 0 To 3
                For di = 0 To 1
                    For dj = 0 To 1
                        Dim pr = pointRef(a + di, bb + dj)
                        If Not pr.HasValue Then Continue For
                        ' corner circles have only their bar to the centre-side large circle
                        If isCorner(a, bb) AndAlso pr.Value.Cell = -1 Then Continue For
                        bars.Add((New EndRef With {.Cell = small(a, bb), .Team = -1}, pr.Value,
                                  LP(a + 0.5F, bb + 0.5F), LP(a + di, bb + dj)))
                    Next
                Next
            Next
        Next

        If barsAreCells Then
            Dim barIds As New List(Of Integer)
            For Each br In bars
                barIds.Add(b.AddCell(New Cell With {.Shape = CellShape.Bar, .P1 = br.P1, .P2 = br.P2, .Thickness = 26,
                    .Label = (barIds.Count + 1).ToString(),
                    .Center = P((br.P1.X + br.P2.X) / 2, (br.P1.Y + br.P2.Y) / 2), .EmptyColor = Color.White}))
            Next
            ' number the bars 1, 2, 3 ... in reading order (top to bottom, left to right)
            Dim ordered = barIds.OrderBy(Function(id) Math.Round(b.Cells(id).Center.Y / 10)).ThenBy(Function(id) b.Cells(id).Center.X).ToList()
            For k = 0 To ordered.Count - 1 : b.Cells(ordered(k)).Label = (k + 1).ToString() : Next
            b.InitNeighbors()
            For k = 0 To bars.Count - 1
                For Each e In {bars(k).A, bars(k).B}
                    If e.Cell >= 0 Then
                        b.Connect(barIds(k), e.Cell)
                    Else
                        AddToSide(b, e.Team, e.Side, barIds(k))
                    End If
                Next
            Next
        Else
            b.InitNeighbors()
            For Each br In bars
                If br.A.Cell >= 0 AndAlso br.B.Cell >= 0 Then
                    b.Connect(br.A.Cell, br.B.Cell)
                    b.Links.Add(New Link With {.CellA = br.A.Cell, .CellB = br.B.Cell, .P1 = br.P1, .P2 = br.P2})
                Else
                    Dim cellEnd = If(br.A.Cell >= 0, br.A, br.B), sideEnd = If(br.A.Cell >= 0, br.B, br.A)
                    AddToSide(b, sideEnd.Team, sideEnd.Side, cellEnd.Cell)
                    b.Links.Add(New Link With {.CellA = cellEnd.Cell, .CellB = -1, .SideTeam = sideEnd.Team,
                        .P1 = If(br.A.Cell >= 0, br.P1, br.P2), .P2 = If(br.A.Cell >= 0, br.P2, br.P1)})
                End If
            Next
        End If

        ' corner circles touch two coloured corners directly
        AddToSide(b, 0, 1, small(0, 0)) : AddToSide(b, 1, 1, small(0, 0))
        AddToSide(b, 0, 1, small(3, 0)) : AddToSide(b, 1, 2, small(3, 0))
        AddToSide(b, 0, 2, small(0, 3)) : AddToSide(b, 1, 1, small(0, 3))
        AddToSide(b, 0, 2, small(3, 3)) : AddToSide(b, 1, 2, small(3, 3))
        Return b
    End Function

    ' ---------------------------------------------------------------- Model 6
    ' The same 25 circles as models 4 and 5 (9 large on a 3x3 grid, 16 small on a 4x4 grid),
    ' but drawn large enough to touch, with no bars. Each large circle touches its 4 diagonal
    ' small circles; the outer small circles touch the coloured semicircles on the sides.
    ' Green joins top ↕ bottom, Red joins left ↔ right.
    Private Function CirclesNoBars() As Board
        Dim b As New Board With {.Name = ModelNames(5),
            .Description = "25 touching circles, no bars. Two circles are connected when they touch. Green joins top ↕ bottom, Red joins left ↔ right."}
        SetTeam(b, 0, "Green", GreenColor, "top ↕ bottom")
        SetTeam(b, 1, "Red", RedColor, "left ↔ right")
        Dim st As Single = 140, m As Single = 60
        b.Width = 2 * m + 4 * st : b.Height = b.Width
        Dim Wd = b.Width, Ht = b.Height
        Dim LP = Function(x As Single, y As Single) P(m + x * st, m + y * st)
        ' diagonal distance between a large and a small circle = st * 0.7071 = 99
        Dim rBig As Single = 52, rSmall As Single = 47, rNotch As Single = 52
        b.Zones.Add(New Zone With {.Points = {P(0, 0), P(Wd, 0), P(Wd, Ht), P(0, Ht)}, .Team = 1, .FillColor = RedColor})
        b.Zones.Add(New Zone With {.Points = {P(0, 0), P(Wd, 0), P(Wd / 2, Ht / 2)}, .Team = 0, .FillColor = GreenColor})
        b.Zones.Add(New Zone With {.Points = {P(0, Ht), P(Wd, Ht), P(Wd / 2, Ht / 2)}, .Team = 0, .FillColor = GreenColor})
        Dim a0 = m, a1 = Wd - m, cut = 0.6F * st
        b.Zones.Add(New Zone With {.Team = -1, .FillColor = YellowBoard, .Points = {
            P(a0 + cut, a0), P(a1 - cut, a0), P(a1, a0 + cut), P(a1, a1 - cut),
            P(a1 - cut, a1), P(a0 + cut, a1), P(a0, a1 - cut), P(a0, a0 + cut)}})
        For k = 1 To 3
            b.Notches.Add(New Notch With {.Center = LP(k, 0), .Radius = rNotch, .Team = 0, .Outline = False})
            b.Notches.Add(New Notch With {.Center = LP(k, 4), .Radius = rNotch, .Team = 0, .Outline = False})
            b.Notches.Add(New Notch With {.Center = LP(0, k), .Radius = rNotch, .Team = 1, .Outline = False})
            b.Notches.Add(New Notch With {.Center = LP(4, k), .Radius = rNotch, .Team = 1, .Outline = False})
        Next
        Dim big(3, 3) As Integer, small(3, 3) As Integer
        For j = 1 To 3
            For i = 1 To 3
                big(i, j) = b.AddCell(New Cell With {.Shape = CellShape.Circle, .Center = LP(i, j), .Radius = rBig, .EmptyColor = Color.White})
            Next
        Next
        For bb = 0 To 3
            For a = 0 To 3
                small(a, bb) = b.AddCell(New Cell With {.Shape = CellShape.Circle, .Center = LP(a + 0.5F, bb + 0.5F), .Radius = rSmall, .EmptyColor = Color.White})
            Next
        Next
        Dim labels = MixedLabels()
        For k = 0 To 24 : b.Cells(k).Label = labels(k) : Next
        b.InitNeighbors()
        For j = 1 To 3
            For i = 1 To 3
                ' a large circle at lattice point (i, j) touches the small circles around it
                For Each d In {(-1, -1), (0, -1), (-1, 0), (0, 0)}
                    b.Connect(big(i, j), small(i + d.Item1, j + d.Item2))
                Next
            Next
        Next
        For k = 0 To 3
            AddToSide(b, 0, 1, small(k, 0)) : AddToSide(b, 0, 2, small(k, 3))
            AddToSide(b, 1, 1, small(0, k)) : AddToSide(b, 1, 2, small(3, k))
        Next
        Return b
    End Function

    Private Sub AddToSide(b As Board, team As Integer, side As Integer, cell As Integer)
        If side = 1 Then b.Teams(team).Side1.Add(cell) Else b.Teams(team).Side2.Add(cell)
    End Sub
End Module
