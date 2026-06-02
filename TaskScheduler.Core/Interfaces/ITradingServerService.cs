using TaskScheduler.Core.DTOs;

namespace TaskScheduler.Core.Interfaces;

public interface ITradingServerService
{
    Task<List<TradingServerResponse>> GetAllAsync();
    Task<TradingServerResponse> CreateAsync(string name);
    Task<TradingServerResponse?> SetEnabledAsync(string id, bool isEnabled);
    Task<bool> DeleteAsync(string id);
}
