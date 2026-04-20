using System.Text.Json;
using GameOfLife.Application.Ports;
using GameOfLife.Domain.Entities;
using Microsoft.Data.Sqlite;

namespace GameOfLife.Infrastructure.Persistence;

public sealed class SqliteBoardRepository : IBoardRepository
{
    private readonly string _connectionString;

    public SqliteBoardRepository(string connectionString)
    {
        _connectionString = connectionString;
        InitializeDatabase();
    }

    public async Task<Board?> GetAsync(string id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT Rows, Cols, Generation, Cells
            FROM Boards
            WHERE Id = @id";
        command.Parameters.AddWithValue("@id", id);

        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        var rows = reader.GetInt32(0);
        var cols = reader.GetInt32(1);
        var generation = reader.GetInt32(2);
        var cellsJson = reader.GetString(3);
        var cells = JsonSerializer.Deserialize<bool[]>(cellsJson)
                    ?? throw new InvalidOperationException($"Corrupted cell data for board '{id}'.");

        return new Board(rows, cols, cells, generation);
    }

    public async Task SaveAsync(string id, Board board, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(board);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Boards (Id, Rows, Cols, Generation, Cells, CreatedAt, UpdatedAt)
            VALUES (@id, @rows, @cols, @generation, @cells, @now, @now)
            ON CONFLICT(Id) DO UPDATE SET
                Rows = excluded.Rows,
                Cols = excluded.Cols,
                Generation = excluded.Generation,
                Cells = excluded.Cells,
                UpdatedAt = excluded.UpdatedAt";

        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@rows", board.Rows);
        command.Parameters.AddWithValue("@cols", board.Cols);
        command.Parameters.AddWithValue("@generation", board.Generation);
        command.Parameters.AddWithValue("@cells", JsonSerializer.Serialize(board.GetCells()));
        command.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Boards WHERE Id = @id";
        command.Parameters.AddWithValue("@id", id);

        var affected = await command.ExecuteNonQueryAsync(ct);
        return affected > 0;
    }

    private void InitializeDatabase()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS Boards (
                Id          TEXT    PRIMARY KEY,
                Rows        INTEGER NOT NULL,
                Cols        INTEGER NOT NULL,
                Generation  INTEGER NOT NULL DEFAULT 0,
                Cells       TEXT    NOT NULL,
                CreatedAt   INTEGER NOT NULL,
                UpdatedAt   INTEGER NOT NULL
            )";
        command.ExecuteNonQuery();
    }
}
