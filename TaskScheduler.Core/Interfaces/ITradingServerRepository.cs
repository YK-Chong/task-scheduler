using TaskScheduler.Core.Entities;

namespace TaskScheduler.Core.Interfaces;

public interface ITradingServerRepository
{
    Task<List<TradingServer>> GetAllEnabledAsync();
    Task<TradingServer> GetByIdAsync(string id);
    Task<List<TradingServer>> GetAllAsync();
    Task<TradingServer> CreateAsync(TradingServer server);
    Task<TradingServer> UpdateAsync(TradingServer server);
    Task<bool> DeleteAsync(string id);
}