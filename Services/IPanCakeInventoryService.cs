using API_PanCake.Models;

namespace API_PanCake.Services;

/// <summary>
/// Interface cho service đồng bộ tồn kho lên PanCake
/// </summary>
public interface IPanCakeInventoryService
{
    /// <summary>
    /// Đồng bộ tồn kho lên PanCake
    /// </summary>
    /// <param name="request">Thông tin đồng bộ (ShopId, MaVt, VariationsWarehouses)</param>
    /// <param name="apiKey">API Key của shop trên PanCake</param>
    /// <returns>Kết quả đồng bộ</returns>
    Task<InventorySyncResult> SyncInventoryAsync(InventorySyncRequest request, string apiKey);
    
    /// <summary>
    /// Tìm kiếm product variation theo mã vật tư
    /// </summary>
    /// <param name="shopId">Shop ID</param>
    /// <param name="maVt">Mã vật tư</param>
    /// <param name="apiKey">API Key</param>
    /// <returns>Thông tin product variation</returns>
    Task<ProductVariationInfo?> SearchProductVariationByMaVtAsync(long shopId, string maVt, string apiKey);
}
