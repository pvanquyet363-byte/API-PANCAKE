using API_PanCake.Helpers;
using API_PanCake.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace API_PanCake.Services;

public class PanCakeService : IPanCakeService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PanCakeService> _logger;
    private const string BaseUrl = "https://pos.pages.fm/api/v1";

    public PanCakeService(HttpClient httpClient, ILogger<PanCakeService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<int?> GetShopIdAsync(string apiKey)
    {
        try
        {
            var url = $"{BaseUrl}/shops?api_key={apiKey}";
            _logger.LogInformation("Getting shop ID with API Key");

            var response = await _httpClient.GetAsync(url);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = new SnakeCaseNamingPolicy()
                };
                
                var shopsResponse = JsonSerializer.Deserialize<ShopsResponse>(responseContent, options);
                
                if (shopsResponse?.Success == true && shopsResponse.Shops?.Count > 0)
                {
                    var shopId = shopsResponse.Shops[0].Id;
                    _logger.LogInformation("Found shop ID: {ShopId}, Shop Name: {ShopName}", 
                        shopId, shopsResponse.Shops[0].Name);
                    return shopId;
                }
                else
                {
                    _logger.LogWarning("No shops found for this API Key");
                    return null;
                }
            }
            else
            {
                _logger.LogError("Failed to get shop ID: {StatusCode} - {Response}", 
                    response.StatusCode, responseContent);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception when getting shop ID");
            return null;
        }
    }

    public async Task<PanCakeApiResponse<object>> CreateProductAsync(CreateProductRequest request, string apiKey)
    {
        try
        {
            // Lấy Shop ID từ API Key
            var shopId = await GetShopIdAsync(apiKey);
            if (!shopId.HasValue)
            {
                return new PanCakeApiResponse<object>
                {
                    Success = false,
                    Message = "Không thể lấy Shop ID từ API Key. Vui lòng kiểm tra lại API Key."
                };
            }

            // Convert request sang format theo API PanCake
            var payload = new
            {
                product = ConvertToApiFormat(request)
            };

            var url = $"{BaseUrl}/shops/{shopId.Value}/products?api_key={apiKey}";
            
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = new SnakeCaseNamingPolicy(),
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            var jsonContent = JsonSerializer.Serialize(payload, options);
            _logger.LogInformation("Creating product for shop {ShopId}. Request: {Request}", shopId, jsonContent);

            var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, content);

            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("PanCake API Response: {Response}", responseContent);

            if (response.IsSuccessStatusCode)
            {
                return new PanCakeApiResponse<object>
                {
                    Success = true,
                    Data = JsonSerializer.Deserialize<object>(responseContent),
                    Message = "Tạo sản phẩm thành công"
                };
            }
            else
            {
                return new PanCakeApiResponse<object>
                {
                    Success = false,
                    Message = $"Lỗi khi tạo sản phẩm: {response.StatusCode} - {responseContent}"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception khi tạo sản phẩm");
            return new PanCakeApiResponse<object>
            {
                Success = false,
                Message = $"Exception: {ex.Message}"
            };
        }
    }

    public async Task<PanCakeApiResponse<object>> UpdateProductAsync(string productId, UpdateProductRequest request, string apiKey)
    {
        try
        {
            // Lấy Shop ID từ API Key
            var shopId = await GetShopIdAsync(apiKey);
            if (!shopId.HasValue)
            {
                return new PanCakeApiResponse<object>
                {
                    Success = false,
                    Message = "Không thể lấy Shop ID từ API Key. Vui lòng kiểm tra lại API Key."
                };
            }

            // Convert request sang format theo API PanCake
            var payload = new
            {
                product = ConvertToApiFormat(request)
            };

            var url = $"{BaseUrl}/shops/{shopId.Value}/products/{productId}?api_key={apiKey}";
            
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = new SnakeCaseNamingPolicy(),
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            var jsonContent = JsonSerializer.Serialize(payload, options);
            _logger.LogInformation("Updating product {ProductId} for shop {ShopId}. Request: {Request}", productId, shopId, jsonContent);

            var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PutAsync(url, content);

            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("PanCake API Response: {Response}", responseContent);

            if (response.IsSuccessStatusCode)
            {
                return new PanCakeApiResponse<object>
                {
                    Success = true,
                    Data = JsonSerializer.Deserialize<object>(responseContent),
                    Message = "Cập nhật sản phẩm thành công"
                };
            }
            else
            {
                return new PanCakeApiResponse<object>
                {
                    Success = false,
                    Message = $"Lỗi khi cập nhật sản phẩm: {response.StatusCode} - {responseContent}"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception khi cập nhật sản phẩm");
            return new PanCakeApiResponse<object>
            {
                Success = false,
                Message = $"Exception: {ex.Message}"
            };
        }
    }

    // Helper method để convert sang format snake_case cho API PanCake
    private static object ConvertToApiFormat(object request)
    {
        if (request is CreateProductRequest createRequest)
        {
            var result = new Dictionary<string, object?>
            {
                ["name"] = createRequest.Name,
                ["weight"] = createRequest.Weight
            };

            if (createRequest.MeasureGroupId.HasValue)
                result["measure_group_id"] = createRequest.MeasureGroupId.Value;
            
            if (createRequest.CategoryIds?.Any() == true)
                result["category_ids"] = createRequest.CategoryIds;
            
            if (!string.IsNullOrEmpty(createRequest.Note))
                result["note"] = createRequest.Note;
            
            if (!string.IsNullOrEmpty(createRequest.NoteProduct))
                result["note_product"] = createRequest.NoteProduct;
            
            if (createRequest.ProductAttributes?.Any() == true)
                result["product_attributes"] = createRequest.ProductAttributes.Select(attr => new
                {
                    name = attr.Name,
                    values = attr.Values
                });
            
            if (createRequest.Tags?.Any() == true)
                result["tags"] = createRequest.Tags;
            
            if (!string.IsNullOrEmpty(createRequest.CustomId))
                result["custom_id"] = createRequest.CustomId;
            
            if (createRequest.IsPublished.HasValue)
                result["is_published"] = createRequest.IsPublished.Value;
            
            if (createRequest.Variations?.Any() == true)
                result["variations"] = createRequest.Variations.Select(ConvertVariationToApiFormat);
            
            if (createRequest.VariationsWarehouses?.Any() == true)
                result["variations_warehouses"] = createRequest.VariationsWarehouses.Select(vw => new
                {
                    remain_quantity = vw.RemainQuantity,
                    warehouse_id = vw.WarehouseId,
                    batch_position = vw.BatchPosition,
                    shelf_position = vw.ShelfPosition
                });
            
            if (createRequest.Materials?.Any() == true)
                result["materials"] = createRequest.Materials;
            
            if (!string.IsNullOrEmpty(createRequest.Keyword))
                result["keyword"] = createRequest.Keyword;

            return result;
        }
        else if (request is UpdateProductRequest updateRequest)
        {
            var result = new Dictionary<string, object?>();

            if (!string.IsNullOrEmpty(updateRequest.Name))
                result["name"] = updateRequest.Name;
            
            if (updateRequest.Weight.HasValue)
                result["weight"] = updateRequest.Weight.Value;
            
            if (updateRequest.MeasureGroupId.HasValue)
                result["measure_group_id"] = updateRequest.MeasureGroupId.Value;
            
            if (updateRequest.CategoryIds?.Any() == true)
                result["category_ids"] = updateRequest.CategoryIds;
            
            if (!string.IsNullOrEmpty(updateRequest.Note))
                result["note"] = updateRequest.Note;
            
            if (!string.IsNullOrEmpty(updateRequest.NoteProduct))
                result["note_product"] = updateRequest.NoteProduct;
            
            if (updateRequest.ProductAttributes?.Any() == true)
                result["product_attributes"] = updateRequest.ProductAttributes.Select(attr => new
                {
                    name = attr.Name,
                    values = attr.Values
                });
            
            if (updateRequest.Tags?.Any() == true)
                result["tags"] = updateRequest.Tags;
            
            if (!string.IsNullOrEmpty(updateRequest.CustomId))
                result["custom_id"] = updateRequest.CustomId;
            
            if (updateRequest.IsPublished.HasValue)
                result["is_published"] = updateRequest.IsPublished.Value;
            
            if (updateRequest.Variations?.Any() == true)
                result["variations"] = updateRequest.Variations.Select(ConvertVariationToApiFormat);
            
            if (updateRequest.VariationsWarehouses?.Any() == true)
                result["variations_warehouses"] = updateRequest.VariationsWarehouses.Select(vw => new
                {
                    remain_quantity = vw.RemainQuantity,
                    warehouse_id = vw.WarehouseId,
                    batch_position = vw.BatchPosition,
                    shelf_position = vw.ShelfPosition
                });
            
            if (updateRequest.Materials?.Any() == true)
                result["materials"] = updateRequest.Materials;
            
            if (!string.IsNullOrEmpty(updateRequest.Keyword))
                result["keyword"] = updateRequest.Keyword;

            return result;
        }

        return request;
    }

    private static object ConvertVariationToApiFormat(ProductVariation variation)
    {
        var result = new Dictionary<string, object?>();

        if (!string.IsNullOrEmpty(variation.Id))
            result["id"] = variation.Id;
        
        if (variation.Fields?.Any() == true)
            result["fields"] = variation.Fields.Select(f => new
            {
                name = f.Name,
                value = f.Value
            });
        
        if (variation.Images?.Any() == true)
            result["images"] = variation.Images;
        
        if (variation.LastImportedPrice.HasValue)
            result["last_imported_price"] = variation.LastImportedPrice.Value;
        
        if (variation.RetailPrice.HasValue)
            result["retail_price"] = variation.RetailPrice.Value;
        
        if (variation.PriceAtCounter.HasValue)
            result["price_at_counter"] = variation.PriceAtCounter.Value;
        
        if (variation.Weight.HasValue)
            result["weight"] = variation.Weight.Value;
        
        if (!string.IsNullOrEmpty(variation.Barcode))
            result["barcode"] = variation.Barcode;
        
        if (!string.IsNullOrEmpty(variation.CustomId))
            result["custom_id"] = variation.CustomId;
        
        if (variation.IsHidden.HasValue)
            result["is_hidden"] = variation.IsHidden.Value;
        
        if (variation.VariationsWarehouses?.Any() == true)
            result["variations_warehouses"] = variation.VariationsWarehouses.Select(vw => new
            {
                remain_quantity = vw.RemainQuantity,
                warehouse_id = vw.WarehouseId,
                batch_position = vw.BatchPosition,
                shelf_position = vw.ShelfPosition
            });

        return result;
    }
}
