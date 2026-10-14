Imports System.Drawing

' ---------------------------------------------------------------------------
'  Data model shared by every board.
'  A board is a set of cells. Two teams compete; each team owns two opposite
'  sides of the board and wins by joining them with a chain of its own cells.
' ---------------------------------------------------------------------------

Public Enum CellShape
    Polygon     ' hexagons, octagons, squares
    Circle      ' circles
    Bar         ' a thick bar between two points (a cell in model 4)
End Enum

Public Class Cell
    Public Property Index As Integer
    Public Property Shape As CellShape
    Public Property Points As PointF()          ' polygon vertices (Polygon)
    Public Property Center As PointF            ' polygon / circle centre
    Public Property Radius As Single            ' circle radius
    Public Property P1 As PointF                ' bar start (Bar)
    Public Property P2 As PointF                ' bar end (Bar)
    Public Property Thickness As Single         ' bar thickness (Bar)
    Public Property Label As String = ""
    Public Property EmptyColor As Color = Color.White

    ''' <summary>Polygon outline of a bar, used for drawing and hit-testing.</summary>
    Public Function BarPolygon() As PointF()
        Dim dx = P2.X - P1.X, dy = P2.Y - P1.Y
        Dim len = CSng(Math.Sqrt(dx * dx + dy * dy))
        Dim nx = -dy / len * Thickness / 2, ny = dx / len * Thickness / 2
        Return {New PointF(P1.X + nx, P1.Y + ny), New PointF(P2.X + nx, P2.Y + ny),
                New PointF(P2.X - nx, P2.Y - ny), New PointF(P1.X - nx, P1.Y - ny)}
    End Function
End Class

''' <summary>
''' A non-cell connector drawn between two circles (model 5, "Circles Only").
''' It lights up in a team's colour when it joins two cells of that team,
''' or one cell of that team to one of that team's own sides.
''' </summary>
Public Class Link
    Public Property CellA As Integer
    Public Property CellB As Integer = -1       ' -1 when the link runs to a side
    Public Property SideTeam As Integer = -1    ' team owning that side (when CellB = -1)
    Public Property P1 As PointF
    Public Property P2 As PointF
End Class

''' <summary>A coloured background region (a team's side area).</summary>
Public Class Zone
    Public Property Points As PointF()
    Public Property Team As Integer             ' -1 = neutral board fill
    Public Property FillColor As Color
End Class

''' <summary>A small coloured dot on the border where a chain must arrive.</summary>
Public Class Notch
    Public Property Center As PointF
    Public Property Radius As Single
    Public Property Team As Integer
End Class

Public Class TeamInfo
    Public Property Name As String
    Public Property Color As Color
    Public Property Sides As String             ' e.g. "left ↔ right"
    ''' <summary>Cells touching this team's first and second side.</summary>
    Public Property Side1 As New HashSet(Of Integer)
    Public Property Side2 As New HashSet(Of Integer)
End Class

Public Class Board
    Public Property Name As String
    Public Property Description As String
    Public Property Width As Single
    Public Property Height As Single
    Public Property Cells As New List(Of Cell)
    Public Property Neighbors As List(Of Integer)()
    Public Property Teams As TeamInfo() = {New TeamInfo(), New TeamInfo()}
    Public Property Zones As New List(Of Zone)
    Public Property Notches As New List(Of Notch)
    Public Property Links As New List(Of Link)
    Public Property LabelColor As Color = Color.Black

    Public ReadOnly Property CellCount As Integer
        Get
            Return Cells.Count
        End Get
    End Property

    Public Function AddCell(c As Cell) As Integer
        c.Index = Cells.Count
        Cells.Add(c)
        Return c.Index
    End Function

    Public Sub InitNeighbors()
        Dim arr(Cells.Count - 1) As List(Of Integer)
        For i = 0 To Cells.Count - 1
            arr(i) = New List(Of Integer)()
        Next
        Neighbors = arr
    End Sub

    Public Sub Connect(a As Integer, b As Integer)
        If a = b Then Return
        If Not Neighbors(a).Contains(b) Then Neighbors(a).Add(b)
        If Not Neighbors(b).Contains(a) Then Neighbors(b).Add(a)
    End Sub
End Class
