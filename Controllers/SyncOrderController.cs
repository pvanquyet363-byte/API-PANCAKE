using API_PanCake.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace API_PanCake.Controllers
{
    /// <summary>
    /// Controller để đồng bộ đơn hàng từ PanCake về ERP
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class SyncOrderController : ControllerBase
    {
        private readonly IPanCakeOrderService _panCakeOrderService;
        private readonly IOrderSyncService _orderSyncService;
        private readonly ILogger<SyncOrderController> _logger;

        public SyncOrderController(
            IPanCakeOrderService panCakeOrderService,
            IOrderSyncService orderSyncService,
            ILogger<SyncOrderController> logger)
        {
            _panCakeOrderService = panCakeOrderService;
            _orderSyncService = orderSyncService;
            _logger = logger;
        }

        /// <summary>
        /// Đồng bộ 1 đơn hàng từ PanCake về ERP
        /// </summary>
        /// <param name="shopId">Shop ID từ PanCake</param>
        /// <param name="orderId">Order ID từ PanCake</param>
        /// <returns>Kết quả đồng bộ</returns>
        [HttpPost("sync-order")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> SyncOrder([FromQuery] int shopId, [FromQuery] string orderId)
        {
            try
            {
                _logger.LogInformation("=== BẮT ĐẦU ĐỒNG BỘ ĐơN HÀNG ===");
                _logger.LogInformation("Shop ID: {ShopId}, Order ID: {OrderId}", shopId, orderId);

                // Bước 1: Lấy thông tin đơn hàng từ PanCake API
                var orderDetail = await _panCakeOrderService.GetOrderDetailAsync(shopId, orderId);
                
                if (orderDetail == null)
                {
                    _logger.LogWarning("Không lấy được thông tin đơn hàng từ PanCake");
                    return BadRequest(new 
                    { 
                        success = false, 
                        message = "Không lấy được thông tin đơn hàng từ PanCake API" 
                    });
                }

                _logger.LogInformation("✅ Đã lấy đơn hàng từ PanCake: {Items} items", orderDetail.Items?.Count ?? 0);

                // Bước 2: Đồng bộ vào ERP system (m65/d65 tables - Xuất bán)
                var result = await _orderSyncService.SyncOrderToERPAsync(shopId, orderDetail, orderId);

                if (result.Success)
                {
                    _logger.LogInformation("✅ Đồng bộ thành công - Số CT: {SoCt}, STT_REC: {SttRec}", 
                        result.SoCt, result.SttRec);
                    
                    return Ok(new
                    {
                        success = true,
                        message = "Đồng bộ đơn hàng thành công",
                        data = new
                        {
                            stt_rec = result.SttRec,
                            so_ct = result.SoCt,
                            ma_ct = result.MaCt,
                            items_count = orderDetail.Items?.Count ?? 0,
                            total_amount = orderDetail.Total_Price
                        }
                    });
                }
                else
                {
                    _logger.LogError("❌ Đồng bộ thất bại: {Error}", result.ErrorMessage);
                    return StatusCode(500, new
                    {
                        success = false,
                        message = result.ErrorMessage
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi đồng bộ đơn hàng");
                return StatusCode(500, new
                {
                    success = false,
                    message = $"Lỗi hệ thống: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Đồng bộ nhiều đơn hàng theo danh sách
        /// </summary>
        [HttpPost("sync-orders-bulk")]
        public async Task<IActionResult> SyncOrdersBulk([FromBody] List<SyncOrderRequest> orders)
        {
            try
            {
                _logger.LogInformation("=== ĐỒNG BỘ HÀNG LOẠT: {Count} đơn hàng ===", orders.Count);

                var results = new List<object>();
                var successCount = 0;
                var failCount = 0;

                foreach (var order in orders)
                {
                    try
                    {
                        var orderDetail = await _panCakeOrderService.GetOrderDetailAsync(order.ShopId, order.OrderId);
                        
                        if (orderDetail != null)
                        {
                            var syncResult = await _orderSyncService.SyncOrderToERPAsync(order.ShopId, orderDetail, order.OrderId);
                            
                            if (syncResult.Success)
                            {
                                successCount++;
                                results.Add(new { orderId = order.OrderId, success = true, stt_rec = syncResult.SttRec });
                            }
                            else
                            {
                                failCount++;
                                results.Add(new { orderId = order.OrderId, success = false, error = syncResult.ErrorMessage });
                            }
                        }
                        else
                        {
                            failCount++;
                            results.Add(new { orderId = order.OrderId, success = false, error = "Không lấy được từ PanCake API" });
                        }
                    }
                    catch (Exception ex)
                    {
                        failCount++;
                        results.Add(new { orderId = order.OrderId, success = false, error = ex.Message });
                    }
                }

                return Ok(new
                {
                    success = true,
                    message = $"Đã xử lý {orders.Count} đơn hàng: {successCount} thành công, {failCount} thất bại",
                    results
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi đồng bộ hàng loạt");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Test connection đến PanCake API
        /// </summary>
        [HttpGet("test-connection")]
        public async Task<IActionResult> TestConnection([FromQuery] int shopId, [FromQuery] string orderId)
        {
            try
            {
                _logger.LogInformation("Testing PanCake API - Shop: {ShopId}, Order: {OrderId}", shopId, orderId);
                
                var orderDetail = await _panCakeOrderService.GetOrderDetailAsync(shopId, orderId);
                
                if (orderDetail != null)
                {
                    return Ok(new
                    {
                        success = true,
                        message = "✅ Kết nối thành công - Token hợp lệ",
                        order = new
                        {
                            id = orderDetail.Id,
                            order_code = orderDetail.Order_Code,
                            status_name = orderDetail.Status_Name,
                            total_price = orderDetail.Total_Price,
                            items_count = orderDetail.Items?.Count ?? 0
                        }
                    });
                }
                else
                {
                    return BadRequest(new { success = false, message = "❌ Không lấy được dữ liệu - Kiểm tra token trong bảng dmkb_api" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi test connection");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Kiểm tra token trong bảng dmkb_api (DEBUG endpoint)
        /// </summary>
        [HttpGet("check-token")]
        public async Task<IActionResult> CheckToken([FromQuery] int shopId)
        {
            try
            {
                var shopIdStr = shopId.ToString().Trim();
                
                // Inject AppDbContext để query trực tiếp
                using var scope = HttpContext.RequestServices.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<Data.AppDbContext>();
                
                // Lấy tất cả shops để debug
                var allShops = await context.DmkbApis.ToListAsync();
                
                // Tìm shop match
                var matchedShop = await context.DmkbApis
                    .Where(x => x.IdShop.Trim() == shopIdStr && x.Status == "1")
                    .FirstOrDefaultAsync();

                _logger.LogInformation("Check token - Shop ID: {ShopId}, Found: {Found}", shopId, matchedShop != null);

                return Ok(new
                {
                    success = true,
                    search_criteria = new
                    {
                        shop_id = shopIdStr,
                        status_required = "1"
                    },
                    total_shops_in_db = allShops.Count,
                    all_shops = allShops.Select(s => new
                    {
                        id_shop = s.IdShop,
                        ten_shop = s.TenShop,
                        status = s.Status,
                        ma_shop = s.MaShop,
                        ma_kho_dh = s.MaKhoDh,
                        has_token = !string.IsNullOrEmpty(s.KeyApi),
                        token_length = s.KeyApi?.Length ?? 0,
                        token_preview = !string.IsNullOrEmpty(s.KeyApi) ? s.KeyApi.Substring(0, Math.Min(30, s.KeyApi.Length)) + "..." : "❌ NULL"
                    }).ToList(),
                    matched_shop = matchedShop != null ? new
                    {
                        id_shop = matchedShop.IdShop,
                        ten_shop = matchedShop.TenShop,
                        status = matchedShop.Status,
                        ma_shop = matchedShop.MaShop,
                        ma_kho_dh = matchedShop.MaKhoDh,
                        has_token = !string.IsNullOrEmpty(matchedShop.KeyApi),
                        token_length = matchedShop.KeyApi?.Length ?? 0,
                        token_preview = !string.IsNullOrEmpty(matchedShop.KeyApi) ? matchedShop.KeyApi.Substring(0, Math.Min(30, matchedShop.KeyApi.Length)) + "..." : "❌ NULL",
                        full_token = matchedShop.KeyApi ?? "NULL"
                    } : null,
                    message = matchedShop == null 
                        ? $"❌ KHÔNG tìm thấy shop ID '{shopIdStr}' với status='1' trong dmkb_api" 
                        : (string.IsNullOrEmpty(matchedShop.KeyApi) 
                            ? "⚠️ Tìm thấy shop nhưng KeyApi = NULL" 
                            : "✅ Tìm thấy shop và có token"),
                    instruction = matchedShop == null 
                        ? "Vui lòng dùng endpoint POST /api/sync-order/save-token để thêm token" 
                        : "Token sẵn sàng để sử dụng"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi check token");
                return StatusCode(500, new { success = false, message = ex.Message, stack = ex.ToString() });
            }
        }

        /// <summary>
        /// Lưu hoặc cập nhật token cho shop (INSERT/UPDATE dmkb_api)
        /// </summary>
        /// <param name="request">Thông tin shop và token</param>
        /// <returns>Kết quả lưu token</returns>
        [HttpPost("save-token")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SaveToken([FromBody] SaveTokenRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.ShopId) || string.IsNullOrWhiteSpace(request.ApiToken))
                {
                    return BadRequest(new { success = false, message = "ShopId và ApiToken không được để trống" });
                }

                using var scope = HttpContext.RequestServices.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<Data.AppDbContext>();

                var shopIdStr = request.ShopId.Trim();

                // Kiểm tra xem shop đã tồn tại chưa
                var existingShop = await context.DmkbApis
                    .FirstOrDefaultAsync(x => x.IdShop == shopIdStr);

                if (existingShop != null)
                {
                    // UPDATE token cho shop đã tồn tại
                    existingShop.KeyApi = request.ApiToken.Trim();
                    existingShop.Status = "1"; // Active
                    existingShop.Datetime2 = DateTime.Now;
                    existingShop.UserId2 = 1;

                    // Cập nhật thông tin khác nếu có
                    if (!string.IsNullOrWhiteSpace(request.TenShop))
                        existingShop.TenShop = request.TenShop;
                    if (!string.IsNullOrWhiteSpace(request.MaKhoDh))
                        existingShop.MaKhoDh = request.MaKhoDh;
                    if (!string.IsNullOrWhiteSpace(request.MaShop))
                        existingShop.MaShop = request.MaShop;

                    await context.SaveChangesAsync();

                    _logger.LogInformation("✅ Đã UPDATE token cho shop ID: {ShopId}", shopIdStr);

                    return Ok(new
                    {
                        success = true,
                        message = "✅ Đã cập nhật token thành công",
                        action = "UPDATE",
                        shop = new
                        {
                            id_shop = existingShop.IdShop,
                            ten_shop = existingShop.TenShop,
                            status = existingShop.Status,
                            token_length = existingShop.KeyApi?.Length ?? 0
                        }
                    });
                }
                else
                {
                    // INSERT shop mới
                    var newShop = new Data.Entities.DmkbApiEntity
                    {
                        IdShop = shopIdStr,
                        KeyApi = request.ApiToken.Trim(),
                        TenShop = request.TenShop ?? $"Shop {shopIdStr}",
                        MaKhoDh = request.MaKhoDh ?? "KHO01",
                        MaKhoTonKho = request.MaKhoTonKho ?? "KHO01",
                        MaShop = request.MaShop ?? $"SHOP{shopIdStr}",
                        Status = "1",
                        Datetime0 = DateTime.Now,
                        Datetime2 = DateTime.Now,
                        UserId0 = 1,
                        UserId2 = 1
                    };

                    context.DmkbApis.Add(newShop);
                    await context.SaveChangesAsync();

                    _logger.LogInformation("✅ Đã INSERT shop mới ID: {ShopId}", shopIdStr);

                    return Ok(new
                    {
                        success = true,
                        message = "✅ Đã thêm shop và token mới thành công",
                        action = "INSERT",
                        shop = new
                        {
                            id_shop = newShop.IdShop,
                            ten_shop = newShop.TenShop,
                            ma_kho_dh = newShop.MaKhoDh,
                            ma_shop = newShop.MaShop,
                            status = newShop.Status,
                            token_length = newShop.KeyApi?.Length ?? 0
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi save token");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
    }

    public class SyncOrderRequest
    {
        public int ShopId { get; set; }
        public string OrderId { get; set; } = "";
    }

    /// <summary>
    /// Model request để lưu/cập nhật token cho shop
    /// </summary>
    public class SaveTokenRequest
    {
        /// <summary>
        /// Shop ID từ PanCake (bắt buộc)
        /// </summary>
        [Required(ErrorMessage = "ShopId là bắt buộc")]
        public string ShopId { get; set; } = string.Empty;

        /// <summary>
        /// API Token từ PanCake (bắt buộc) - lấy từ Settings > API trên PanCake POS
        /// </summary>
        [Required(ErrorMessage = "ApiToken là bắt buộc")]
        public string ApiToken { get; set; } = string.Empty;

        /// <summary>
        /// Tên shop (tùy chọn)
        /// </summary>
        public string? TenShop { get; set; }

        /// <summary>
        /// Mã kho đơn hàng (tùy chọn) - mặc định: KHO01
        /// </summary>
        public string? MaKhoDh { get; set; }

        /// <summary>
        /// Mã kho tồn kho (tùy chọn) - mặc định: KHO01
        /// </summary>
        public string? MaKhoTonKho { get; set; }

        /// <summary>
        /// Mã shop trong ERP (tùy chọn) - mặc định: SHOP{ShopId}
        /// </summary>
        public string? MaShop { get; set; }
    }
}
