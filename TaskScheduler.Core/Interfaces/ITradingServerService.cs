using TaskScheduler.Core.Entities;

namespace TaskScheduler.Core.Interfaces;

public interface ITradingServerService
{
    Task<List<TradingServer>> GetAllAsync();
    Task<TradingServer> CreateAsync(string name);
    Task<TradingServer> SetEnabledAsync(string id, bool isEnabled);
    Task<bool> DeleteAsync(string id);
}