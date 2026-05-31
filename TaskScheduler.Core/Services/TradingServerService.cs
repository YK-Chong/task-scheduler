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

    public async Task<List<TradingServer>> GetAllAsync()
    {
        return await _serverRepo.GetAllAsync();
    }

    public async Task<TradingServer> CreateAsync(string name)
    {
        var server = new TradingServer
        {
            Name = name,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        };
        return await _serverRepo.CreateAsync(server);
    }

    public async Task<TradingServer?> SetEnabledAsync(string id, bool isEnabled)
    {
        var server = await _serverRepo.GetByIdAsync(id);
        if (server == null) return null;

        server.IsEnabled = isEnabled;
        return await _serverRepo.UpdateAsync(server);
    }

    public async Task<bool> DeleteAsync(string id)
    {
        return await _serverRepo.DeleteAsync(id);
    }
}
