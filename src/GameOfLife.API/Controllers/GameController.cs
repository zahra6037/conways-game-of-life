using System.ComponentModel.DataAnnotations;
using GameOfLife.API.Models;
using GameOfLife.Application.Ports;
using Microsoft.AspNetCore.Mvc;

namespace GameOfLife.API.Controllers;

[ApiController]
[Route("api/boards")]
[Produces("application/json")]
public sealed class GameController : ControllerBase
{
    public const int MaxN = 100_000;

    private readonly IGameService _service;

    public GameController(IGameService service) => _service = service;

    /// <summary>Upload a new board state and receive a unique identifier.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(UploadBoardResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UploadBoardResponse>> CreateBoard(
        [FromBody] UploadBoardRequest request,
        CancellationToken ct = default)
    {
        if (request is null)
            throw new ValidationException("Request body is required.");

        var (rows, cols, cells) = request.Validate();
        var (boardId, board) = await _service.CreateBoardAsync(rows, cols, cells, ct);

        var response = new UploadBoardResponse(boardId, GridConverter.ToGrid(board), board.Generation);
        return CreatedAtAction(nameof(GetBoard), new { boardId }, response);
    }

    /// <summary>Get the currently stored (initial) board state.</summary>
    [HttpGet("{boardId}")]
    [ProducesResponseType(typeof(BoardStateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BoardStateResponse>> GetBoard(string boardId, CancellationToken ct = default)
    {
        var board = await _service.GetInitialStateAsync(boardId, ct);
        return BoardStateResponse.From(boardId, board);
    }

    /// <summary>Get the next generation for the given board.</summary>
    [HttpGet("{boardId}/next")]
    [ProducesResponseType(typeof(BoardStateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BoardStateResponse>> GetNextGeneration(string boardId, CancellationToken ct = default)
    {
        var board = await _service.GetNextStateAsync(boardId, ct);
        return BoardStateResponse.From(boardId, board);
    }

    /// <summary>Get the board state N generations ahead.</summary>
    [HttpGet("{boardId}/states/{n:int}")]
    [ProducesResponseType(typeof(BoardStateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BoardStateResponse>> GetStateAhead(
        string boardId, int n, CancellationToken ct = default)
    {
        if (n < 0) throw new ValidationException("n must be non-negative.");
        if (n > MaxN) throw new ValidationException($"n must be <= {MaxN}.");
        var board = await _service.GetStateAheadAsync(boardId, n, ct);
        return BoardStateResponse.From(boardId, board);
    }

    /// <summary>
    /// Get the final stable state of the board. The server runs the simulation
    /// until the board reaches a still life or oscillator and returns that
    /// state. The <c>generation</c> field on the response indicates how many
    /// iterations it took. If the board does not stabilize within the server's
    /// hard ceiling, responds with 422.
    /// </summary>
    [HttpGet("{boardId}/final")]
    [ProducesResponseType(typeof(BoardStateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BoardStateResponse>> GetFinalState(
        string boardId,
        CancellationToken ct = default)
    {
        var board = await _service.GetFinalStateAsync(boardId, ct);
        return BoardStateResponse.From(boardId, board);
    }
}
