using API_PanCake.Data;
using API_PanCake.Data.Entities;
using API_PanCake.Models;
using API_PanCake.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace API_PanCake.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WebhookController : ControllerBase
    {
        private readonly ILogger<WebhookController> _logger;
        private readonly IWebhookDbService _webhookDbService;
        private readonly IPanCakeOrderService _panCakeOrderService;
        private readonly IOrderSyncService _orderSyncService;
        private readonly ICustomerSyncService _customerSyncService;
        private readonly AppDbContext _dbContext;

        public WebhookController(
            ILogger<WebhookController> logger, 
            IWebhookDbService webhookDbService,
            IPanCakeOrderService panCakeOrderService,
            IOrderSyncService orderSyncService,
            ICustomerSyncService customerSyncService,
            AppDbContext dbContext)
        {
            _logger = logger;
            _webhookDbService = webhookDbService;
            _panCakeOrderService = panCakeOrderService;
            _orderSyncService = orderSyncService;
            _customerSyncService = customerSyncService;
            _dbContext = dbContext;
        }

        /// <summary>
        /// Endpoint webhook chung để nhận tất cả sự kiện từ PanCake (Order, Customer, v.v.)
        /// </summary>
        /// <param name="rawData">Dữ liệu webhook từ PanCake</param>
        /// <returns></returns>
        /// <remarks>
        /// Endpoint CHUNG này có thể nhận mọi loại webhook từ PanCake:
        /// - Đơn hàng (type: "orders")
        /// - Khách hàng (type: "customers")
        /// - Các loại khác trong tương lai
        /// 
        /// PanCake sẽ tự động gửi field "type" trong webhook để phân biệt loại sự kiện.
        /// 
        /// ĐỂ SỬ DỤNG:
        /// 1. Đăng ký URL này trong PanCake: https://your-domain.com/api/webhook
        /// 2. PanCake sẽ gửi tất cả webhook về URL này
        /// 3. API sẽ tự động phân loại và xử lý dựa trên field "type"
        /// </remarks>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ReceiveWebhook([FromBody] JsonElement rawData)
        {
            try
            {
                var rawJson = rawData.GetRawText();
                _logger.LogInformation("=== NHẬN WEBHOOK TỪ PANCAKE ===");
                _logger.LogInformation("Raw JSON: {RawJson}", rawJson);
                var Sql = @"
            INSERT INTO Webhook_Logs_ver2 
                (WebhookType, ReceivedAt, RawJsonPreview, Note, CreatedAt)
            VALUES 
                (@WebhookType, @ReceivedAt, @RawJsonPreview, @Note, GETDATE())";

                var preview = string.IsNullOrEmpty(rawJson)
                    ? ""
                    : (rawJson.Length > 1000 ? rawJson.Substring(0, 1000) + "..." : rawJson);

                await _dbContext.Database.ExecuteSqlRawAsync(Sql,
                    new Microsoft.Data.SqlClient.SqlParameter("@WebhookType", "Order"),
                    new Microsoft.Data.SqlClient.SqlParameter("@ReceivedAt", DateTime.Now),
                    new Microsoft.Data.SqlClient.SqlParameter("@RawJsonPreview", preview),
                    new Microsoft.Data.SqlClient.SqlParameter("@Note", "ProcessOrderWebhook - ĐÃ VÀO HÀM")
                );

                // Parse để lấy field "type" để phân biệt loại webhook
                string? webhookType = null;
                try
                {
                    if (rawData.TryGetProperty("type", out var typeElement))
                    {
                        webhookType = typeElement.GetString();
                        _logger.LogInformation("🔍 Webhook Type: {Type}", webhookType);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Không thể lấy field 'type' từ webhook");
                }

                // ROUTE dựa trên type
                if (webhookType == "customers")
                {
                    _logger.LogInformation("➡️ Routing to CUSTOMER webhook handler");
                    return await ProcessCustomerWebhook(rawData, rawJson);
                }
                else
                {
                    // Mặc định xử lý như ORDER webhook (backward compatible)
                    _logger.LogInformation("➡️ Routing to ORDER webhook handler (type: {Type})", webhookType ?? "unknown");
                    return await ProcessOrderWebhook(rawData, rawJson);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Lỗi khi xử lý webhook từ PanCake");
                
                // Vẫn trả về 200 OK để PanCake không retry
                return Ok(new
                {
                    success = false,
                    message = "Webhook received but error occurred: " + ex.Message
                });
            }
        }

        /// <summary>
        /// Nhận webhook từ PanCake khi có đơn hàng mới hoặc thay đổi
        /// </summary>
        /// <param name="rawData">Dữ liệu webhook từ PanCake</param>
        /// <returns></returns>
        /// <remarks>
        /// Endpoint này sẽ nhận các sự kiện từ PanCake như:
        /// - order.created: Đơn hàng mới được tạo
        /// - order.updated: Đơn hàng được cập nhật
        /// - order.confirmed: Đơn hàng được xác nhận
        /// - order.cancelled: Đơn hàng bị hủy
        /// - order.completed: Đơn hàng hoàn thành
        /// 
        /// Để sử dụng:
        /// 1. Đăng ký URL webhook này trong PanCake: https://your-domain.com/api/webhook/order
        /// 2. PanCake sẽ tự động gửi POST request đến endpoint này khi có sự kiện
        /// 
        /// Ví dụ webhook data từ PanCake:
        /// 
        ///     POST /api/webhook/order
        ///     {
        ///         "event": "order.created",
        ///         "timestamp": 1710000000,
        ///         "data": {
        ///             "id": 123456,
        ///             "code": "ORDER-000123",
        ///             "shopId": 789,
        ///             "status": "pending",
        ///             "totalAmount": 500000,
        ///             "discountAmount": 50000,
        ///             "finalAmount": 450000,
        ///             "paymentMethod": "cash",
        ///             "paymentStatus": "unpaid",
        ///             "customer": {
        ///                 "id": 111,
        ///                 "name": "Nguyễn Văn A",
        ///                 "phone": "0901234567",
        ///                 "email": "customer@example.com"
        ///             },
        ///             "items": [
        ///                 {
        ///                     "productId": 222,
        ///                     "productName": "Áo sơ mi",
        ///                     "variationId": 333,
        ///                     "variationName": "Màu Đen - Size M",
        ///                     "quantity": 2,
        ///                     "price": 250000,
        ///                     "totalPrice": 500000
        ///                 }
        ///             ],
        ///             "shippingAddress": {
        ///                 "receiverName": "Nguyễn Văn A",
        ///                 "receiverPhone": "0901234567",
        ///                 "fullAddress": "123 Đường ABC, Phường XYZ",
        ///                 "province": "TP. Hồ Chí Minh",
        ///                 "district": "Quận 1",
        ///                 "ward": "Phường Bến Nghé"
        ///             },
        ///             "note": "Giao hàng buổi chiều",
        ///             "createdAt": "2024-03-10T10:30:00Z",
        ///             "updatedAt": "2024-03-10T10:30:00Z"
        ///         }
        ///     }
        /// </remarks>
        [HttpPost("order")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ReceiveOrderWebhook([FromBody] JsonElement rawData)
        {
            _logger.LogInformation("🔗 Endpoint /api/webhook/order được gọi - redirect to ProcessOrderWebhook");
            var rawJson = rawData.GetRawText();
            return await ProcessOrderWebhook(rawData, rawJson);
        }

        /// <summary>
        /// Đồng bộ 1 đơn hàng từ PanCake về ERP (manual sync)
        /// </summary>
        /// <param name="shopId">Shop ID từ PanCake</param>
        /// <param name="orderId">Order ID từ PanCake</param>
        /// <returns>Kết quả đồng bộ</returns>
        [HttpPost("sync-order")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ManualSyncOrder([FromQuery] int shopId, [FromQuery] string orderId)
        {
            try
            {
                _logger.LogInformation("=== BẮT ĐẦU ĐỒNG BỘ ĐƠN HÀNG (MANUAL) ===");
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

                _logger.LogInformation("Đã lấy đơn hàng từ PanCake: {Items} items", orderDetail.Items?.Count ?? 0);

                // Bước 2: Gọi trực tiếp SyncOrderToERP
                _logger.LogWarning(">>> DEBUG: Trước khi gọi SyncOrderToERPAsync...");
                await SyncOrderToERPAsync(shopId, orderId, orderDetail, 0);
                _logger.LogWarning(">>> DEBUG: Sau khi gọi SyncOrderToERPAsync (chưa có lỗi)...");

                // Kiểm tra xem đơn đã sync chưa
                var mapping = await _dbContext.PanCakeOrderMappings
                    .FirstOrDefaultAsync(m => m.PanCakeOrderId == orderId && m.ShopId == shopId);

                if (mapping != null)
                {
                    _logger.LogInformation("Đồng bộ thành công - Số CT: {SoCt}, STT_REC: {SttRec}", 
                        mapping.SoCt, mapping.SttRec);
                    
                    return Ok(new
                    {
                        success = true,
                        message = "Đồng bộ đơn hàng thành công",
                        data = new
                        {
                            stt_rec = mapping.SttRec,
                            so_ct = mapping.SoCt,
                            ma_ct = mapping.MaCt,
                            items_count = orderDetail.Items?.Count ?? 0,
                            total_amount = orderDetail.Total_Price
                        }
                    });
                }
                else
                {
                    _logger.LogError("Đồng bộ thất bại: không tìm thấy mapping sau khi sync");
                    return StatusCode(500, new
                    {
                        success = false,
                        message = "Đồng bộ có thể thất bại - không tìm thấy mapping"
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
                        message = "Kết nối thành công - Token hợp lệ",
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
                    return BadRequest(new { success = false, message = "Không lấy được dữ liệu - Kiểm tra token trong bảng dmkb_api" });
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
                
                var allShops = await _dbContext.DmkbApis.ToListAsync();
                
                var matchedShop = await _dbContext.DmkbApis
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
                        token_length = s.KeyApi?.Length ?? 0
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
                        token_preview = !string.IsNullOrEmpty(matchedShop.KeyApi) ? matchedShop.KeyApi.Substring(0, Math.Min(30, matchedShop.KeyApi.Length)) + "..." : "NULL",
                        full_token = matchedShop.KeyApi ?? "NULL"
                    } : null
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi check token");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Lưu hoặc cập nhật token cho shop
        /// </summary>
        [HttpPost("save-token")]
        public async Task<IActionResult> SaveToken([FromBody] WebhookController.SaveTokenWebhookRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.ShopId) || string.IsNullOrWhiteSpace(request.ApiToken))
                {
                    return BadRequest(new { success = false, message = "ShopId và ApiToken không được để trống" });
                }

                var shopIdStr = request.ShopId.Trim();
                var existingShop = await _dbContext.DmkbApis
                    .FirstOrDefaultAsync(x => x.IdShop == shopIdStr);

                if (existingShop != null)
                {
                    existingShop.KeyApi = request.ApiToken.Trim();
                    existingShop.Status = "1";
                    existingShop.Datetime2 = DateTime.Now;
                    existingShop.UserId2 = 1;
                    if (!string.IsNullOrWhiteSpace(request.TenShop)) existingShop.TenShop = request.TenShop;
                    if (!string.IsNullOrWhiteSpace(request.MaKhoDh)) existingShop.MaKhoDh = request.MaKhoDh;
                    if (!string.IsNullOrWhiteSpace(request.MaShop)) existingShop.MaShop = request.MaShop;
                    await _dbContext.SaveChangesAsync();
                    _logger.LogInformation("Đã UPDATE token cho shop: {ShopId}", shopIdStr);
                    return Ok(new { success = true, message = "Đã cập nhật token", action = "UPDATE" });
                }
                else
                {
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
                    _dbContext.DmkbApis.Add(newShop);
                    await _dbContext.SaveChangesAsync();
                    _logger.LogInformation("Đã INSERT shop mới: {ShopId}", shopIdStr);
                    return Ok(new { success = true, message = "Đã thêm shop mới", action = "INSERT" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi save token");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Đồng bộ đơn hàng vào ERP (m65/d65/i65/c65)
        /// - Check xem đơn đã sync chưa
        /// - Nếu rồi: DELETE old records và INSERT new (update)
        /// - Nếu chưa: INSERT new
        /// </summary>
        private async Task SyncOrderToERPAsync(
            int shopId,
            string pancakeOrderId,
            Services.PanCakeOrderDetail orderDetail,
            int webhookId)
        {
            try
            {
                _logger.LogInformation("🔄 Bắt đầu đồng bộ đơn hàng vào ERP...");
                _logger.LogInformation("   PanCake Order ID: {OrderId}, Shop ID: {ShopId}", pancakeOrderId, shopId);

                // 1. CHECK xem đơn đã sync chưa
                var existingMapping = await _dbContext.PanCakeOrderMappings
                    .FirstOrDefaultAsync(m => m.PanCakeOrderId == pancakeOrderId && m.ShopId == shopId);

                if (existingMapping != null)
                {
                    _logger.LogWarning("⚠️ Đơn hàng đã được đồng bộ trước đó!");
                    _logger.LogInformation("   STT_REC cũ: {SttRec}, Lần đầu sync: {FirstSync}, Số lần sync: {Count}",
                        existingMapping.SttRec, existingMapping.FirstSyncedAt, existingMapping.SyncCount);
                    
                    // XÓA records cũ trong ERP trước khi INSERT mới
                    await DeleteOldERPRecordsAsync(existingMapping);

                    _logger.LogInformation("🔄 Đang CẬP NHẬT đơn hàng (đè vào đơn cũ)...");
                }
                else
                {
                    _logger.LogInformation("✨ Đơn hàng MỚI - chưa đồng bộ lần nào");
                }

                // 2. SYNC vào ERP (insert m65, d65, sau đó 3 stored procedures tạo i65 + c65)
                _logger.LogWarning(">>> DEBUG: Trước khi gọi _orderSyncService.SyncOrderToERPAsync...");
                var syncResult = await _orderSyncService.SyncOrderToERPAsync(shopId, orderDetail, pancakeOrderId);
                _logger.LogWarning(">>> DEBUG: Sau khi gọi _orderSyncService.SyncOrderToERPAsync, Success={Success}, Error={Error}",
                    syncResult.Success, syncResult.ErrorMessage ?? "null");

                if (syncResult.Success)
                {
                    _logger.LogInformation("✅ Đồng bộ ERP thành công!");
                    _logger.LogInformation("   STT_REC: {SttRec}, SO_CT: {SoCt}, MA_CT: {MaCt}",
                        syncResult.SttRec, syncResult.SoCt, syncResult.MaCt);

                    // 3. Lưu/Cập nhật mapping
                    if (existingMapping != null)
                    {
                        // UPDATE mapping
                        existingMapping.LastSyncedAt = DateTime.Now;
                        existingMapping.SyncCount++;
                        existingMapping.LastStatus = orderDetail.Status_Name;
                        existingMapping.Note = $"Updated from webhook #{webhookId} at {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
                        
                        _dbContext.PanCakeOrderMappings.Update(existingMapping);
                        _logger.LogInformation("📝 Đã cập nhật mapping - Lần sync thứ {Count}", existingMapping.SyncCount);
                    }
                    else
                    {
                        // INSERT mapping mới
                        var newMapping = new Data.Entities.PanCakeOrderMappingEntity
                        {
                            PanCakeOrderId = pancakeOrderId,
                            ShopId = shopId,
                            SttRec = syncResult.SttRec!,
                            MaCt = syncResult.MaCt,
                            SoCt = syncResult.SoCt,
                            Partition = syncResult.SttRec?.Length >= 6 ? syncResult.SttRec.Substring(0, 6) : null,
                            FirstSyncedAt = DateTime.Now,
                            LastSyncedAt = DateTime.Now,
                            SyncCount = 1,
                            LastStatus = orderDetail.Status_Name,
                            Note = $"Created from webhook #{webhookId} at {DateTime.Now:yyyy-MM-dd HH:mm:ss}"
                        };

                        _dbContext.PanCakeOrderMappings.Add(newMapping);
                        _logger.LogInformation("📝 Đã tạo mapping mới");
                    }

                        await _dbContext.SaveChangesAsync();
                        _logger.LogWarning(">>> DEBUG: SaveChangesAsync thành công!");


                }
                else
                {
                    _logger.LogError("❌ Đồng bộ ERP THẤT BẠI: {Error}", syncResult.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ LỖI khi đồng bộ đơn hàng vào ERP");
                _logger.LogError("   Order ID: {OrderId}, Shop ID: {ShopId}", pancakeOrderId, shopId);
                _logger.LogError("   Exception: {Message}", ex.Message);
            }
        }

        /// <summary>
        /// Xóa records cũ trong ERP trước khi update (DELETE from m65/d65/i65/c65 hoặc m760/d760/i760/c760)
        /// </summary>
        private async Task DeleteOldERPRecordsAsync(Data.Entities.PanCakeOrderMappingEntity mapping)
        {
            try
            {
                _logger.LogInformation("🗑️ Đang xóa records cũ trong ERP...");
                _logger.LogInformation("   STT_REC: {SttRec}, Partition: {Partition}, MaCt: {MaCt}",
                    mapping.SttRec, mapping.Partition, mapping.MaCt);

                var connectionString = _dbContext.Database.GetConnectionString();
                using (var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    var partition = mapping.Partition ?? "000000";
                    var sttRec = mapping.SttRec;
                    var maCt = mapping.MaCt ?? "DXB";

                    string deleteSql;
                    if (maCt == "SDF")
                    {
                        // SDF: xóa từ bảng m760/d760/i760/c760
                        deleteSql = $@"
                            DELETE FROM d760${partition} WHERE stt_rec = @stt_rec;
                            DELETE FROM m760${partition} WHERE stt_rec = @stt_rec;
                            DELETE FROM i760${partition} WHERE stt_rec = @stt_rec;
                            DELETE FROM c760$000000 WHERE stt_rec = @stt_rec;";
                        _logger.LogInformation("Xóa SDF records (m760/d760/i760/c760) cho STT_REC: {SttRec}", sttRec);
                    }
                    else
                    {
                        // DXB: xóa từ bảng m65/d65/i65/c65
                        deleteSql = $@"
                            DELETE FROM d65${partition} WHERE stt_rec = @stt_rec;
                            DELETE FROM m65${partition} WHERE stt_rec = @stt_rec;
                            DELETE FROM i65${partition} WHERE stt_rec = @stt_rec;
                            DELETE FROM c65$000000 WHERE stt_rec = @stt_rec;";
                        _logger.LogInformation("Xóa DXB records (m65/d65/i65/c65) cho STT_REC: {SttRec}", sttRec);
                    }

                    using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(deleteSql, connection))
                    {
                        cmd.Parameters.AddWithValue("@stt_rec", sttRec);
                        var rowsAffected = await cmd.ExecuteNonQueryAsync();
                        _logger.LogInformation("✅ Đã xóa {Rows} records cũ ({MaCt})", rowsAffected, maCt);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Lỗi khi xóa records cũ trong ERP");
                // Không throw - vẫn tiếp tục insert mới
            }
        }

        /// <summary>
        /// Chuyển đổi PanCake webhook (flat structure) sang entity để lưu database
        /// </summary>
        private WebhookOrderEntity ConvertPanCakeWebhookToEntity(PanCakeOrderWebhook webhook, string rawJson)
        {
            return new WebhookOrderEntity
            {
                // Chỉ lưu các field cần thiết
                PanCakeOrderId = webhook.Id.ToString(),
                OrderCode = webhook.Order_Code,
                ShopId = webhook.Shop_Id,
                EventType = webhook.Event_Type ?? "unknown",
                Status = webhook.Status_Name,
                TotalAmount = webhook.Total_Price,
                CustomerName = webhook.Creator?.Name,
                CustomerEmail = webhook.Creator?.Email,
                RawWebhookData = rawJson,
                PanCakeTimestamp = webhook.Timestamp,
                Signature = webhook.Signature,
                IsProcessed = false,
                ProcessingNote = $"Webhook received - Event: {webhook.Event_Type}, Status: {webhook.Status_Name}"
            };
        }

        /* OLD METHODS - REMOVED (entity simplified)
        #region Old Methods - Keep for reference

        /// <summary>
        /// Chuyển đổi webhook request sang entity để lưu vào database (OLD - Model cũ)
        /// </summary>
        private WebhookOrderEntity ConvertToEntity(PanCakeWebhookRequest webhookData)
        {
            var data = webhookData.Data;
            var itemsJson = data?.Items != null ? JsonSerializer.Serialize(data.Items) : null;
            var rawData = JsonSerializer.Serialize(webhookData);

            return new WebhookOrderEntity
            {
                // Thông tin đơn hàng
                PanCakeOrderId = data?.Id,
                OrderCode = data?.Code,
                EventType = webhookData.Event,
                ShopId = data?.ShopId,
                BranchId = data?.BranchId,
                Status = data?.Status,

                // Thông tin giá
                TotalAmount = data?.TotalAmount,
                DiscountAmount = data?.DiscountAmount,
                FinalAmount = data?.FinalAmount,

                // Thanh toán
                PaymentMethod = data?.PaymentMethod,
                PaymentStatus = data?.PaymentStatus,

                // Khách hàng
                CustomerId = data?.Customer?.Id,
                CustomerName = data?.Customer?.Name,
                CustomerPhone = data?.Customer?.Phone,
                CustomerEmail = data?.Customer?.Email,

                // Địa chỉ giao hàng
                ReceiverName = data?.ShippingAddress?.ReceiverName,
                ReceiverPhone = data?.ShippingAddress?.ReceiverPhone,
                ShippingAddress = data?.ShippingAddress?.FullAddress,
                Province = data?.ShippingAddress?.Province,
                District = data?.ShippingAddress?.District,
                Ward = data?.ShippingAddress?.Ward,

                // Ghi chú
                Note = data?.Note,

                // Sản phẩm
                ItemsCount = data?.Items?.Count ?? 0,
                ItemsJson = itemsJson,

                // Raw data
                RawWebhookData = rawData,

                // Timestamp
                OrderCreatedAt = data?.CreatedAt,
                OrderUpdatedAt = data?.UpdatedAt,
                PanCakeTimestamp = webhookData.Timestamp,
                Signature = webhookData.Signature,

                // Processing status
                IsProcessed = false
            };
        }

        /// <summary>
        /// Xử lý khi có đơn hàng mới được tạo
        /// </summary>
        private async Task HandleOrderCreated(WebhookOrderData orderData, int webhookId)
        {
            _logger.LogInformation("=== XỬ LÝ ĐƠN HÀNG MỚI ===");
            _logger.LogInformation("Order ID: {OrderId}", orderData.Id);
            _logger.LogInformation("Order Code: {OrderCode}", orderData.Code);
            _logger.LogInformation("Customer: {CustomerName} - {CustomerPhone}",
                orderData.Customer?.Name, orderData.Customer?.Phone);
            _logger.LogInformation("Total Amount: {Amount} VNĐ", orderData.FinalAmount);
            _logger.LogInformation("Items Count: {ItemsCount}", orderData.Items?.Count ?? 0);

            // TODO: Thêm logic xử lý đơn hàng mới
            // Ví dụ:
            // - Gửi email thông báo cho khách hàng
            // - Gửi thông báo cho warehouse để chuẩn bị hàng
            // - Cập nhật tồn kho
            // - Tích hợp với hệ thống vận chuyển

            // Đánh dấu đã xử lý
            await _webhookDbService.MarkAsProcessedAsync(webhookId, "Order created processed successfully");
        }

        /// <summary>
        /// Xử lý khi đơn hàng được cập nhật
        /// </summary>
        private async Task HandleOrderUpdated(WebhookOrderData orderData, int webhookId)
        {
            _logger.LogInformation("=== ĐƠN HÀNG ĐƯỢC CẬP NHẬT ===");
            _logger.LogInformation("Order ID: {OrderId}", orderData.Id);
            _logger.LogInformation("Order Code: {OrderCode}", orderData.Code);
            _logger.LogInformation("New Status: {Status}", orderData.Status);

            // TODO: Thêm logic xử lý cập nhật đơn hàng
            // Ví dụ:
            // - Gửi thông báo cho khách hàng về thay đổi
            // - Đồng bộ với các hệ thống khác

            // Đánh dấu đã xử lý
            await _webhookDbService.MarkAsProcessedAsync(webhookId, "Order updated processed successfully");
        }

        /// <summary>
        /// Xử lý khi đơn hàng được xác nhận
        /// </summary>
        private async Task HandleOrderConfirmed(WebhookOrderData orderData, int webhookId)
        {
            _logger.LogInformation("=== ĐƠN HÀNG ĐƯỢC XÁC NHẬN ===");
            _logger.LogInformation("Order ID: {OrderId}", orderData.Id);
            _logger.LogInformation("Order Code: {OrderCode}", orderData.Code);

            // TODO: Thêm logic xử lý đơn hàng được xác nhận
            // Ví dụ:
            // - Gửi email xác nhận cho khách hàng
            // - Bắt đầu quy trình đóng gói và giao hàng
            // - Cập nhật trạng thái trong hệ thống

            // Đánh dấu đã xử lý
            await _webhookDbService.MarkAsProcessedAsync(webhookId, "Order confirmed processed successfully");
        }

        /// <summary>
        /// Xử lý khi đơn hàng bị hủy
        /// </summary>
        private async Task HandleOrderCancelled(WebhookOrderData orderData, int webhookId)
        {
            _logger.LogInformation("=== ĐƠN HÀNG BỊ HỦY ===");
            _logger.LogInformation("Order ID: {OrderId}", orderData.Id);
            _logger.LogInformation("Order Code: {OrderCode}", orderData.Code);
            _logger.LogInformation("Cancellation Note: {Note}", orderData.Note);

            // TODO: Thêm logic xử lý đơn hàng bị hủy
            // Ví dụ:
            // - Hoàn trả tồn kho
            // - Xử lý hoàn tiền nếu đã thanh toán
            // - Gửi thông báo cho khách hàng
            // - Cập nhật báo cáo, thống kê

            // Đánh dấu đã xử lý
            await _webhookDbService.MarkAsProcessedAsync(webhookId, "Order cancelled processed successfully");
        }

        /// <summary>
        /// Xử lý khi đơn hàng hoàn thành
        /// </summary>
        private async Task HandleOrderCompleted(WebhookOrderData orderData, int webhookId)
        {
            _logger.LogInformation("=== ĐƠN HÀNG HOÀN THÀNH ===");
            _logger.LogInformation("Order ID: {OrderId}", orderData.Id);
            _logger.LogInformation("Order Code: {OrderCode}", orderData.Code);
            _logger.LogInformation("Final Amount: {Amount} VNĐ", orderData.FinalAmount);

            // TODO: Thêm logic xử lý đơn hàng hoàn thành
            // Ví dụ:
            // - Gửi email cảm ơn khách hàng
            // - Yêu cầu đánh giá sản phẩm
            // - Cập nhật doanh thu
            // - Tích điểm cho khách hàng
            // - Tạo invoice/hóa đơn điện tử

            // Đánh dấu đã xử lý
            await _webhookDbService.MarkAsProcessedAsync(webhookId, "Order completed processed successfully");
        }

        #endregion
        */

        // ============= CUSTOMER WEBHOOK ENDPOINTS =============
        
        /// <summary>
        /// Nhận webhook từ PanCake khi có khách hàng mới hoặc thay đổi
        /// </summary>
        /// <param name="rawData">Dữ liệu webhook từ PanCake</param>
        /// <returns></returns>
        /// <remarks>
        /// Endpoint này sẽ nhận các sự kiện từ PanCake như:
        /// - customer.created: Khách hàng mới được tạo
        /// - customer.updated: Khách hàng được cập nhật
        /// - customer.deleted: Khách hàng bị xóa
        /// 
        /// Để sử dụng:
        /// 1. Đăng ký URL webhook này trong PanCake: https://your-domain.com/api/webhook/customer
        /// 2. PanCake sẽ tự động gửi POST request đến endpoint này khi có sự kiện
        /// 
        /// Ví dụ webhook data từ PanCake:
        /// 
        ///     POST /api/webhook/customer
        ///     {
        ///         "id": 123456,
        ///         "event_type": "customer.created",
        ///         "shop_id": 789,
        ///         "customer_code": "KH-000123",
        ///         "name": "Nguyễn Văn A",
        ///         "phone_number": "0901234567",
        ///         "email": "customer@example.com",
        ///         "address": "123 Đường ABC",
        ///         "province_name": "TP. Hồ Chí Minh",
        ///         "district_name": "Quận 1",
        ///         "commune_name": "Phường Bến Nghé",
        ///         "customer_level_name": "VIP",
        ///         "point": 1000,
        ///         "total_spent": 5000000,
        ///         "gender": "male",
        ///         "date_of_birth": "1990-01-01",
        ///         "inserted_at": "2024-03-10T10:30:00Z",
        ///         "updated_at": "2024-03-10T10:30:00Z",
        ///         "timestamp": 1710000000,
        ///         "signature": "abc123..."
        ///     }
        /// </remarks>
        [HttpPost("customer")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ReceiveCustomerWebhook([FromBody] JsonElement rawData)
        {
            _logger.LogInformation("🔗 Endpoint /api/webhook/customer được gọi - redirect to ProcessCustomerWebhook");
            var rawJson = rawData.GetRawText();
            return await ProcessCustomerWebhook(rawData, rawJson);
        }

        /// <summary>
        /// Lấy danh sách customer webhooks đã nhận
        /// </summary>
        /// <param name="skip">Số bản ghi bỏ qua (phân trang)</param>
        /// <param name="take">Số bản ghi lấy (phân trang)</param>
        /// <param name="isProcessed">Lọc theo trạng thái xử lý (null = tất cả)</param>
        /// <returns>Danh sách customer webhooks</returns>
        [HttpGet("customers")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetCustomerWebhooks(
            [FromQuery] int skip = 0,
            [FromQuery] int take = 100,
            [FromQuery] bool? isProcessed = null)
        {
            try
            {
                var webhooks = await _webhookDbService.GetCustomerWebhooksAsync(skip, take, isProcessed);
                
                return Ok(new
                {
                    success = true,
                    data = webhooks,
                    count = webhooks.Count,
                    skip,
                    take,
                    isProcessed
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while getting customer webhooks");
                return BadRequest(new
                {
                    success = false,
                    message = "Failed to get customer webhooks",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Xử lý webhook ORDER từ PanCake (Private method)
        /// </summary>
        private async Task<IActionResult> ProcessOrderWebhook(JsonElement rawData, string rawJson)
        {
            try
            {
                var logSql = @"INSERT INTO Webhook_Logs 
                (WebhookType, ReceivedAt, RawJsonPreview, Note, CreatedAt)
            VALUES 
                (@WebhookType, @ReceivedAt, @RawJsonPreview, @Note, GETDATE())";

                var preview = string.IsNullOrEmpty(rawJson)
                    ? ""
                    : (rawJson.Length > 1000 ? rawJson.Substring(0, 1000) + "..." : rawJson);

                await _dbContext.Database.ExecuteSqlRawAsync(logSql,
                    new Microsoft.Data.SqlClient.SqlParameter("@WebhookType", "Order"),
                    new Microsoft.Data.SqlClient.SqlParameter("@ReceivedAt", DateTime.Now),
                    new Microsoft.Data.SqlClient.SqlParameter("@RawJsonPreview", preview),
                    new Microsoft.Data.SqlClient.SqlParameter("@Note", "ProcessOrderWebhook - ĐÃ VÀO HÀM")
                );
                _logger.LogInformation("=== XỬ LÝ ORDER WEBHOOK ===");

                // Parse sang model ORDER
                PanCakeOrderWebhook? webhookData = null;
                try
                {
                    webhookData = JsonSerializer.Deserialize<PanCakeOrderWebhook>(rawJson, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    _logger.LogInformation("=== PARSED WEBHOOK DATA ===");
                    _logger.LogInformation("Order ID: {OrderId}", webhookData?.Id);
                    _logger.LogInformation("Event Type: {EventType}", webhookData?.Event_Type);
                    _logger.LogInformation("Shop ID: {ShopId}", webhookData?.Shop_Id);
                    _logger.LogInformation("Status: {Status} ({StatusName})", webhookData?.Status, webhookData?.Status_Name);
                    _logger.LogInformation("Total Price: {TotalPrice}", webhookData?.Total_Price);
                }
                catch (Exception parseEx)
                {
                    _logger.LogError(parseEx, "Lỗi khi parse webhook JSON");
                }

                // Convert sang Entity để lưu database
                WebhookOrderEntity webhookEntity;
                if (webhookData == null || webhookData.Id == null)
                {
                    _logger.LogWarning("⚠️ Webhook parse failed hoặc không có Order ID - lưu raw data");
                    
                    webhookEntity = new WebhookOrderEntity
                    {
                        EventType = "unknown",
                        RawWebhookData = rawJson,
                        WebhookReceivedAt = DateTime.Now,
                        IsProcessed = false,
                        ProcessingNote = "Parse failed or missing Order ID - raw data saved"
                    };
                }
                else
                {
                    webhookEntity = ConvertPanCakeWebhookToEntity(webhookData, rawJson);
                }

                // LƯU DỮ LIỆU VÀO DATABASE
                var savedWebhook = await _webhookDbService.SaveWebhookAsync(webhookEntity);
                _logger.LogInformation("✅ Webhook đã được lưu vào database. Webhook ID: {WebhookId}", savedWebhook.WebhookId);

                // CALL PANCAKE API để lấy thông tin đơn hàng chi tiết
                if (webhookData != null && webhookData.Shop_Id.HasValue)
                {
                    _logger.LogInformation("🔄 Bắt đầu lấy chi tiết đơn hàng từ PanCake API...");
                    _logger.LogInformation("   Shop ID: {ShopId}, Order ID: {OrderId}", webhookData.Shop_Id, webhookData.Id);
                    
                    try
                    {
                        var orderDetail = await _panCakeOrderService.GetOrderDetailAsync(
                            webhookData.Shop_Id.Value, 
                            webhookData.Id);

                        if (orderDetail != null)
                        {
                            _logger.LogInformation("✅ Lấy thông tin đơn hàng thành công!");
                            _logger.LogInformation("   - Order Code: {OrderCode}", orderDetail.Order_Code);
                            _logger.LogInformation("   - Status: {Status}", orderDetail.Status_Name);
                            _logger.LogInformation("   - Total: {Total:N0} VNĐ", orderDetail.Total_Price);
                            _logger.LogInformation("   - Customer: {Customer}", orderDetail.Creator?.Name);
                            _logger.LogInformation("   - Items: {Count}", orderDetail.Items?.Count ?? 0);

                            var logSql2 = @"INSERT INTO Webhook_Logs_ver3 
                (WebhookType, ReceivedAt, RawJsonPreview, Note, CreatedAt)
            VALUES 
                (@WebhookType, @ReceivedAt, @RawJsonPreview, @Note, GETDATE())";


                            await _dbContext.Database.ExecuteSqlRawAsync(logSql2,
                                new Microsoft.Data.SqlClient.SqlParameter("@WebhookType", "Order"),
                                new Microsoft.Data.SqlClient.SqlParameter("@ReceivedAt", DateTime.Now),
                                new Microsoft.Data.SqlClient.SqlParameter("@RawJsonPreview", orderDetail.Status_Name),
                                new Microsoft.Data.SqlClient.SqlParameter("@Note", "ProcessOrderWebhook - ĐÃ VÀO HÀM")
                            );

                            // ĐỒNG BỘ VÀO ERP
                            await SyncOrderToERPAsync(
                                webhookData.Shop_Id.Value,
                                webhookData.Id,
                                orderDetail,
                                savedWebhook.WebhookId);

                            await _webhookDbService.MarkAsProcessedAsync(
                                savedWebhook.WebhookId, 
                                $"Đã đồng bộ vào ERP - {orderDetail.Items?.Count ?? 0} sản phẩm hoantest");
                        }
                        else
                        {
                            _logger.LogWarning("⚠️ Không lấy được thông tin đơn hàng từ PanCake API");
                            await _webhookDbService.MarkAsProcessedAsync(
                                savedWebhook.WebhookId, 
                                "Không lấy được order detail từ API (có thể thiếu config hoặc API error)");
                        }
                    }
                    catch (Exception apiEx)
                    {
                        _logger.LogError(apiEx, "❌ Lỗi khi call PanCake API");
                    }
                }

                return Ok(new
                {
                    success = true,
                    message = "Order webhook received and processed",
                    webhookId = savedWebhook.WebhookId,
                    orderId = webhookData?.Id,
                    orderCode = webhookData?.Order_Code,
                    eventType = webhookData?.Event_Type
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xử lý order webhook");
                return Ok(new
                {
                    success = false,
                    message = "Order webhook received but error occurred: " + ex.Message
                });
            }
        }

        /// <summary>
        /// Xử lý webhook CUSTOMER từ PanCake (Private method)
        /// </summary>
        private async Task<IActionResult> ProcessCustomerWebhook(JsonElement rawData, string rawJson)
        {
            try
            {
                _logger.LogInformation("=== XỬ LÝ CUSTOMER WEBHOOK ===");

                // Parse sang model CUSTOMER
                PanCakeCustomerWebhook? webhookData = null;
                try
                {
                    webhookData = JsonSerializer.Deserialize<PanCakeCustomerWebhook>(rawJson, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    _logger.LogInformation("=== PARSED CUSTOMER WEBHOOK DATA ===");
                    _logger.LogInformation("Customer ID: {CustomerId}", webhookData?.Id);
                    _logger.LogInformation("Event Type: {EventType}", webhookData?.Event_Type);
                    _logger.LogInformation("Type: {Type}", webhookData?.Type);
                    _logger.LogInformation("Shop ID: {ShopId}", webhookData?.Shop_Id);
                    _logger.LogInformation("Customer Name: {CustomerName}", webhookData?.Name);
                    _logger.LogInformation("Phone Numbers: {Phones}", webhookData?.Phone_Numbers != null ? string.Join(", ", webhookData.Phone_Numbers) : "null");
                    _logger.LogInformation("Emails: {Emails}", webhookData?.Emails != null ? string.Join(", ", webhookData.Emails) : "null");
                }
                catch (Exception parseEx)
                {
                    _logger.LogError(parseEx, "Lỗi khi parse customer webhook JSON");
                }

                // Convert sang Entity
                WebhookCustomerEntity webhookEntity;
                if (webhookData == null || string.IsNullOrEmpty(webhookData.Id))
                {
                    _logger.LogWarning("⚠️ Customer webhook parse failed hoặc không có Customer ID");
                    
                    webhookEntity = new WebhookCustomerEntity
                    {
                        EventType = "unknown",
                        RawWebhookData = rawJson,
                        WebhookReceivedAt = DateTime.Now,
                        IsProcessed = false,
                        ProcessingNote = "Parse failed or missing Customer ID"
                    };
                }
                else
                {
                    webhookEntity = ConvertPanCakeCustomerWebhookToEntity(webhookData, rawJson);
                }

                // LƯU VÀO DATABASE
                var savedWebhook = await _webhookDbService.SaveCustomerWebhookAsync(webhookEntity);
                _logger.LogInformation("✅ Customer webhook đã được lưu. Webhook ID: {WebhookId}", savedWebhook.WebhookId);

                // ĐỒNG BỘ VÀO ERP
                string? maKh = null;
                string? syncMessage = null;
                bool isNewCustomer = false;
                
                if (webhookData != null && !string.IsNullOrEmpty(webhookData.Id))
                {
                    try
                    {
                        _logger.LogInformation("🔄 Bắt đầu đồng bộ khách hàng vào ERP...");
                        
                        var syncResult = await _customerSyncService.SyncCustomerToERPAsync(webhookData);
                        
                        if (syncResult.Success)
                        {
                            maKh = syncResult.MaKh;
                            isNewCustomer = syncResult.IsNewCustomer;
                            syncMessage = syncResult.Message;
                            
                            if (isNewCustomer)
                            {
                                _logger.LogInformation("✅ Đã tạo khách hàng mới trong ERP. Mã KH: {MaKh}", maKh);
                            }
                            else
                            {
                                _logger.LogInformation("ℹ️ Khách hàng đã tồn tại trong ERP. Mã KH: {MaKh}", maKh);
                            }
                            
                            await _webhookDbService.MarkCustomerAsProcessedAsync(
                                savedWebhook.WebhookId,
                                $"Đã đồng bộ vào dmkh - Mã KH: {maKh} - {syncMessage}");
                        }
                        else
                        {
                            _logger.LogWarning("⚠️ Không đồng bộ được: {Error}", syncResult.ErrorMessage);
                            await _webhookDbService.MarkCustomerAsProcessedAsync(
                                savedWebhook.WebhookId,
                                $"Lỗi đồng bộ: {syncResult.ErrorMessage}");
                            syncMessage = syncResult.ErrorMessage;
                        }
                    }
                    catch (Exception syncEx)
                    {
                        _logger.LogError(syncEx, "❌ Lỗi khi đồng bộ khách hàng");
                        await _webhookDbService.MarkCustomerAsProcessedAsync(
                            savedWebhook.WebhookId,
                            $"Exception: {syncEx.Message}");
                        syncMessage = $"Exception: {syncEx.Message}";
                    }
                }

                return Ok(new
                {
                    success = true,
                    message = "Customer webhook received and processed",
                    webhookId = savedWebhook.WebhookId,
                    customerId = webhookData?.Id,
                    customerName = webhookData?.Name,
                    eventType = webhookData?.Event_Type,
                    syncToERP = new
                    {
                        maKh = maKh,
                        isNewCustomer = isNewCustomer,
                        message = syncMessage
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Lỗi xử lý customer webhook");
                return Ok(new
                {
                    success = false,
                    message = "Customer webhook received but error occurred: " + ex.Message
                });
            }
        }

        private WebhookCustomerEntity ConvertPanCakeCustomerWebhookToEntity(PanCakeCustomerWebhook webhook, string rawJson)
        {
            // Parse date of birth từ birthday object nếu có
            DateTime? dateOfBirth = null;
            if (webhook.Birthday != null && 
                !string.IsNullOrEmpty(webhook.Birthday.Year) && 
                !string.IsNullOrEmpty(webhook.Birthday.Month) && 
                !string.IsNullOrEmpty(webhook.Birthday.Date))
            {
                try
                {
                    var year = int.Parse(webhook.Birthday.Year);
                    var month = int.Parse(webhook.Birthday.Month);
                    var day = int.Parse(webhook.Birthday.Date);
                    dateOfBirth = new DateTime(year, month, day);
                    _logger.LogInformation("Parsed birthday: {Birthday}", dateOfBirth);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Không parse được birthday: {Birthday}", 
                        $"{webhook.Birthday.Year}-{webhook.Birthday.Month}-{webhook.Birthday.Date}");
                }
            }

            // Lấy phone từ array (phần tử đầu tiên)
            var phone = webhook.Phone_Numbers?.FirstOrDefault();
            
            // Lấy email từ array (phần tử đầu tiên)
            var email = webhook.Emails?.FirstOrDefault();
            
            // Sử dụng billing_address_street làm địa chỉ
            var address = webhook.Billing_Address_Street;

            return new WebhookCustomerEntity
            {
                PanCakeCustomerId = webhook.Id,  // UUID string
                CustomerCode = null,  // Không có trong webhook
                ShopId = webhook.Shop_Id.HasValue ? (int?)webhook.Shop_Id.Value : null,
                EventType = webhook.Event_Type ?? "unknown",
                CustomerName = webhook.Name,
                CustomerPhone = phone,
                CustomerEmail = email,
                CustomerAddress = address,
                CustomerLevel = webhook.Level_Name,
                CustomerPoints = webhook.Reward_Point,
                TotalSpent = null,  // Không có trong webhook
                Gender = webhook.Gender,
                DateOfBirth = dateOfBirth,
                RawWebhookData = rawJson,
                PanCakeTimestamp = null,  // Không có trong webhook
                Signature = null,  // Không có trong webhook
                IsProcessed = false,
                ProcessingNote = $"Customer webhook received - Event: {webhook.Event_Type}, Name: {webhook.Name}, Phone: {phone}"
            };
        }

        /// <summary>
        /// Endpoint test để kiểm tra webhook hoạt động
        /// </summary>
        /// <returns></returns>
        [HttpGet("test")]
        public IActionResult TestWebhook()
        {
            return Ok(new
            {
                success = true,
                message = "Webhook endpoint is working!",
                endpoint = "/api/webhook/order",
                instructions = "Đăng ký URL này trong PanCake để nhận webhook"
            });
        }

        /// <summary>
        /// Model request để lưu/cập nhật token cho shop
        /// </summary>
        public class SaveTokenWebhookRequest
        {
            public string ShopId { get; set; } = string.Empty;
            public string ApiToken { get; set; } = string.Empty;
            public string? TenShop { get; set; }
            public string? MaKhoDh { get; set; }
            public string? MaKhoTonKho { get; set; }
            public string? MaShop { get; set; }
        }
    }
}
