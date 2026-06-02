using TaskScheduler.Core.DTOs;
using TaskScheduler.Core.Entities;
using TaskScheduler.Core.Interfaces;

namespace TaskScheduler.Core.Services;

public class TradingServerService : ITradingServerService
{
    private readonly ITradingServerRepository _serverRepo;

    public TradingServerService(ITradingServerRepository repository)
    {
        _serverRepo = repository;
    }

    public async Task<List<TradingServerResponse>> GetAllAsync()
    {
        var servers = await _serverRepo.GetAllAsync();
        return servers.Select(MapToResponse).ToList();
    }

    public async Task<TradingServerResponse> CreateAsync(string name)
    {
        var server = new TradingServer
        {
            Name = name,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        };
        await _serverRepo.CreateAsync(server);
        return MapToResponse(server);
    }

    public async Task<TradingServerResponse?> SetEnabledAsync(string id, bool isEnabled)
    {
        var server = await _serverRepo.GetByIdAsync(id);
        if (server == null) return null;

        server.IsEnabled = isEnabled;
        await _serverRepo.UpdateAsync(server);
        return MapToResponse(server);
    }

    public async Task<bool> DeleteAsync(string id)
    {
        return await _serverRepo.DeleteAsync(id);
    }

    private static TradingServerResponse MapToResponse(TradingServer server)
    {
        return new TradingServerResponse
        {
            Id = server.Id,
            Name = server.Name,
            IsEnabled = server.IsEnabled,
            CreatedAt = server.CreatedAt
        };
    }
}
