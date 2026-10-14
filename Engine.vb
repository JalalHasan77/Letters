Imports System.Threading
Imports System.Threading.Tasks

' ---------------------------------------------------------------------------
'  Game logic: win detection and the probability calculations.
'  owner(i) = -1 (empty), 0 (team 0) or 1 (team 1)
' ---------------------------------------------------------------------------

Public Class SimResult
    Public Property Team0Win As Double
    Public Property Team1Win As Double
    Public Property NoWinner As Double
    Public Property Games As Long
    Public Property IsExact As Boolean
End Class

Public Module Engine

    ''' <summary>Returns the shortest winning chain for a team, or Nothing.</summary>
    Public Function FindWinPath(b As Board, owner As Integer(), team As Integer) As List(Of Integer)
        Dim t = b.Teams(team)
        Dim parent(b.CellCount - 1) As Integer
        For i = 0 To parent.Length - 1 : parent(i) = -2 : Next
        Dim q As New Queue(Of Integer)
        For Each c In t.Side1
            If owner(c) = team Then parent(c) = -1 : q.Enqueue(c)
        Next
        While q.Count > 0
            Dim c = q.Dequeue()
            If t.Side2.Contains(c) Then
                Dim path As New List(Of Integer)
                Dim x = c
                While x >= 0 : path.Add(x) : x = parent(x) : End While
                path.Reverse()
                Return path
            End If
            For Each nb In b.Neighbors(c)
                If parent(nb) = -2 AndAlso owner(nb) = team Then parent(nb) = c : q.Enqueue(nb)
            Next
        End While
        Return Nothing
    End Function

    ''' <summary>True if the team could still win using its own cells plus empty ones.</summary>
    Public Function CanStillWin(b As Board, owner As Integer(), team As Integer) As Boolean
        Dim other = 1 - team
        Dim open = Function(c As Integer) owner(c) <> other
        Dim seen(b.CellCount - 1) As Boolean
        Dim st As New Stack(Of Integer)
        For Each c In b.Teams(team).Side1
            If open(c) Then seen(c) = True : st.Push(c)
        Next
        While st.Count > 0
            Dim c = st.Pop()
            If b.Teams(team).Side2.Contains(c) Then Return True
            For Each nb In b.Neighbors(c)
                If Not seen(nb) AndAlso open(nb) Then seen(nb) = True : st.Push(nb)
            Next
        End While
        Return False
    End Function

    ' ------------------------------------------------------------ simulation

    ''' <summary>
    ''' Plays many random games. Each cell goes to team 0 with probability p0
    ''' (the chance team 0 answers a question faster). Filling the whole board and
    ''' then checking gives the same result as stopping at the first line, because
    ''' the two teams can never both complete a line.
    ''' </summary>
    Public Function MonteCarlo(b As Board, games As Long, p0 As Double,
                               progress As IProgress(Of Double), ct As CancellationToken) As SimResult
        Dim n = b.CellCount
        Dim nbs = b.Neighbors.Select(Function(l) l.ToArray()).ToArray()
        Dim s1(1)() As Boolean, s2(1)() As Boolean
        For t = 0 To 1
            s1(t) = New Boolean(n - 1) {} : s2(t) = New Boolean(n - 1) {}
            For Each c In b.Teams(t).Side1 : s1(t)(c) = True : Next
            For Each c In b.Teams(t).Side2 : s2(t)(c) = True : Next
        Next
        Dim chunks = Math.Max(1, Environment.ProcessorCount * 8)
        Dim per = games \ chunks
        Dim w0 As Long = 0, w1 As Long = 0, done As Long = 0
        Dim seedBase = Environment.TickCount
        Parallel.For(0, chunks, New ParallelOptions With {.CancellationToken = ct},
            Sub(ci)
                Dim rnd As New Random(seedBase + ci * 7919)
                Dim own(n - 1) As Integer, stack(n - 1) As Integer, mark(n - 1) As Integer
                Dim stamp = 0
                Dim count = If(ci = chunks - 1, games - per * (chunks - 1), per)
                Dim lw0 As Long = 0, lw1 As Long = 0
                For g As Long = 1 To count
                    For i = 0 To n - 1 : own(i) = If(rnd.NextDouble() < p0, 0, 1) : Next
                    For t = 0 To 1
                        stamp += 1
                        Dim sp = 0, won = False
                        For i = 0 To n - 1
                            If s1(t)(i) AndAlso own(i) = t Then mark(i) = stamp : stack(sp) = i : sp += 1
                        Next
                        While sp > 0
                            sp -= 1 : Dim c = stack(sp)
                            If s2(t)(c) Then won = True : Exit While
                            For Each nb In nbs(c)
                                If mark(nb) <> stamp AndAlso own(nb) = t Then mark(nb) = stamp : stack(sp) = nb : sp += 1
                            Next
                        End While
                        If won Then
                            If t = 0 Then lw0 += 1 Else lw1 += 1
                        End If
                    Next
                    If (g And 4095) = 0 Then
                        ct.ThrowIfCancellationRequested()
                        progress?.Report(Interlocked.Add(done, 4096) / CDbl(games))
                    End If
                Next
                Interlocked.Add(w0, lw0) : Interlocked.Add(w1, lw1)
            End Sub)
        Return New SimResult With {.Games = games, .IsExact = False,
            .Team0Win = w0 / CDbl(games), .Team1Win = w1 / CDbl(games),
            .NoWinner = 1 - (w0 + w1) / CDbl(games)}
    End Function

    Public Const MaxExactCells As Integer = 28

    ''' <summary>
    ''' Exact answer: checks every one of the 2^n ways the cells can be split and
    ''' weights each by its probability. Only for boards with up to 28 cells.
    ''' </summary>
    Public Function Exact(b As Board, p0 As Double,
                          progress As IProgress(Of Double), ct As CancellationToken) As SimResult
        Dim n = b.CellCount
        If n > MaxExactCells Then Throw New InvalidOperationException("Too many cells for an exact count.")
        Dim nbMask(n - 1) As ULong
        For i = 0 To n - 1
            For Each j In b.Neighbors(i) : nbMask(i) = nbMask(i) Or (1UL << j) : Next
        Next
        Dim side = Function(hs As HashSet(Of Integer)) As ULong
                       Dim m As ULong = 0
                       For Each c In hs : m = m Or (1UL << c) : Next
                       Return m
                   End Function
        Dim a1 = side(b.Teams(0).Side1), a2 = side(b.Teams(0).Side2)
        Dim b1 = side(b.Teams(1).Side1), b2 = side(b.Teams(1).Side2)
        Dim full As ULong = (1UL << n) - 1UL
        ' weight(k) = p0^k (1-p0)^(n-k) for a board where team 0 owns k cells
        Dim weight(n) As Double
        For k = 0 To n : weight(k) = Math.Pow(p0, k) * Math.Pow(1 - p0, n - k) : Next

        Dim hiBits = Math.Min(10, n)
        Dim chunks = 1 << hiBits, lowBits = n - hiBits
        Dim r0(chunks - 1) As Double, r1(chunks - 1) As Double
        Dim done As Long = 0
        Parallel.For(0, chunks, New ParallelOptions With {.CancellationToken = ct},
            Sub(ci)
                Dim l0 As Double = 0, l1 As Double = 0
                Dim baseMask = CULng(ci) << lowBits
                For lo As ULong = 0 To (1UL << lowBits) - 1UL
                    Dim m0 = baseMask Or lo                 ' cells of team 0
                    Dim m1 = full And Not m0                ' cells of team 1
                    Dim w = weight(PopCount(m0))
                    If Connected(m0, a1, a2, nbMask) Then
                        l0 += w
                    ElseIf Connected(m1, b1, b2, nbMask) Then
                        l1 += w
                    End If
                Next
                r0(ci) = l0 : r1(ci) = l1
                Dim d = Interlocked.Increment(done)
                If (d And 15) = 0 Then ct.ThrowIfCancellationRequested() : progress?.Report(d / CDbl(chunks))
            End Sub)
        Dim t0 = r0.Sum(), t1 = r1.Sum()
        Return New SimResult With {.Games = CLng(1) << n, .IsExact = True,
            .Team0Win = t0, .Team1Win = t1, .NoWinner = Math.Max(0, 1 - t0 - t1)}
    End Function

    ''' <summary>Flood fill on bit masks: is there a chain of 'own' cells from side s to side t?</summary>
    Private Function Connected(own As ULong, s As ULong, t As ULong, nb As ULong()) As Boolean
        Dim reach = s And own
        Dim frontier = reach
        While frontier <> 0UL
            If (reach And t) <> 0UL Then Return True
            Dim nxt As ULong = 0
            Dim f = frontier
            While f <> 0UL
                nxt = nxt Or nb(TrailingZeros(f))
                f = f And (f - 1UL)
            End While
            nxt = nxt And own And Not reach
            reach = reach Or nxt
            frontier = nxt
        End While
        Return (reach And t) <> 0UL
    End Function
    ' Bit helpers (work on both .NET Framework and .NET 8)
    Private Function PopCount(v As ULong) As Integer
#If NET Then
        Return System.Numerics.BitOperations.PopCount(v)
#Else
        Dim c = 0
        While v <> 0UL : v = v And (v - 1UL) : c += 1 : End While
        Return c
#End If
    End Function

    Private Function TrailingZeros(v As ULong) As Integer
#If NET Then
        Return System.Numerics.BitOperations.TrailingZeroCount(v)
#Else
        Dim c = 0
        While (v And &HFFUL) = 0UL : v >>= 8 : c += 8 : End While
        While (v And 1UL) = 0UL : v >>= 1 : c += 1 : End While
        Return c
#End If
    End Function
End Module
