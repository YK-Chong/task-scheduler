using Microsoft.AspNetCore.Mvc;
using TaskScheduler.Core.DTOs;
using TaskScheduler.Core.Interfaces;

namespace TaskScheduler.API.Controllers;

[ApiController]
[Route("api/trading-servers")]
public class TradingServersController : ControllerBase
{
    private readonly ITradingServerService _serverService;
    private readonly ILogger<TradingServersController> _logger;

    public TradingServersController(ITradingServerService serverService, ILogger<TradingServersController> logger)
    {
        _serverService = serverService;
        _logger = logger;
    }

    /// <summary>
    /// Returns a list of all trading servers.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<TradingServerResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var servers = await _serverService.GetAllAsync();
        return Ok(servers);
    }

    /// <summary>
    /// Creates a new trading server. The server is enabled by default.
    /// A SymbolDataPullJob will be automatically created on the next MasterServerSyncJob trigger.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(TradingServerResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateServerRequest request)
    {
        var server = await _serverService.CreateAsync(request.Name);
        return CreatedAtAction(nameof(GetAll), new { id = server.Id }, server);
    }

    /// <summary>
    /// Enables a trading server. Its SymbolDataPullJob will be created on the next MasterServerSyncJob trigger.
    /// </summary>
    [HttpPut("{id}/enable")]
    [ProducesResponseType(typeof(TradingServerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Enable(string id)
    {
        var server = await _serverService.SetEnabledAsync(id, true);
        if (server == null) return NotFound(new { error = $"Server '{id}' not found." });
        return Ok(server);
    }

    /// <summary>
    /// Disables a trading server. Its SymbolDataPullJob will be removed on the next MasterServerSyncJob trigger.
    /// </summary>
    [HttpPut("{id}/disable")]
    [ProducesResponseType(typeof(TradingServerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Disable(string id)
    {
        var server = await _serverService.SetEnabledAsync(id, false);
        if (server == null) return NotFound(new { error = $"Server '{id}' not found." });
        return Ok(server);
    }

    /// <summary>
    /// Deletes a trading server permanently.
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteServer(string id)
    {
        var deleted = await _serverService.DeleteAsync(id);
        if (!deleted)
            return NotFound(new { error = $"Server '{id}' not found." });

        return Ok(new { message = $"Server '{id}' deleted successfully." });
    }
}
