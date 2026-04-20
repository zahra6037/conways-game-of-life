using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GameOfLife.Application.Ports;
using GameOfLife.Domain.Entities;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace GameOfLife.API.Tests;

public class GameApiTests : IClassFixture<TestApiFactory>
{
    private readonly HttpClient _client;

    public GameApiTests(TestApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Upload_matches_documented_contract()
    {
        var resp = await _client.PostAsJsonAsync("/api/boards", new { grid = new[] { new[] { 1, 1 }, new[] { 1, 1 } } });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var payload = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.StartsWith("board_", payload.GetProperty("boardId").GetString());
        Assert.Equal(0, payload.GetProperty("generation").GetInt32());
        Assert.Equal("[[1,1],[1,1]]", payload.GetProperty("initialState").GetRawText());
    }

    [Fact]
    public async Task Full_lifecycle_next_states_and_final_for_a_blinker()
    {
        var blinker = new[]
        {
            new[] { 0, 0, 0, 0, 0 },
            new[] { 0, 0, 1, 0, 0 },
            new[] { 0, 0, 1, 0, 0 },
            new[] { 0, 0, 1, 0, 0 },
            new[] { 0, 0, 0, 0, 0 },
        };

        var create = await _client.PostAsJsonAsync("/api/boards", new { grid = blinker });
        var boardId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("boardId").GetString();

        var next = await _client.GetFromJsonAsync<JsonElement>($"/api/boards/{boardId}/next");
        Assert.Equal(1, next.GetProperty("generation").GetInt32());

        var ahead = await _client.GetFromJsonAsync<JsonElement>($"/api/boards/{boardId}/states/4");
        Assert.Equal(4, ahead.GetProperty("generation").GetInt32());
        // Period 2 → state at gen 4 equals the original.
        Assert.Equal(JsonSerializer.Serialize(blinker), ahead.GetProperty("state").GetRawText());

        var final = await _client.GetFromJsonAsync<JsonElement>($"/api/boards/{boardId}/final");
        Assert.Equal(2, final.GetProperty("generation").GetInt32());
    }

    [Theory]
    [InlineData("{\"grid\":[[1,2]]}", "0 or 1")]
    [InlineData("{\"grid\":[[1,0],[1]]}", "rectangular")]
    [InlineData("{\"grid\":[]}", "non-empty")]
    [InlineData("{\"grid\":null}", "required")]
    public async Task Upload_rejects_invalid_grids_with_400(string body, string expectedDetailFragment)
    {
        var resp = await _client.PostAsync("/api/boards",
            new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var problem = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(expectedDetailFragment, problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Unknown_board_returns_404_problem_details()
    {
        var resp = await _client.GetAsync("/api/boards/board_doesnotexist_0/next");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        var problem = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Board Not Found", problem.GetProperty("title").GetString());
        Assert.Equal("board_doesnotexist_0", problem.GetProperty("boardId").GetString());
    }

    [Fact]
    public async Task Final_returns_iteration_count_when_board_stabilizes()
    {
        // A glider on a finite 6x6 grid walks off the edge and settles to all-dead
        // (cells beyond the boundary are treated as permanently dead). The /final
        // endpoint should report the generation at which stabilization happened.
        var glider = new[]
        {
            new[] { 0, 0, 0, 0, 0, 0 },
            new[] { 0, 0, 1, 0, 0, 0 },
            new[] { 0, 0, 0, 1, 0, 0 },
            new[] { 0, 1, 1, 1, 0, 0 },
            new[] { 0, 0, 0, 0, 0, 0 },
            new[] { 0, 0, 0, 0, 0, 0 },
        };

        var create = await _client.PostAsJsonAsync("/api/boards", new { grid = glider });
        var boardId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("boardId").GetString();

        var resp = await _client.GetAsync($"/api/boards/{boardId}/final");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var generation = body.GetProperty("generation").GetInt32();
        Assert.True(generation > 0, "stabilized board should report the iteration count > 0");
    }

    [Fact]
    public async Task Negative_n_is_rejected_with_400()
    {
        var create = await _client.PostAsJsonAsync("/api/boards", new { grid = new[] { new[] { 1, 1 }, new[] { 1, 1 } } });
        var boardId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("boardId").GetString();

        var resp = await _client.GetAsync($"/api/boards/{boardId}/states/-1");
        // The route constraint already coerces, but our `int n` allows negatives via fallback.
        // If the router 404s due to the constraint, that's also a fine "rejected" outcome.
        Assert.True(resp.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound);
    }
}

public sealed class TestApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace the SQLite-backed repository with an in-memory one for hermetic tests.
            var existing = services.SingleOrDefault(d => d.ServiceType == typeof(IBoardRepository));
            if (existing is not null) services.Remove(existing);
            services.AddSingleton<IBoardRepository, InMemoryBoardRepository>();
        });
    }
}

internal sealed class InMemoryBoardRepository : IBoardRepository
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
