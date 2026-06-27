using API_PanCake.Models;

namespace API_PanCake.Services;

public interface IPanCakeService
{
    /// <summary>
    /// Lấy Shop ID từ API Key
    /// </summary>
    /// <param name="apiKey">API Key</param>
    /// <returns>Shop ID (lấy shop đầu tiên nếu có nhiều shop)</returns>
    Task<int?> GetShopIdAsync(string apiKey);
    
    /// <summary>
    /// Tạo sản phẩm mới
    /// </summary>
    /// <param name="request">Thông tin sản phẩm cần tạo</param>
    /// <param name="apiKey">API Key để authenticate</param>
    /// <returns></returns>
    Task<PanCakeApiResponse<object>> CreateProductAsync(CreateProductRequest request, string apiKey);
    
    /// <summary>
    /// Cập nhật sản phẩm
    /// </summary>
    /// <param name="productId">ID của sản phẩm cần cập nhật</param>
    /// <param name="request">Thông tin sản phẩm cần cập nhật</param>
    /// <param name="apiKey">API Key để authenticate</param>
    /// <returns></returns>
    Task<PanCakeApiResponse<object>> UpdateProductAsync(string productId, UpdateProductRequest request, string apiKey);
}
