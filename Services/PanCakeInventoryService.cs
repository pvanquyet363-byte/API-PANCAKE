using API_PanCake.Helpers;
using API_PanCake.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace API_PanCake.Services;

/// <summary>
/// Service để đồng bộ tồn kho từ ERP lên PanCake
/// </summary>
public class PanCakeInventoryService : IPanCakeInventoryService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PanCakeInventoryService> _logger;
    private const string BaseUrl = "https://pos.pages.fm/api/v1";

    public PanCakeInventoryService(HttpClient httpClient, ILogger<PanCakeInventoryService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Đồng bộ tồn kho lên PanCake
    /// Bước 1: Tìm product variation theo ma_vt
    /// Bước 2: Lấy warehouse IDs từ thông tin sản phẩm
    /// Bước 3: Cập nhật quantity cho tất cả warehouses
    /// </summary>
    public async Task<InventorySyncResult> SyncInventoryAsync(InventorySyncRequest request, string apiKey)
    {
        try
        {
            _logger.LogInformation("Starting inventory sync for ShopId={ShopId}, MaVt={MaVt}, Quantity={Quantity}", 
                request.ShopId, request.MaVt, request.RemainQuantity);

            // Bước 1: Tìm product variation theo ma_vt
            var productVariation = await SearchProductVariationByMaVtAsync(
                request.ShopId, 
                request.MaVt, 
                apiKey);

            if (productVariation == null)
            {
                var errorMsg = $"Không tìm thấy sản phẩm với mã vật tư '{request.MaVt}' trên PanCake";
                _logger.LogWarning(errorMsg);
                return new InventorySyncResult
                {
                    Success = false,
                    Message = errorMsg,
                    MaVt = request.MaVt
                };
            }

            var variationId = productVariation.Id;
            if (string.IsNullOrEmpty(variationId))
            {
                var errorMsg = $"Variation ID không hợp lệ cho mã vật tư '{request.MaVt}'";
                _logger.LogError(errorMsg);
                return new InventorySyncResult
                {
                    Success = false,
                    Message = errorMsg,
                    MaVt = request.MaVt
                };
            }

            _logger.LogInformation("Found product variation: VariationId={VariationId}, ProductId={ProductId}, Name={Name}", 
                variationId, productVariation.ProductId, productVariation.Product?.Name ?? "Unknown");

            // Bước 2: Lấy warehouse IDs từ thông tin sản phẩm hoặc lấy kho mặc định
            List<InventoryWarehouseItem> warehouseItems;
            
            if (productVariation.VariationsWarehouses == null || productVariation.VariationsWarehouses.Count == 0)
            {
                _logger.LogWarning("Sản phẩm '{MaVt}' không có thông tin kho hàng, đang lấy kho mặc định của shop...", request.MaVt);
                
                // Lấy danh sách kho của shop
                var shopWarehouses = await GetShopWarehousesAsync(request.ShopId, apiKey);
                
                if (shopWarehouses == null || shopWarehouses.Count == 0)
                {
                    var errorMsg = $"Không tìm thấy kho hàng nào cho shop ID {request.ShopId}";
                    _logger.LogError(errorMsg);
                    return new InventorySyncResult
                    {
                        Success = false,
                        Message = errorMsg,
                        ProductId = productVariation.ProductId,
                        VariationId = variationId,
                        MaVt = request.MaVt
                    };
                }
                
                // Lấy kho đầu tiên làm kho mặc định
                var defaultWarehouse = shopWarehouses.First();
                if (string.IsNullOrEmpty(defaultWarehouse.Id))
                {
                    var errorMsg = $"Kho mặc định của shop {request.ShopId} không có ID hợp lệ";
                    _logger.LogError(errorMsg);
                    return new InventorySyncResult
                    {
                        Success = false,
                        Message = errorMsg,
                        ProductId = productVariation.ProductId,
                        VariationId = variationId,
                        MaVt = request.MaVt
                    };
                }
                
                _logger.LogInformation("✅ Sử dụng kho mặc định: ID={WarehouseId}, Name={WarehouseName}", 
                    defaultWarehouse.Id, defaultWarehouse.Name ?? "Unknown");
                
                warehouseItems = new List<InventoryWarehouseItem>
                {
                    new InventoryWarehouseItem
                    {
                        WarehouseId = defaultWarehouse.Id,
                        RemainQuantity = request.RemainQuantity
                    }
                };
            }
            else
            {
                // Tạo danh sách warehouse items với số lượng tồn từ request
                warehouseItems = productVariation.VariationsWarehouses
                    .Where(w => !string.IsNullOrEmpty(w.WarehouseId))
                    .Select(w => new InventoryWarehouseItem
                    {
                        WarehouseId = w.WarehouseId!,
                        RemainQuantity = request.RemainQuantity
                    })
                    .ToList();

                if (warehouseItems.Count == 0)
                {
                    _logger.LogWarning("Sản phẩm '{MaVt}' có thông tin kho nhưng không có warehouse ID hợp lệ, đang lấy kho mặc định...", request.MaVt);
                    
                    // Lấy danh sách kho của shop
                    var shopWarehouses = await GetShopWarehousesAsync(request.ShopId, apiKey);
                    
                    if (shopWarehouses == null || shopWarehouses.Count == 0 || string.IsNullOrEmpty(shopWarehouses.First().Id))
                    {
                        var errorMsg = $"Không tìm thấy kho hàng mặc định hợp lệ cho shop ID {request.ShopId}";
                        _logger.LogError(errorMsg);
                        return new InventorySyncResult
                        {
                            Success = false,
                            Message = errorMsg,
                            ProductId = productVariation.ProductId,
                            VariationId = variationId,
                            MaVt = request.MaVt
                        };
                    }
                    
                    var defaultWarehouse = shopWarehouses.First();
                    _logger.LogInformation("✅ Sử dụng kho mặc định: ID={WarehouseId}, Name={WarehouseName}", 
                        defaultWarehouse.Id, defaultWarehouse.Name ?? "Unknown");
                    
                    warehouseItems = new List<InventoryWarehouseItem>
                    {
                        new InventoryWarehouseItem
                        {
                            WarehouseId = defaultWarehouse.Id!,
                            RemainQuantity = request.RemainQuantity
                        }
                    };
                }
                else
                {
                    _logger.LogInformation("Sử dụng {Count} kho từ thông tin sản phẩm: {Warehouses}", 
                        warehouseItems.Count, 
                        string.Join(", ", productVariation.VariationsWarehouses.Select(w => w.WarehouseId ?? "Unknown")));
                }
            }

            // Bước 3: Cập nhật quantity cho tất cả warehouses
            var updateResult = await UpdateQuantityAsync(
                request.ShopId,
                variationId,
                warehouseItems,
                apiKey);

            if (updateResult)
            {
                var totalQty = warehouseItems.Count * request.RemainQuantity;
                _logger.LogInformation("Successfully synced inventory for MaVt={MaVt}, Warehouses={Count}, Quantity={Quantity}", 
                    request.MaVt, warehouseItems.Count, request.RemainQuantity);
                
                return new InventorySyncResult
                {
                    Success = true,
                    Message = $"Đồng bộ tồn kho thành công cho {warehouseItems.Count} kho",
                    ProductId = productVariation.ProductId,
                    VariationId = variationId,
                    MaVt = request.MaVt,
                    TotalQuantityUpdated = request.RemainQuantity
                };
            }
            else
            {
                var errorMsg = "Cập nhật số lượng tồn thất bại";
                _logger.LogError(errorMsg);
                return new InventorySyncResult
                {
                    Success = false,
                    Message = errorMsg,
                    ProductId = productVariation.ProductId,
                    VariationId = variationId,
                    MaVt = request.MaVt
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during inventory sync for MaVt={MaVt}", request.MaVt);
            return new InventorySyncResult
            {
                Success = false,
                Message = $"Lỗi: {ex.Message}",
                MaVt = request.MaVt
            };
        }
    }

    /// <summary>
    /// Tìm kiếm product variation theo mã vật tư (search trong custom_id hoặc barcode)
    /// API: GET /api/v1/shops/{SHOP_ID}/products/variations?api_key={API_KEY}&amp;search={ma_vt}
    /// </summary>
    public async Task<ProductVariationInfo?> SearchProductVariationByMaVtAsync(
        long shopId, 
        string maVt, 
        string apiKey)
    {
        try
        {
            var url = $"{BaseUrl}/shops/{shopId}/products/variations?api_key={apiKey}&search={Uri.EscapeDataString(maVt)}";
            _logger.LogInformation("Searching product variation: URL={Url}", url);

            var response = await _httpClient.GetAsync(url);
            var responseContent = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("Search response: StatusCode={StatusCode}, Content={Content}", 
                response.StatusCode, responseContent);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to search product variation: {StatusCode} - {Response}", 
                    response.StatusCode, responseContent);
                return null;
            }

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = new SnakeCaseNamingPolicy(),
                PropertyNameCaseInsensitive = true
            };

            var searchResponse = JsonSerializer.Deserialize<ProductVariationSearchResponse>(responseContent, options);

            if (searchResponse?.Success == true && 
                searchResponse.Data != null && 
                searchResponse.Data.Count > 0)
            {
                // Trả về kết quả đầu tiên tìm được
                var firstResult = searchResponse.Data[0];
                var productName = firstResult.Product?.Name ?? firstResult.DisplayId ?? "Unknown";
                _logger.LogInformation("Found {Count} product variation(s), using first one: {Name}", 
                    searchResponse.Data.Count, productName);
                
                // Debug: Log warehouse info
                _logger.LogInformation("DEBUG: VariationsWarehouses count: {Count}", 
                    firstResult.VariationsWarehouses?.Count ?? 0);
                if (firstResult.VariationsWarehouses != null)
                {
                    foreach (var wh in firstResult.VariationsWarehouses)
                    {
                        _logger.LogInformation("DEBUG: WarehouseId={WarehouseId}, RemainQuantity={Qty}", 
                            wh.WarehouseId, wh.RemainQuantity);
                    }
                }
                
                return firstResult;
            }

            _logger.LogWarning("No product variation found for MaVt={MaVt}", maVt);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception when searching product variation for MaVt={MaVt}", maVt);
            return null;
        }
    }

    /// <summary>
    /// Lấy danh sách kho của shop
    /// API: GET /api/v1/shops/{SHOP_ID}/warehouses?api_key={API_KEY}
    /// </summary>
    private async Task<List<WarehouseInfo>?> GetShopWarehousesAsync(long shopId, string apiKey)
    {
        try
        {
            var url = $"{BaseUrl}/shops/{shopId}/warehouses?api_key={apiKey}";
            _logger.LogInformation("Getting shop warehouses: URL={Url}", url);

            var response = await _httpClient.GetAsync(url);
            var responseContent = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("Get warehouses response: StatusCode={StatusCode}, Content={Content}", 
                response.StatusCode, responseContent);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get warehouses: {StatusCode} - {Response}", 
                    response.StatusCode, responseContent);
                return null;
            }

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = new SnakeCaseNamingPolicy(),
                PropertyNameCaseInsensitive = true
            };

            var warehousesResponse = JsonSerializer.Deserialize<WarehousesResponse>(responseContent, options);

            if (warehousesResponse?.Success == true && warehousesResponse.Data != null)
            {
                _logger.LogInformation("Found {Count} warehouse(s) for shop {ShopId}", 
                    warehousesResponse.Data.Count, shopId);
                return warehousesResponse.Data;
            }

            _logger.LogWarning("No warehouses found for shop {ShopId}", shopId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception when getting warehouses for shop {ShopId}", shopId);
            return null;
        }
    }

    /// <summary>
    /// Cập nhật số lượng tồn kho cho variation
    /// API: POST /api/v1/shops/{SHOP_ID}/variations/{VARIATION_ID}/update_quantity?api_key={API_KEY}
    /// </summary>
    private async Task<bool> UpdateQuantityAsync(
        long shopId,
        string variationId,
        List<InventoryWarehouseItem> warehouseItems,
        string apiKey)
    {
        try
        {
            var url = $"{BaseUrl}/shops/{shopId}/variations/{variationId}/update_quantity?api_key={apiKey}";
            
            var updateRequest = new UpdateQuantityRequest
            {
                VariationsWarehouses = warehouseItems
            };

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = new SnakeCaseNamingPolicy(),
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            var jsonContent = JsonSerializer.Serialize(updateRequest, options);
            _logger.LogInformation("Updating quantity: URL={Url}, Request={Request}", url, jsonContent);

            var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("Update quantity response: StatusCode={StatusCode}, Content={Content}", 
                response.StatusCode, responseContent);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to update quantity: {StatusCode} - {Response}", 
                    response.StatusCode, responseContent);
                return false;
            }

            var updateResponse = JsonSerializer.Deserialize<UpdateQuantityResponse>(responseContent, options);
            return updateResponse?.Success == true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception when updating quantity for VariationId={VariationId}", variationId);
            return false;
        }
    }
}
