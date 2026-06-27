using API_PanCake.Models;
using API_PanCake.Services;
using Microsoft.AspNetCore.Mvc;

namespace API_PanCake.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductController : ControllerBase
{
    private readonly IPanCakeService _panCakeService;
    private readonly ILogger<ProductController> _logger;

    public ProductController(IPanCakeService panCakeService, ILogger<ProductController> logger)
    {
        _panCakeService = panCakeService;
        _logger = logger;
    }

    /// <summary>
    /// Tạo sản phẩm mới
    /// </summary>
    /// <param name="apiKey">API Key để authenticate với PanCake (tự động lấy Shop ID)</param>
    /// <param name="request">Thông tin sản phẩm cần tạo</param>
    /// <returns></returns>
    /// <remarks>
    /// Các trường BẮT BUỘC:
    /// - Name (string): Tên sản phẩm
    /// - Weight (int): Trọng lượng sản phẩm
    /// 
    /// LƯU Ý: Chỉ cần truyền API Key, hệ thống sẽ tự động lấy Shop ID
    /// 
    /// Ví dụ request:
    /// 
    ///     POST /api/Product/create?apiKey=your_api_key
    ///     {
    ///         "name": "Áo sơ mi nam",
    ///         "weight": 100,
    ///         "categoryIds": [1290021044, 201250699],
    ///         "noteProduct": "Ghi chú sản phẩm",
    ///         "productAttributes": [
    ///             {
    ///                 "name": "Màu",
    ///                 "values": ["Đen", "Trắng", "Đỏ"]
    ///             },
    ///             {
    ///                 "name": "Size",
    ///                 "values": ["S", "M", "L"]
    ///             }
    ///         ],
    ///         "tags": [193, 51],
    ///         "variations": [
    ///             {
    ///                 "fields": [
    ///                     {"name": "Màu", "value": "Trắng"},
    ///                     {"name": "Size", "value": "M"}
    ///                 ],
    ///                 "images": ["https://example.com/image.jpg"],
    ///                 "lastImportedPrice": 30000,
    ///                 "retailPrice": 140000,
    ///                 "barcode": "BARCODE123",
    ///                 "customId": "VCUSTOMID"
    ///             }
    ///         ],
    ///         "customId": "PCUSTOMID",
    ///         "isPublished": true
    ///     }
    /// </remarks>
    [HttpPost("create")]
    [ProducesResponseType(typeof(PanCakeApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateProduct(
        [FromQuery] string apiKey,
        [FromBody] CreateProductRequest request)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            return BadRequest(new { success = false, message = "API Key là bắt buộc" });
        }

        _logger.LogInformation("Creating product with API Key");
        
        var result = await _panCakeService.CreateProductAsync(request, apiKey);
        
        if (result.Success)
        {
            return Ok(result);
        }
        else
        {
            return BadRequest(result);
        }
    }

    /// <summary>
    /// Cập nhật sản phẩm
    /// </summary>
    /// <param name="productId">ID của sản phẩm cần cập nhật</param>
    /// <param name="apiKey">API Key để authenticate với PanCake (tự động lấy Shop ID)</param>
    /// <param name="request">Thông tin sản phẩm cần cập nhật (chỉ truyền các trường cần thay đổi)</param>
    /// <returns></returns>
    /// <remarks>
    /// LƯU Ý: 
    /// - Chỉ cần truyền API Key, hệ thống sẽ tự động lấy Shop ID
    /// - Với endpoint Update thì KHÔNG cần truyền đầy đủ tất cả trường
    /// - Chỉ truyền các trường bạn muốn cập nhật
    /// 
    /// Ví dụ request:
    /// 
    ///     PUT /api/Product/update?productId=3487e126-b0d9-4dae-89a9-ee60bef2f4e9&amp;apiKey=your_api_key
    ///     {
    ///         "name": "Áo sơ mi nam - Updated",
    ///         "retailPrice": 150000,
    ///         "variations": [
    ///             {
    ///                 "id": "3c3aca82-030e-4708-a75b-b2eaf538a2f5",
    ///                 "retailPrice": 150000
    ///             }
    ///         ]
    ///     }
    /// </remarks>
    [HttpPut("update")]
    [ProducesResponseType(typeof(PanCakeApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateProduct(
        [FromQuery] string productId,
        [FromQuery] string apiKey,
        [FromBody] UpdateProductRequest request)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            return BadRequest(new { success = false, message = "API Key là bắt buộc" });
        }

        if (string.IsNullOrEmpty(productId))
        {
            return BadRequest(new { success = false, message = "Product ID là bắt buộc" });
        }

        _logger.LogInformation("Updating product {ProductId}", productId);
        
        var result = await _panCakeService.UpdateProductAsync(productId, request, apiKey);
        
        if (result.Success)
        {
            return Ok(result);
        }
        else
        {
            return BadRequest(result);
        }
    }
}
