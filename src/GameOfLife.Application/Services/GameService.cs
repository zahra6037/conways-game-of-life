using GameOfLife.Application.Ports;
using GameOfLife.Application.Telemetry;
using GameOfLife.Domain.Entities;
using GameOfLife.Domain.Exceptions;
using GameOfLife.Domain.Services;
using Microsoft.Extensions.Logging;

namespace GameOfLife.Application.Services;

public sealed class GameService : IGameService
{
    /// <summary>
    /// Hard server-side ceiling on the number of generations the
    /// <see cref="GetFinalStateAsync"/> search will run before giving up.
    /// Conway boards can run forever (e.g. glider guns), so a cap is required;
    /// it is intentionally not user-tunable so callers get a definitive answer.
    /// </summary>
    public const int MaxFinalStateIterations = 100_000;

    private readonly IBoardRepository _boardRepository;
    private readonly GameRulesService _gameRulesService;
    private readonly ILogger<GameService> _logger;

    public GameService(
        IBoardRepository boardRepository,
        GameRulesService gameRulesService,
        ILogger<GameService> logger)
    {
        _boardRepository = boardRepository;
        _gameRulesService = gameRulesService;
        _logger = logger;
    }

    public async Task<(string BoardId, Board Board)> CreateBoardAsync(
        int rows, int cols, bool[] initialCells, CancellationToken ct = default)
    {
        // Starts a trace named "CreateBoard"
        using var activity = GameTelemetry.ActivitySource.StartActivity("CreateBoard");
        activity?.SetTag("board.rows", rows); //Adds metadata to the trace
        activity?.SetTag("board.cols", cols);

        var board = new Board(rows, cols, initialCells, generation: 0);
        var id = BoardId.New();
        await _boardRepository.SaveAsync(id, board, ct);

        activity?.SetTag("board.id", id);
        _logger.LogInformation("Created board {BoardId} ({Rows}x{Cols})", id, rows, cols);
        return (id, board);
    }

    public async Task<Board> GetInitialStateAsync(string boardId, CancellationToken ct = default)
        => await LoadAsync(boardId, ct);

    public async Task<Board> GetNextStateAsync(string boardId, CancellationToken ct = default)
    {
        using var activity = GameTelemetry.ActivitySource.StartActivity("GetNextState");
        activity?.SetTag("board.id", boardId);

        var board = await LoadAsync(boardId, ct);
        return _gameRulesService.NextGeneration(board);
    }

    public async Task<Board> GetStateAheadAsync(string boardId, int n, CancellationToken ct = default)
    {
        if (n < 0) throw new ArgumentException("n must be non-negative", nameof(n));

        using var activity = GameTelemetry.ActivitySource.StartActivity("GetStateAhead");
        activity?.SetTag("board.id", boardId);
        activity?.SetTag("board.n", n);

        var board = await LoadAsync(boardId, ct);
        return _gameRulesService.Advance(board, n);
    }

    public async Task<Board> GetFinalStateAsync(string boardId, CancellationToken ct = default)
    {
        using var activity = GameTelemetry.ActivitySource.StartActivity("GetFinalState");
        activity?.SetTag("board.id", boardId);
        activity?.SetTag("board.maxIterations", MaxFinalStateIterations);

        var board = await LoadAsync(boardId, ct);
        try
        {
            var final = _gameRulesService.FindFinalState(boardId, board, MaxFinalStateIterations);
            activity?.SetTag("board.finalGeneration", final.Generation);
            _logger.LogInformation(
                "Board {BoardId} stabilized at generation {Generation} (limit {MaxIterations})",
                boardId, final.Generation, MaxFinalStateIterations);
            return final;
        }
        catch (BoardDidNotStabilizeException ex)
        {
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.Message);
            _logger.LogWarning(
                "Board {BoardId} did not stabilize within {MaxIterations} iterations",
                boardId, MaxFinalStateIterations);
            throw;
        }
    }

    private async Task<Board> LoadAsync(string boardId, CancellationToken ct)
    {
        if (!BoardId.IsValid(boardId))
        {
            _logger.LogInformation("Rejected request for malformed board id {BoardId}", boardId);
            throw new BoardNotFoundException(boardId);
        }

        var board = await _boardRepository.GetAsync(boardId, ct);
        if (board is null)
        {
            _logger.LogInformation("Board {BoardId} not found", boardId);
            throw new BoardNotFoundException(boardId);
        }
        return board;
    }
}
