using API_PanCake.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace API_PanCake.Services
{
    public interface IPanCakeOrderService
    {
        Task<PanCakeOrderDetail?> GetOrderDetailAsync(int shopId, string orderIdOrCode);
    }

    public class PanCakeOrderService : IPanCakeOrderService
    {
        private readonly AppDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<PanCakeOrderService> _logger;

        public PanCakeOrderService(
            AppDbContext context,
            IHttpClientFactory httpClientFactory,
            ILogger<PanCakeOrderService> logger)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <summary>
        /// Lấy chi tiết đơn hàng từ PanCake API
        /// </summary>
        public async Task<PanCakeOrderDetail?> GetOrderDetailAsync(int shopId, string orderIdOrCode)
        {
            try
            {
                _logger.LogInformation("=== BẮT ĐẦU LẤY THÔNG TIN ĐƠN HÀNG ===");
                _logger.LogInformation("Input - ShopId: {ShopId}, OrderIdOrCode: {OrderId}", shopId, orderIdOrCode);
                
                var apiKey = await GetApiKeyByShopId(shopId);
                if (string.IsNullOrEmpty(apiKey))
                {
                    _logger.LogError("❌ DỪNG: Không tìm thấy API key cho shop_id: {ShopId}", shopId);
                    _logger.LogError("👉 Hướng dẫn: Chạy endpoint POST /api/sync-order/save-token để thêm token");
                    return null;
                }

                _logger.LogInformation("✅ Đã lấy token thành công - Length: {Length} chars", apiKey.Length);

                // Bước 2: Call PanCake API với api_key parameter trong URL
                // URL encode orderIdOrCode để xử lý các ký tự đặc biệt như #
                var encodedOrderId = Uri.EscapeDataString(orderIdOrCode);
                var apiUrl = $"https://pos.pages.fm/api/v1/shops/{shopId}/orders/{encodedOrderId}?api_key={apiKey}";
                _logger.LogInformation("📞 Đang gọi PanCake API: {Url}", 
                    $"https://pos.pages.fm/api/v1/shops/{shopId}/orders/{encodedOrderId}?api_key=***");
                
                var httpClient = _httpClientFactory.CreateClient();
                
                _logger.LogInformation("🔐 API Key preview: {TokenPreview}...", apiKey.Substring(0, Math.Min(20, apiKey.Length)));

                var response = await httpClient.GetAsync(apiUrl);

                _logger.LogInformation("📥 Response Status: {StatusCode} - {ReasonPhrase}", 
                    response.StatusCode, response.ReasonPhrase);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("❌ PanCake API LỖI: {StatusCode} - {ReasonPhrase}", 
                        response.StatusCode, response.ReasonPhrase);
                    _logger.LogError("Error Response: {ErrorContent}", errorContent);
                    return null;
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("✅ Lấy thông tin đơn hàng thành công từ PanCake API");
                _logger.LogInformation("📊 Response length: {Length} chars", jsonContent.Length);
                _logger.LogInformation("📄 Full Response JSON:\n{RawJson}", jsonContent);

                // Parse JSON response - PanCake API có thể trả về { "success": true, "data": {...} } hoặc trực tiếp {...}
                using var jsonDoc = JsonDocument.Parse(jsonContent);
                string jsonToParse;
                
                if (jsonDoc.RootElement.TryGetProperty("data", out var dataElement))
                {
                    // Nested structure: { "success": true, "data": {...} }
                    _logger.LogInformation("🔍 Detected nested structure with 'data' wrapper");
                    jsonToParse = dataElement.GetRawText();
                    _logger.LogInformation("📦 Data content length: {Length} chars", jsonToParse.Length);
                }
                else
                {
                    // Flat structure: trực tiếp order data
                    _logger.LogInformation("🔍 Detected flat structure without wrapper");
                    jsonToParse = jsonContent;
                }
                
                _logger.LogInformation("🔧 Parsing JSON to PanCakeOrderDetail...");
                var orderDetail = JsonSerializer.Deserialize<PanCakeOrderDetail>(jsonToParse, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (orderDetail != null)
                {
                    _logger.LogInformation("✅ Parse thành công - Order ID: {Id}, Code: {Code}, Items: {ItemCount}", 
                        orderDetail.Id, orderDetail.Order_Code, orderDetail.Items?.Count ?? 0);
                }
                else
                {
                    _logger.LogWarning("⚠️ Parse thành công nhưng orderDetail = null");
                }

                return orderDetail;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy chi tiết đơn hàng từ PanCake API");
                return null;
            }
        }

        /// <summary>
        /// Lấy API key từ bảng dmkb_api theo shop_id
        /// </summary>
        private async Task<string?> GetApiKeyByShopId(int shopId)
        {
            try
            {
                var shopIdStr = shopId.ToString().Trim();
                
                _logger.LogInformation("🔍 === TÌM KIẾM API KEY ===");
                _logger.LogInformation("🔍 Shop ID input: {ShopId} (type: {Type})", shopId, shopId.GetType().Name);
                _logger.LogInformation("🔍 Shop ID string: '{ShopIdStr}'", shopIdStr);
                
                // Query dmkb_api - check status = "1" (active)
                var apiConfig = await _context.DmkbApis
                    .Where(x => x.IdShop.Trim() == shopIdStr && x.Status == "1")
                    .FirstOrDefaultAsync();

                if (apiConfig == null)
                {
                    _logger.LogError("❌ KHÔNG TÌM THẤY config cho shop_id: '{ShopId}' với status='1'", shopIdStr);
                    
                    // Debug: kiểm tra xem có shop nào trong DB không
                    var totalShops = await _context.DmkbApis.CountAsync();
                    _logger.LogWarning("📊 Tổng số shops trong dmkb_api: {Count}", totalShops);
                    
                    if (totalShops > 0)
                    {
                        var allShopIds = await _context.DmkbApis
                            .Select(x => new { x.IdShop, x.Status, x.TenShop, HasToken = x.KeyApi != null && x.KeyApi != "" })
                            .Take(10)
                            .ToListAsync();
                        
                        _logger.LogWarning("📋 Danh sách shops có trong DB:");
                        foreach (var shop in allShopIds)
                        {
                            _logger.LogWarning("   - ID='{Id}' | Status='{Status}' | Name='{Name}' | HasToken={HasToken}", 
                                shop.IdShop, shop.Status, shop.TenShop, shop.HasToken);
                        }
                    }
                    else
                    {
                        _logger.LogError("❌ Bảng dmkb_api RỖNG! Không có shop nào.");
                        _logger.LogError("👉 Chạy: POST /api/sync-order/save-token để thêm shop và token");
                    }
                    
                    return null;
                }

                _logger.LogInformation("✅ TÌM THẤY shop trong database!");
                _logger.LogInformation("   - Shop ID: {ShopId}", apiConfig.IdShop);
                _logger.LogInformation("   - Tên shop: {ShopName}", apiConfig.TenShop);
                _logger.LogInformation("   - Status: {Status}", apiConfig.Status);
                _logger.LogInformation("   - Mã kho DH: {MaKho}", apiConfig.MaKhoDh);
                
                if (string.IsNullOrEmpty(apiConfig.KeyApi))
                {
                    _logger.LogError("❌ Shop tồn tại nhưng KeyApi = NULL hoặc rỗng!");
                    _logger.LogError("👉 Chạy: POST /api/sync-order/save-token để cập nhật token");
                    return null;
                }
                
                _logger.LogInformation("✅ Token hợp lệ:");
                _logger.LogInformation("   - Length: {Length} chars", apiConfig.KeyApi.Length);
                _logger.LogInformation("   - Preview: {Preview}...", 
                    apiConfig.KeyApi.Substring(0, Math.Min(30, apiConfig.KeyApi.Length)));

                return apiConfig.KeyApi;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ LỖI KHI QUERY dmkb_api");
                _logger.LogError("Exception: {Message}", ex.Message);
                _logger.LogError("StackTrace: {Stack}", ex.StackTrace);
                return null;
            }
        }
    }

    /// <summary>
    /// Model response từ PanCake Order API
    /// </summary>
    public class PanCakeOrderDetail
    {
        [JsonConverter(typeof(StringOrNumberConverter))]
        public string? Id { get; set; }
        public string? Order_Code { get; set; }
        public int? Shop_Id { get; set; }
        public int? Status { get; set; }
        public string? Status_Name { get; set; }
        public decimal? Total_Price { get; set; }
        public decimal? Total_Discount { get; set; }
        public decimal? Total_Quantity { get; set; }
        public decimal? Cod { get; set; }
        public decimal? Cash { get; set; }
        public decimal? Transfer_Money { get; set; }
        public decimal? Prepaid { get; set; }
        public decimal? Money_To_Collect { get; set; }
        public decimal? Shipping_Fee { get; set; }
        public string? Payment_Method { get; set; }
        public string? Shipping_Unit { get; set; }
        public PanCakeOrderCustomer? Creator { get; set; }
        public PanCakeOrderShippingAddress? Shipping_Address { get; set; }
        public PanCakeOrderCustomer? Customer { get; set; }
        public PanCakeOrderPartner? Partner { get; set; }
        public PanCakeOrderWarehouse? Warehouse_Info { get; set; }
        public List<PanCakeOrderItem>? Items { get; set; }
        public string? Note { get; set; }
        public string? Inserted_At { get; set; }
        public string? Updated_At { get; set; }
        public string? Order_Link { get; set; }
        public string? Tracking_Link { get; set; }
    }

    public class PanCakeOrderCustomer
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Phone_Number { get; set; }
        public string? Address { get; set; }
    }

    public class PanCakeOrderShippingAddress
    {
        public string? Full_Name { get; set; }
        public string? Phone_Number { get; set; }
        public string? Full_Address { get; set; }
        public string? Province_Name { get; set; }
        public string? District_Name { get; set; }
        public string? Commune_Name { get; set; }
    }

    public class PanCakeOrderPartner
    {
        public string? Partner_Name { get; set; }
        public string? Sort_Code { get; set; }
        public string? Extend_Code { get; set; }
        public string? Delivery_Name { get; set; }
        public string? Delivery_Tel { get; set; }
        public string? Order_Id_Ghn { get; set; }
        public string? Order_Number_Vtp { get; set; }
    }

    public class PanCakeOrderWarehouse
    {
        public string? Name { get; set; }
        public string? Address { get; set; }
        public string? Province_Name { get; set; }
        public string? District_Name { get; set; }
        public string? Commune_Name { get; set; }
        public string? Phone_Number { get; set; }
        public string? Custom_Id { get; set; }
        public string? Postcode { get; set; }
    }

    public class PanCakeOrderItem
    {
        public long? Id { get; set; }
        public string? Product_Id { get; set; }
        public string? Variation_Id { get; set; }
        public int? Quantity { get; set; }
        public decimal? Price { get; set; }
        public PanCakeOrderItemVariation? Variation_Info { get; set; }
    }

    public class PanCakeOrderItemVariation
    {
        public string? Name { get; set; }
        public string? Display_Id { get; set; }
        public string? Barcode { get; set; }
        public decimal? Retail_Price { get; set; }
    }


public class StringOrNumberConverter : JsonConverter<string?>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            if (reader.TokenType == JsonTokenType.Number)
            {
                return reader.GetInt64().ToString();  
            }

            return reader.GetString();   // Trường hợp bình thường là string
        }

        public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
        {
            if (value == null)
                writer.WriteNullValue();
            else
                writer.WriteStringValue(value);
        }
    }
}
