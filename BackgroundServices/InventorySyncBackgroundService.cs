using API_PanCake.Data;
using API_PanCake.Data.Entities;
using API_PanCake.Models;
using API_PanCake.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace API_PanCake.BackgroundServices;

/// <summary>
/// Background service để đồng bộ tồn kho từ cdvt13 lên PanCake
/// Có thể enable/disable và config interval qua appsettings.json
/// </summary>
public class InventorySyncBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InventorySyncBackgroundService> _logger;
    private readonly IConfiguration _configuration;
    private readonly TimeSpan _interval;
    private readonly bool _enabled;

    public InventorySyncBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<InventorySyncBackgroundService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
        
        // Đọc config từ appsettings.json
        _enabled = _configuration.GetValue<bool>("InventorySync:Enabled", true);
        var intervalSeconds = _configuration.GetValue<int>("InventorySync:IntervalSeconds", 120); // Default 2 phút
        _interval = TimeSpan.FromSeconds(intervalSeconds);
        
        _logger.LogWarning("🔧 [CONSTRUCTOR] InventorySyncBackgroundService initialized");
        _logger.LogWarning("🔧 [CONFIG] Enabled={Enabled}, Interval={Interval} seconds", _enabled, intervalSeconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogWarning("🚀 [STARTUP] InventorySyncBackgroundService.ExecuteAsync() called");
        _logger.LogWarning("🚀 [STARTUP] Enabled={Enabled}, Interval={Interval} seconds", _enabled, _interval.TotalSeconds);
        
        if (!_enabled)
        {
            _logger.LogWarning("⚠️ [DISABLED] InventorySync is disabled in configuration. Service will not run.");
            _logger.LogWarning("⚠️ [DISABLED] To enable, set 'InventorySync:Enabled' to true in appsettings.json");
            return;
        }

        _logger.LogInformation("🚀 InventorySyncBackgroundService started - Interval: {Interval} seconds", _interval.TotalSeconds);

        // Chờ 5 giây trước khi chạy lần đầu (để app startup xong)
        _logger.LogInformation("⏳ Waiting 5 seconds before first run...");
        try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
        catch (TaskCanceledException) { _logger.LogInformation("🛑 InventorySync startup cancelled"); return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("⏰ [InventorySync] Starting periodic check at {Time}", DateTime.Now);
                await ProcessInventorySyncQueueAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in InventorySyncBackgroundService");
            }

            // Đợi trước khi chạy lại
            _logger.LogInformation("⏸️ [InventorySync] Waiting {Seconds} seconds until next check...", _interval.TotalSeconds);
            try { await Task.Delay(_interval, stoppingToken); }
            catch (TaskCanceledException) { break; }
        }

        _logger.LogInformation("🛑 InventorySyncBackgroundService stopped");
    }

    private async Task ProcessInventorySyncQueueAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var inventoryService = scope.ServiceProvider.GetRequiredService<IPanCakeInventoryService>();

        try
        {
            _logger.LogInformation("=== BẮT ĐẦU XỬ LÝ ĐỒNG BỘ TỒN KHO ===");

            // Lấy danh sách pending items (tối đa 500 items mỗi lần)
            // Chỉ lấy Pending, không lấy Failed (đã bỏ qua)
            var pendingItems = await context.InventorySyncQueue
                .Where(q => q.Status == (byte)InventorySyncStatus.Pending && q.RetryCount < 3)
                .OrderBy(q => q.CreatedAt)
                .Take(500)
                .ToListAsync(cancellationToken);

            // Thống kê số lượng Failed items (để theo dõi)
            var failedCount = await context.InventorySyncQueue
                .CountAsync(q => q.Status == (byte)InventorySyncStatus.Failed, cancellationToken);

            if (failedCount > 0)
            {
                _logger.LogInformation("ℹ️ Có {FailedCount} mã vật tư đã Failed (đã bỏ qua, không xử lý lại)", failedCount);
            }

            if (pendingItems.Count == 0)
            {
                _logger.LogInformation("✅ Không có mã vật tư nào cần đồng bộ");
                return;
            }

            _logger.LogInformation("📦 Tìm thấy {Count} mã vật tư cần xử lý (tối đa 500/lần)", pendingItems.Count);

            foreach (var item in pendingItems)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                await ProcessSingleItemAsync(context, inventoryService, item, cancellationToken);
            }

            await context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("=== KẾT THÚC XỬ LÝ ĐỒNG BỘ TỒN KHO ===");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Lỗi khi xử lý queue");
        }
    }

    private async Task ProcessSingleItemAsync(
        AppDbContext context,
        IPanCakeInventoryService inventoryService,
        InventorySyncQueueEntity item,
        CancellationToken cancellationToken)
    {
        try
        {
            // Trim khoảng trắng thừa từ mã vật tư
            item.MaVt = item.MaVt?.Trim() ?? string.Empty;
            
            _logger.LogInformation("🔄 Đang xử lý MaVt='{MaVt}', ShopId={ShopId}", item.MaVt, item.ShopId);

            // Đánh dấu đang xử lý
            item.Status = (byte)InventorySyncStatus.Processing;
            item.RetryCount++;

            // Lấy tổng tồn kho từ cdvt13
            var totalQuantity = await GetTotalInventoryFromCdvt13Async(context, item.MaVt, cancellationToken);
            item.TotalQuantity = totalQuantity;

            _logger.LogInformation("📊 Tổng tồn kho cho MaVt={MaVt}: {Quantity}", item.MaVt, totalQuantity);

            // Lấy API Key từ dmkb_api
            var apiKey = await context.DmkbApis
                .Where(x => x.IdShop == item.ShopId.ToString())
                .Select(x => x.KeyApi)
                .FirstOrDefaultAsync(cancellationToken);

            if (string.IsNullOrEmpty(apiKey))
            {
                throw new Exception($"Không tìm thấy API Key cho Shop ID {item.ShopId}");
            }

            // Gọi API đồng bộ tồn kho
            var syncRequest = new InventorySyncRequest
            {
                ShopId = item.ShopId,
                MaVt = item.MaVt,
                RemainQuantity = (int)Math.Round(totalQuantity) // Làm tròn số thập phân
            };

            var result = await inventoryService.SyncInventoryAsync(syncRequest, apiKey);

            if (result.Success)
            {
                item.Status = (byte)InventorySyncStatus.Success;
                item.ProcessedAt = DateTime.Now;
                item.ErrorMessage = null;
                _logger.LogInformation("✅ Đồng bộ thành công MaVt='{MaVt}', Quantity={Qty}", 
                    item.MaVt, totalQuantity);
            }
            else
            {
                // Không throw exception, chỉ đánh dấu Failed và tiếp tục
                _logger.LogWarning("⚠️ Không đồng bộ được MaVt='{MaVt}': {Message}", item.MaVt, result.Message);
                
                // Nếu sản phẩm không tồn tại trên PanCake, đánh dấu Failed ngay (không retry)
                if (result.Message.Contains("Không tìm thấy sản phẩm"))
                {
                    item.Status = (byte)InventorySyncStatus.Failed;
                    item.ErrorMessage = $"❌ SP không có trên PanCake: {result.Message}";
                    item.RetryCount = 3; // Set max để không retry nữa
                    _logger.LogInformation("⏭️ Bỏ qua MaVt='{MaVt}' (không có trên PanCake)", item.MaVt);
                }
                else
                {
                    // Lỗi khác thì retry
                    if (item.RetryCount >= 3)
                    {
                        item.Status = (byte)InventorySyncStatus.Failed;
                        item.ErrorMessage = $"Failed after 3 retries: {result.Message}";
                    }
                    else
                    {
                        item.Status = (byte)InventorySyncStatus.Pending;
                        item.ErrorMessage = result.Message;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Exception khi xử lý MaVt='{MaVt}'", item.MaVt);
            
            // Đánh dấu Failed hoặc Pending để retry
            if (item.RetryCount >= 3)
            {
                item.Status = (byte)InventorySyncStatus.Failed;
                item.ErrorMessage = $"Exception after {item.RetryCount} retries: {ex.Message}";
                _logger.LogError("❌ MaVt='{MaVt}' đã hết số lần retry", item.MaVt);
            }
            else
            {
                item.Status = (byte)InventorySyncStatus.Pending; // Retry lại
                item.ErrorMessage = $"Exception (retry {item.RetryCount}/3): {ex.Message}";
                _logger.LogInformation("🔄 MaVt='{MaVt}' sẽ retry lại (lần {Count}/3)", item.MaVt, item.RetryCount);
            }
        }
    }

    /// <summary>
    /// Lấy tổng tồn kho từ bảng cdvt13 theo ma_vt
    /// </summary>
    private async Task<decimal> GetTotalInventoryFromCdvt13Async(
        AppDbContext context,
        string maVt,
        CancellationToken cancellationToken)
    {
        try
        {
            // Gọi stored procedure SP_GetTotalInventoryByMaVt
            var maVtParam = new SqlParameter("@MaVt", maVt);

            // Sử dụng SqlQuery với FromSqlRaw
            var connection = context.Database.GetDbConnection();
            await using var command = connection.CreateCommand();
            command.CommandText = "EXEC SP_GetTotalInventoryByMaVt @MaVt";
            command.Parameters.Add(maVtParam);
            
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync(cancellationToken);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            
            if (await reader.ReadAsync(cancellationToken))
            {
                var totalQuantity = reader.GetDecimal(reader.GetOrdinal("TotalQuantity"));
                return totalQuantity;
            }

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Lỗi khi lấy tổng tồn kho cho MaVt={MaVt}", maVt);
            return 0;
        }
    }
}
