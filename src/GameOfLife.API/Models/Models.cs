using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using GameOfLife.Domain.Entities;

namespace GameOfLife.API.Models;

/// <summary>
/// Request body for uploading a new board state.
/// Grid is a 2D array of cells where 1 = alive and 0 = dead.
/// </summary>
public sealed class UploadBoardRequest
{
    public const int MaxDimension = 1000;
    public const int MaxCells = 1_000_000;

    [JsonPropertyName("grid")]
    public int[][]? Grid { get; init; }

    public (int Rows, int Cols, bool[] Cells) Validate()
    {
        if (Grid is null)
            throw new ValidationException("'grid' is required.");
        if (Grid.Length == 0)
            throw new ValidationException("'grid' must be a non-empty 2D array.");
        if (Grid.Length > MaxDimension)
            throw new ValidationException(
                $"'grid' has too many rows ({Grid.Length}); maximum is {MaxDimension}.");

        var rows = Grid.Length;

        if (Grid[0] is null)
            throw new ValidationException("'grid' row 0 must not be null.");
        var cols = Grid[0]!.Length;
        if (cols == 0)
            throw new ValidationException("'grid' rows must contain at least one cell.");
        if (cols > MaxDimension)
            throw new ValidationException(
                $"'grid' has too many columns ({cols}); maximum is {MaxDimension}.");

        if ((long)rows * cols > MaxCells)
            throw new ValidationException(
                $"'grid' has {(long)rows * cols} cells; maximum is {MaxCells}.");

        var cells = new bool[rows * cols];
        for (int r = 0; r < rows; r++)
        {
            var row = Grid[r];
            if (row is null)
                throw new ValidationException($"'grid' row {r} must not be null.");
            if (row.Length != cols)
                throw new ValidationException(
                    $"'grid' must be rectangular: row {r} has length {row.Length}, expected {cols}.");

            for (int c = 0; c < cols; c++)
            {
                var v = row[c];
                if (v != 0 && v != 1)
                    throw new ValidationException(
                        $"'grid' cells must be 0 or 1; got {v} at ({r}, {c}).");

                cells[r * cols + c] = v == 1;
            }
        }

        return (rows, cols, cells);
    }
}

public sealed record UploadBoardResponse(
    [property: JsonPropertyName("boardId")] string BoardId,
    [property: JsonPropertyName("initialState")] int[][] InitialState,
    [property: JsonPropertyName("generation")] int Generation);

public sealed record BoardStateResponse(
    [property: JsonPropertyName("boardId")] string BoardId,
    [property: JsonPropertyName("state")] int[][] State,
    [property: JsonPropertyName("generation")] int Generation)
{
    public static BoardStateResponse From(string boardId, Board board) =>
        new(boardId, GridConverter.ToGrid(board), board.Generation);
}

internal static class GridConverter
{
    public static int[][] ToGrid(Board board)
    {
        var flat = board.GetCells();
        var grid = new int[board.Rows][];
        for (int r = 0; r < board.Rows; r++)
        {
            grid[r] = new int[board.Cols];
            for (int c = 0; c < board.Cols; c++)
                grid[r][c] = flat[r * board.Cols + c] ? 1 : 0;
        }
        return grid;
    }
}
