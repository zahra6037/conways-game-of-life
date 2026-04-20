using GameOfLife.Domain.Entities;

namespace GameOfLife.Application.Ports;

public interface IGameService
{
    Task<(string BoardId, Board Board)> CreateBoardAsync(int rows, int cols, bool[] initialCells, CancellationToken ct = default);
    Task<Board> GetInitialStateAsync(string boardId, CancellationToken ct = default);
    Task<Board> GetNextStateAsync(string boardId, CancellationToken ct = default);
    Task<Board> GetStateAheadAsync(string boardId, int n, CancellationToken ct = default);
    Task<Board> GetFinalStateAsync(string boardId, CancellationToken ct = default);
}