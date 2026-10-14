# Letters Contest Boards (VB.NET)

A Windows Forms app for running the letters contest on five board designs and
calculating each team's chance of winning.

## Boards

| # | Board | Cells | Teams |
|---|-------|-------|-------|
| 1 | Hexagons (original rectangle) | 25 | Red left ↔ right, Green top ↕ bottom |
| 2 | Hexagons (diamond) | 25 | Red slanted sides, Green top ↕ bottom |
| 3 | Octagons & Squares | 25 | Pink top ↕ bottom, Green left ↔ right |
| 4 | Circles & Bars (bars are cells) | 101 | Green top ↕ bottom, Pink left ↔ right |
| 5 | Circles Only (bars are links) | 25 | Green top ↕ bottom, Pink left ↔ right |

## Windows

- **Control panel**: choose the board, run the contest, calculate win chances. Closing it exits the app.
- **Board window** (16:9): the board in the middle and team score cards on each side; winner messages
  appear at the bottom. **Board full screen**, **F11** or a double-click on the board toggles full
  screen, which is handy on a TV or projector. Closing the board window only hides it; use **Show board**.
- **Arabic keyboard**: opened with **Show Arabic keyboard**. It does not take the focus away, so the
  physical keyboard keeps working.

## Running a contest

1. **Select a cell** by typing its letter on the physical keyboard (Windows set to Arabic *or* English;
   an English key selects the Arabic letter printed on the same key), or by clicking the letter on the
   on-screen keyboard. The selected cell **blinks** on the board and its key lights up.
   - If two cells share a label, press it again to move to the other one.
   - On board 4 type a bar number (e.g. 4 then 7 for bar 47).
2. **Turn to Red / Turn to Green** (keyboard keys, panel buttons, **F1/Enter** = Red-side team,
   **F2/Space** = Green team). On boards whose other team is Pink the key says **Turn to Pink**.
3. **De-select** (key, button, or **Esc**) cancels the selection.

Other controls:
- **Pick random cell** selects a random empty cell.
- On the board: left-click = first team, right-click = second team, Shift+click = clear.
- **Undo** reverses the last change; **New game** starts over.
- The app announces the winner as soon as a team joins its two sides and outlines the
  winning chain. On board 4 it also says when neither team can win any more.
- **Auto-play demo** plays a random game; the speed box sets the delay per move.
- **Hexagon pose** (boards 1 and 2): *Horizontal* = flat-top hexagons, *Vertical* = pointy-top.
  On board 1 this swaps the advantage: horizontal Red 53.46 % / Green 46.54 %,
  vertical Red 46.54 % / Green 53.46 %. Board 2 stays 50 / 50 in both poses.
- **Letters font** chooses the font for the letters on the board and keyboard; **Bold** for heavier letters.
  The default is the **Al-Arabiya** font when it is installed (any font whose name contains "Arabiya"),
  otherwise Tahoma. The letter ه is always shown as **هـ** so it is not mistaken for a zero or a circle.
- Boards 1, 2, 3 and 5: every **New game** (or board change) draws 25 labels at random from the
  28 Arabic letters plus the digits 1–9, so each game has a different mix and no label repeats.
  Type a digit (or click it on the keyboard) to select a digit cell.
- Board 4: circles carry letters and the 76 bars carry the numbers 1–76 in reading order.

## Win chances

- **Team answers faster (%)**: how often the first team wins a question (50 = equal teams).
- With **Exact count** ticked, boards with 28 cells or fewer are solved exactly by checking
  every way the cells can be split (2^25 = 33.5 million for 25 cells; a few seconds).
- Board 4 (101 cells) is simulated with the number of games you choose.

Expected results with equal teams: board 1 → Red 53.46 % / Green 46.54 %;
boards 2, 3, 5 → 50 % / 50 %; board 4 → about 11.9 % each and 76.2 % no winner.

## Building (Visual Studio 2019)

This version targets .NET Framework 4.7.2, which Visual Studio 2019 supports out of the box.

1. Unzip the folder.
2. In Visual Studio 2019: **File → Open → Project/Solution**, choose `ContestBoards.vbproj`.
3. Press **F5** (or **Debug → Start Debugging**).

The first time, Visual Studio creates a solution file; save it when asked.
If you see "the .NET Framework 4.7.2 targeting pack is not installed", run the
Visual Studio Installer → Modify → Individual components → tick
".NET Framework 4.7.2 targeting pack" (or change `net472` to `net48` in the .vbproj).

## Files

- `Models.vb` – cells, teams, links and the board container
- `BoardFactory.vb` – geometry and neighbours for the five boards
- `Engine.vb` – win detection, exact count and simulation
- `BoardView.vb` – draws the 16:9 board picture and handles clicks
- `BoardForm.vb` – the board window (16:9, full screen)
- `KeyboardForm.vb` – the on-screen Arabic keyboard
- `ArabicKeys.vb` – Arabic keyboard layout and key translation
- `MainForm.vb` – the control panel
- `Program.vb` – entry point
