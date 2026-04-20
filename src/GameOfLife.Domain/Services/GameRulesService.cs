using GameOfLife.Domain.Entities;
using GameOfLife.Domain.Exceptions;

namespace GameOfLife.Domain.Services;

public sealed class GameRulesService
{
    public Board NextGeneration(Board board)
    {
        var cells = new bool[board.Rows * board.Cols];

        for (int row = 0; row < board.Rows; row++)
        {
            for (int col = 0; col < board.Cols; col++)
            {
                int liveNeighbors = CountLiveNeighbors(board, row, col);
                bool isCurrentlyAlive = board.IsAlive(row, col);
                
                // Rule 1: Live cell with < 2 neighbors dies (underpopulation)
                // Rule 2: Live cell with 2-3 neighbors survives
                // Rule 3: Live cell with > 3 neighbors dies (overpopulation)
                // Rule 4: Dead cell with exactly 3 neighbors becomes alive (reproduction)
                bool willBeAlive = isCurrentlyAlive switch
                {
                    true => liveNeighbors == 2 || liveNeighbors == 3, // Rules 2 (survives if 2-3)
                    false => liveNeighbors == 3 // Rule 4 (births on 3)
                };

                cells[row * board.Cols + col] = willBeAlive;
            }
        }

        return new Board(board.Rows, board.Cols, cells, board.Generation + 1);
    }

    public Board Advance(Board board, int steps)
    {
        if (steps < 0) throw new ArgumentException("Steps must be non-negative", nameof(steps));

        var current = board;
        for (int i = 0; i < steps; i++)
        {
            current = NextGeneration(current);
        }
        return current;
    }

    /// <summary>
    /// Advances the board until it reaches a stable state — either a still life
    /// (identical to the previous generation) or an oscillator (revisits a prior state).
    /// Throws <see cref="BoardDidNotStabilizeException"/> if no stable state is reached
    /// within <paramref name="maxIterations"/> generations.
    /// </summary>
    public Board FindFinalState(string boardId, Board board, int maxIterations)
    {
        if (maxIterations <= 0)
            throw new ArgumentException("maxIterations must be positive", nameof(maxIterations));

        var seen = new HashSet<Board> { board };
        var current = board;

        for (int i = 0; i < maxIterations; i++)
        {
            var next = NextGeneration(current);

            // Still life: state unchanged.
            // Example: A 2x2 block pattern never changes
            if (next.Equals(current)) return next;

            // Oscillator / cycle: the new state was seen before.
            // Example: A blinker alternates between vertical and horizontal (period 2)
            if (!seen.Add(next)) return next;

            current = next;
        }

        throw new BoardDidNotStabilizeException(boardId, maxIterations);
    }

    /// <summary>
    /// Counts live neighbors for a cell at (row, col).
    /// Checks all 8 surrounding cells (up, down, left, right, diagonals).
    /// Uses toroidal wrapping at grid boundaries (edges wrap around).
    /// </summary>
    private static int CountLiveNeighbors(Board board, int row, int col)
    {
        int count = 0;
        // Check all 8 neighbors: (-1,-1), (-1,0), (-1,1), (0,-1), (0,1), (1,-1), (1,0), (1,1)
        for (int deltaRow = -1; deltaRow <= 1; deltaRow++)
        {
            for (int deltaCol = -1; deltaCol <= 1; deltaCol++)
            {
                // Skip the cell itself (only count neighbors, not self)
                if (deltaRow == 0 && deltaCol == 0) continue;
                
                // Board.IsAlive() handles toroidal wrapping internally
                if (board.IsAlive(row + deltaRow, col + deltaCol))
                {
                    count++;
                }
            }
        }

        return count;
    }
}
