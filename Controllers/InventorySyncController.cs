using API_PanCake.Data;
using API_PanCake.Models;
using API_PanCake.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace API_PanCake.Controllers
{
    /// <summary>
    /// Controller để đồng bộ tồn kho từ ERP lên PanCake
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class InventorySyncController : ControllerBase
    {
        private readonly IPanCakeInventoryService _inventoryService;
        private readonly AppDbContext _context;
        private readonly ILogger<InventorySyncController> _logger;
        private readonly IConfiguration _configuration;

        public InventorySyncController(
            IPanCakeInventoryService inventoryService,
            AppDbContext context,
            ILogger<InventorySyncController> logger,
            IConfiguration configuration)
        {
            _inventoryService = inventoryService;
            _context = context;
            _logger = logger;
            _configuration = configuration;
        }

        /// <summary>
        /// 🔍 Kiểm tra trạng thái Background Service
        /// </summary>
        [HttpGet("background-service-status")]
        public IActionResult GetBackgroundServiceStatus()
        {
            var enabled = _configuration.GetValue<bool>("InventorySync:Enabled", true);
            var intervalSeconds = _configuration.GetValue<int>("InventorySync:IntervalSeconds", 120);
            
            return Ok(new
            {
                success = true,
                backgroundService = new
                {
                    enabled = enabled,
                    intervalSeconds = intervalSeconds,
                    environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
                    message = enabled 
                        ? $"Background Service is ENABLED and runs every {intervalSeconds} seconds" 
                        : "Background Service is DISABLED in configuration"
                },
                checkInstructions = new
                {
                    howToEnable = "Set 'InventorySync:Enabled' to true in appsettings.json",
                    howToChangeInterval = "Set 'InventorySync:IntervalSeconds' in appsettings.json",
                    logsToCheck = "Look for '🚀 [STARTUP]' and '⏰ [InventorySync]' in application logs"
                }
            });
        }

        /// <summary>
        /// Đồng bộ tồn kho từ ERP lên PanCake
        /// Warehouse IDs sẽ được tự động lấy từ thông tin sản phẩm trên PanCake
        /// </summary>
        /// <param name="request">Thông tin đồng bộ (ShopId, MaVt, RemainQuantity)</param>
        /// <returns>Kết quả đồng bộ</returns>
        /// <remarks>
        /// Sample request:
        /// 
        ///     POST /api/InventorySync/sync
        ///     {
        ///         "shopId": 1635971586,
        ///         "maVt": "TEST323",
        ///         "remainQuantity": 50
        ///     }
        ///     
        /// API sẽ:
        /// 1. Tìm product variation trên PanCake theo ma_vt (search API)
        /// 2. Tự động lấy tất cả warehouse_id từ thông tin sản phẩm
        /// 3. Cập nhật số lượng tồn cho tất cả các kho với giá trị remainQuantity
        /// </remarks>
        [HttpPost("sync")]
        [ProducesResponseType(typeof(InventorySyncResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> SyncInventory([FromBody] InventorySyncRequest request)
        {
            try
            {
                _logger.LogInformation("=== BẮT ĐẦU ĐỒNG BỘ TỒN KHO ===");
                _logger.LogInformation("Shop ID: {ShopId}, Mã Vật Tư: {MaVt}, Số Lượng: {Quantity}", 
                    request.ShopId, request.MaVt, request.RemainQuantity);

                // Lấy API Key từ database theo ShopId
                var apiToken = await _context.DmkbApis
                    .Where(x => x.IdShop == request.ShopId.ToString())
                    .Select(x => x.KeyApi)
                    .FirstOrDefaultAsync();

                if (string.IsNullOrEmpty(apiToken))
                {
                    _logger.LogWarning("Không tìm thấy API Key cho Shop ID: {ShopId}", request.ShopId);
                    return BadRequest(new
                    {
                        success = false,
                        message = $"Không tìm thấy API Key cho Shop ID {request.ShopId}. Vui lòng cấu hình trong bảng dmkb_api."
                    });
                }

                _logger.LogInformation("✅ Đã tìm thấy API Key cho Shop {ShopId}", request.ShopId);

                // Gọi service để đồng bộ tồn kho
                var result = await _inventoryService.SyncInventoryAsync(request, apiToken);

                if (result.Success)
                {
                    _logger.LogInformation("✅ Đồng bộ tồn kho thành công - MaVt: {MaVt}, Total Qty: {Qty}", 
                        result.MaVt, result.TotalQuantityUpdated);

                    return Ok(result);
                }
                else
                {
                    _logger.LogError("❌ Đồng bộ tồn kho thất bại: {Message}", result.Message);
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi đồng bộ tồn kho");
                return StatusCode(500, new InventorySyncResult
                {
                    Success = false,
                    Message = $"Lỗi hệ thống: {ex.Message}",
                    MaVt = request?.MaVt
                });
            }
        }

        /// <summary>
        /// Tìm kiếm thông tin product variation theo mã vật tư
        /// </summary>
        /// <param name="shopId">Shop ID trên PanCake</param>
        /// <param name="maVt">Mã vật tư</param>
        /// <returns>Thông tin product variation</returns>
        [HttpGet("search-product")]
        [ProducesResponseType(typeof(ProductVariationInfo), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> SearchProduct(
            [FromQuery][Required] long shopId,
            [FromQuery][Required] string maVt)
        {
            try
            {
                _logger.LogInformation("Searching product for ShopId={ShopId}, MaVt={MaVt}", shopId, maVt);

                // Lấy API Key từ database
                var apiToken = await _context.DmkbApis
                    .Where(x => x.IdShop == shopId.ToString())
                    .Select(x => x.KeyApi)
                    .FirstOrDefaultAsync();

                if (string.IsNullOrEmpty(apiToken))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = $"Không tìm thấy API Key cho Shop ID {shopId}"
                    });
                }

                var productVariation = await _inventoryService.SearchProductVariationByMaVtAsync(
                    shopId, maVt, apiToken);

                if (productVariation != null)
                {
                    var productName = productVariation.Product?.Name ?? productVariation.DisplayId ?? "Unknown";
                    _logger.LogInformation("✅ Tìm thấy product: {Name}, VariationId={VariationId}", 
                        productName, productVariation.Id);

                    return Ok(new
                    {
                        success = true,
                        data = productVariation
                    });
                }
                else
                {
                    _logger.LogWarning("Không tìm thấy product với MaVt={MaVt}", maVt);
                    return NotFound(new
                    {
                        success = false,
                        message = $"Không tìm thấy sản phẩm với mã vật tư '{maVt}'"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tìm kiếm product");
                return StatusCode(500, new
                {
                    success = false,
                    message = $"Lỗi hệ thống: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// 🧪 TEST ENDPOINT: Xem danh sách queue và trigger xử lý manual
        /// </summary>
        /// <param name="process">Nếu true, sẽ trigger xử lý ngay (default: false)</param>
        /// <returns>Danh sách items trong queue</returns>
        [HttpGet("queue")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetQueueStatus([FromQuery] bool process = false)
        {
            try
            {
                _logger.LogInformation("📋 Checking InventorySyncQueue status, process={Process}", process);

                // Lấy danh sách pending items
                var pendingItems = await _context.InventorySyncQueue
                    .Where(q => q.Status == (byte)InventorySyncStatus.Pending)
                    .OrderBy(q => q.CreatedAt)
                    .Take(50)
                    .ToListAsync();

                var allItems = await _context.InventorySyncQueue
                    .OrderByDescending(q => q.CreatedAt)
                    .Take(100)
                    .ToListAsync();

                var result = new
                {
                    success = true,
                    pendingCount = pendingItems.Count,
                    totalItems = allItems.Count,
                    processTriggered = process,
                    items = allItems.Select(q => new
                    {
                        q.Id,
                        q.MaVt,
                        q.ShopId,
                        Status = ((InventorySyncStatus)q.Status).ToString(),
                        q.RetryCount,
                        q.TotalQuantity,
                        q.CreatedAt,
                        q.ProcessedAt,
                        q.ErrorMessage
                    }).ToList()
                };

                // Nếu process = true, trigger xử lý ngay
                if (process && pendingItems.Count > 0)
                {
                    _logger.LogInformation("🚀 Manual trigger: Processing {Count} pending items", pendingItems.Count);

                    foreach (var item in pendingItems)
                    {
                        try
                        {
                            _logger.LogInformation("🔄 Processing MaVt={MaVt}", item.MaVt);
                            
                            // Get API key
                            var apiKey = await _context.DmkbApis
                                .Where(x => x.IdShop == item.ShopId.ToString())
                                .Select(x => x.KeyApi)
                                .FirstOrDefaultAsync();

                            if (string.IsNullOrEmpty(apiKey))
                            {
                                _logger.LogWarning("⚠️ No API key for shop {ShopId}", item.ShopId);
                                continue;
                            }

                            // Mark as processing
                            item.Status = (byte)InventorySyncStatus.Processing;
                            await _context.SaveChangesAsync();

                            // Sync inventory (using injected service)
                            var syncRequest = new InventorySyncRequest
                            {
                                ShopId = item.ShopId,
                                MaVt = item.MaVt,
                                RemainQuantity = 100 // Test value
                            };

                            var syncResult = await _inventoryService.SyncInventoryAsync(syncRequest, apiKey);

                            if (syncResult.Success)
                            {
                                item.Status = (byte)InventorySyncStatus.Success;
                                item.ProcessedAt = DateTime.Now;
                                _logger.LogInformation("✅ Success: {MaVt}", item.MaVt);
                            }
                            else
                            {
                                item.Status = (byte)InventorySyncStatus.Failed;
                                item.ErrorMessage = syncResult.Message;
                                _logger.LogError("❌ Failed: {MaVt} - {Message}", item.MaVt, syncResult.Message);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "❌ Error processing {MaVt}", item.MaVt);
                            item.Status = (byte)InventorySyncStatus.Failed;
                            item.ErrorMessage = ex.Message;
                        }
                    }

                    await _context.SaveChangesAsync();
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting queue status");
                return StatusCode(500, new
                {
                    success = false,
                    message = $"Lỗi: {ex.Message}"
                });
            }
        }
    }
}

/// <summary>
/// Enum status (duplicate for reference)
/// </summary>
public enum InventorySyncStatus : byte
{
    Pending = 0,
    Processing = 1,
    Success = 2,
    Failed = 3
}
