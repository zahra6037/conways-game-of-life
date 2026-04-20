using System.Collections.Concurrent;
using GameOfLife.Application.Ports;
using GameOfLife.Application.Services;
using GameOfLife.Domain.Entities;
using GameOfLife.Domain.Exceptions;
using GameOfLife.Domain.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace GameOfLife.Application.Tests;

public class GameServiceTests
{
    private readonly InMemoryBoardRepository _repo = new();
    private readonly GameService _service;

    public GameServiceTests()
    {
        _service = new GameService(_repo, new GameRulesService(), NullLogger<GameService>.Instance);
    }

    [Fact]
    public async Task CreateBoardAsync_persists_and_returns_id_with_initial_generation()
    {
        var (id, board) = await _service.CreateBoardAsync(2, 2, AllAlive(2, 2));

        Assert.True(BoardId.IsValid(id));
        Assert.Equal(0, board.Generation);
        Assert.NotNull(await _repo.GetAsync(id));
    }

    [Fact]
    public async Task GetNextStateAsync_does_not_mutate_persisted_state()
    {
        var (id, _) = await _service.CreateBoardAsync(2, 2, AllAlive(2, 2));

        var next = await _service.GetNextStateAsync(id);
        var stored = await _repo.GetAsync(id);

        Assert.Equal(1, next.Generation);
        Assert.Equal(0, stored!.Generation);
    }

    [Fact]
    public async Task Methods_throw_BoardNotFoundException_for_unknown_or_invalid_ids()
    {
        await Assert.ThrowsAsync<BoardNotFoundException>(() => _service.GetNextStateAsync("not-a-board"));
        await Assert.ThrowsAsync<BoardNotFoundException>(() => _service.GetNextStateAsync("board_missing_1"));
    }

    [Fact]
    public async Task GetStateAheadAsync_throws_for_negative_n()
        => await Assert.ThrowsAsync<ArgumentException>(() => _service.GetStateAheadAsync("board_x_1", -1));

    private static bool[] AllAlive(int rows, int cols)
        => Enumerable.Repeat(true, rows * cols).ToArray();

    private sealed class InMemoryBoardRepository : IBoardRepository
    {
        private readonly ConcurrentDictionary<string, Board> _store = new();

        public Task<Board?> GetAsync(string id, CancellationToken ct = default)
            => Task.FromResult(_store.TryGetValue(id, out var b) ? b : null);

        public Task SaveAsync(string id, Board board, CancellationToken ct = default)
        {
            _store[id] = board;
            return Task.CompletedTask;
        }

        public Task<bool> DeleteAsync(string id, CancellationToken ct = default)
            => Task.FromResult(_store.TryRemove(id, out _));
    }
}
