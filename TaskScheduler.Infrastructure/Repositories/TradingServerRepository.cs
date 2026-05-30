using Microsoft.EntityFrameworkCore;
using TaskScheduler.Core.Entities;
using TaskScheduler.Core.Interfaces;
using TaskScheduler.Infrastructure.Data;

namespace TaskScheduler.Infrastructure.Repositories;

public class TradingServerRepository : ITradingServerRepository
{
    private readonly AppDbContext _context;

    public TradingServerRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<TradingServer>> GetAllEnabledAsync()
    {
        return await _context.TradingServers
            .Where(s => s.IsEnabled)
            .ToListAsync();
    }

    public async Task<TradingServer> GetByIdAsync(string id)
    {
        return await _context.TradingServers.FindAsync(id);
    }

    public async Task<List<TradingServer>> GetAllAsync()
    {
        return await _context.TradingServers.ToListAsync();
    }

    public async Task<TradingServer> CreateAsync(TradingServer server)
    {
        _context.TradingServers.Add(server);
        await _context.SaveChangesAsync();
        return server;
    }

    public async Task<TradingServer> UpdateAsync(TradingServer server)
    {
        await _context.SaveChangesAsync();
        return server;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var server = await _context.TradingServers.FindAsync(id);
        if (server == null) return false;

        _context.TradingServers.Remove(server);
        await _context.SaveChangesAsync();
        return true;
    }
}
