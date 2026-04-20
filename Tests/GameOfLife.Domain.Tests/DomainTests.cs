using GameOfLife.Domain.Entities;
using GameOfLife.Domain.Exceptions;
using GameOfLife.Domain.Services;

namespace GameOfLife.Domain.Tests;

public class BoardTests
{
    [Fact]
    public void Constructor_throws_when_dimensions_invalid()
    {
        Assert.Throws<ArgumentException>(() => new Board(0, 1, new bool[0]));
        Assert.Throws<ArgumentException>(() => new Board(1, 0, new bool[0]));
        Assert.Throws<ArgumentException>(() => new Board(1, 1, new bool[2]));
        Assert.Throws<ArgumentException>(() => new Board(1, 1, new bool[1], generation: -1));
    }

    [Fact]
    public void IsAlive_returns_false_for_out_of_bounds()
    {
        var board = new Board(2, 2, new[] { true, true, true, true });
        Assert.False(board.IsAlive(-1, 0));
        Assert.False(board.IsAlive(0, 2));
    }

    [Fact]
    public void Equality_compares_dimensions_and_cells_but_ignores_generation()
    {
        var a = new Board(2, 2, new[] { true, false, false, true }, generation: 0);
        var b = new Board(2, 2, new[] { true, false, false, true }, generation: 5);
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }
}

public class GameRulesServiceTests
{
    private readonly GameRulesService _rules = new();

    [Fact]
    public void NextGeneration_block_is_a_still_life()
    {
        // 4x4 with a 2x2 block in the middle.
        var board = Grid(new[,]
        {
            { 0, 0, 0, 0 },
            { 0, 1, 1, 0 },
            { 0, 1, 1, 0 },
            { 0, 0, 0, 0 },
        });

        var next = _rules.NextGeneration(board);

        Assert.Equal(board, next);
        Assert.Equal(1, next.Generation);
    }

    [Fact]
    public void NextGeneration_blinker_oscillates_with_period_two()
    {
        var vertical = Grid(new[,]
        {
            { 0, 0, 0, 0, 0 },
            { 0, 0, 1, 0, 0 },
            { 0, 0, 1, 0, 0 },
            { 0, 0, 1, 0, 0 },
            { 0, 0, 0, 0, 0 },
        });

        var horizontal = _rules.NextGeneration(vertical);
        var backToVertical = _rules.NextGeneration(horizontal);

        Assert.NotEqual(vertical, horizontal);
        Assert.Equal(vertical, backToVertical);
    }

    [Fact]
    public void FindFinalState_returns_still_life_when_already_stable()
    {
        var block = Grid(new[,]
        {
            { 1, 1 },
            { 1, 1 },
        });

        var final = _rules.FindFinalState("board_test", block, maxIterations: 10);
        Assert.Equal(block, final);
    }

    [Fact]
    public void FindFinalState_detects_oscillator()
    {
        var blinker = Grid(new[,]
        {
            { 0, 0, 0, 0, 0 },
            { 0, 0, 1, 0, 0 },
            { 0, 0, 1, 0, 0 },
            { 0, 0, 1, 0, 0 },
            { 0, 0, 0, 0, 0 },
        });

        var final = _rules.FindFinalState("board_test", blinker, maxIterations: 100);

        // Blinker has period 2, so cycle is detected at generation 2.
        Assert.Equal(2, final.Generation);
        Assert.Equal(blinker, final);
    }

    [Fact]
    public void FindFinalState_throws_when_grid_does_not_stabilize()
    {
        // A glider on a sufficiently large field translates indefinitely; with
        // a tiny iteration budget the algorithm must give up.
        var glider = Grid(new[,]
        {
            { 0, 0, 0, 0, 0, 0 },
            { 0, 0, 1, 0, 0, 0 },
            { 0, 0, 0, 1, 0, 0 },
            { 0, 1, 1, 1, 0, 0 },
            { 0, 0, 0, 0, 0, 0 },
            { 0, 0, 0, 0, 0, 0 },
        });

        var ex = Assert.Throws<BoardDidNotStabilizeException>(
            () => _rules.FindFinalState("board_glider", glider, maxIterations: 3));

        Assert.Equal("board_glider", ex.BoardId);
        Assert.Equal(3, ex.MaxIterations);
    }

    private static Board Grid(int[,] cells)
    {
        var rows = cells.GetLength(0);
        var cols = cells.GetLength(1);
        var flat = new bool[rows * cols];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                flat[r * cols + c] = cells[r, c] == 1;
        return new Board(rows, cols, flat);
    }
}

public class BoardIdTests
{
    [Fact]
    public void New_produces_expected_format()
    {
        var id = BoardId.New();
        Assert.StartsWith("board_", id);
        Assert.True(BoardId.IsValid(id));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("not-a-board-id")]
    public void IsValid_rejects_obviously_bad_ids(string? id)
        => Assert.False(BoardId.IsValid(id));
}
