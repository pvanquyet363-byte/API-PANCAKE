using API_PanCake.Services;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace API_PanCake.Models;

/// <summary>
/// Request model để tạo sản phẩm
/// </summary>
public class CreateProductRequest
{
    /// <summary>
    /// BẮT BUỘC: Tên sản phẩm
    /// </summary>
    [Required(ErrorMessage = "Tên sản phẩm là bắt buộc")]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// BẮT BUỘC: Trọng lượng sản phẩm
    /// </summary>
    [Required(ErrorMessage = "Trọng lượng sản phẩm là bắt buộc")]
    public int Weight { get; set; }
    
    /// <summary>
    /// Mã nhóm đơn vị tính (ví dụ: 58)
    /// </summary>
    public int? MeasureGroupId { get; set; }
    
    /// <summary>
    /// Danh sách mã danh mục
    /// </summary>
    public List<int>? CategoryIds { get; set; }
    
    /// <summary>
    /// Ghi chú nội bộ
    /// </summary>
    public string? Note { get; set; }
    
    /// <summary>
    /// Ghi chú sản phẩm
    /// </summary>
    public string? NoteProduct { get; set; }
    
    /// <summary>
    /// Thuộc tính sản phẩm (Màu, Size, v.v.)
    /// </summary>
    public List<ProductAttribute>? ProductAttributes { get; set; }
    
    /// <summary>
    /// Danh sách mã thẻ
    /// </summary>
    public List<int>? Tags { get; set; }
    
    /// <summary>
    /// Mã tùy chỉnh sản phẩm
    /// </summary>
    public string? CustomId { get; set; }
    
    /// <summary>
    /// Trạng thái xuất bản (true/false)
    /// </summary>
    public bool? IsPublished { get; set; }
    
    /// <summary>
    /// Danh sách mẫu mã/biến thể
    /// </summary>
    public List<ProductVariation>? Variations { get; set; }
    
    /// <summary>
    /// Thông tin tồn kho
    /// </summary>
    public List<VariationWarehouse>? VariationsWarehouses { get; set; }
    
    /// <summary>
    /// Danh sách mã nguyên liệu
    /// </summary>
    public List<int>? Materials { get; set; }
    
    /// <summary>
    /// Từ khóa tìm kiếm
    /// </summary>
    public string? Keyword { get; set; }
}

/// <summary>
/// Request model để cập nhật sản phẩm (các trường giống CreateProductRequest nhưng không required)
/// </summary>
public class UpdateProductRequest
{
    public string? Name { get; set; }
    public int? Weight { get; set; }
    public int? MeasureGroupId { get; set; }
    public List<int>? CategoryIds { get; set; }
    public string? Note { get; set; }
    public string? NoteProduct { get; set; }
    public List<ProductAttribute>? ProductAttributes { get; set; }
    public List<int>? Tags { get; set; }
    public string? CustomId { get; set; }
    public bool? IsPublished { get; set; }
    public List<ProductVariation>? Variations { get; set; }
    public List<VariationWarehouse>? VariationsWarehouses { get; set; }
    public List<int>? Materials { get; set; }
    public string? Keyword { get; set; }
}

/// <summary>
/// Thuộc tính sản phẩm (Màu, Size, v.v.)
/// </summary>
public class ProductAttribute
{
    /// <summary>
    /// Tên thuộc tính (ví dụ: "Màu")
    /// </summary>
    [Required]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Các giá trị của thuộc tính (ví dụ: ["Đen", "Trắng", "Đỏ"])
    /// </summary>
    [Required]
    public List<string> Values { get; set; } = new();
}

/// <summary>
/// Mẫu mã/Biến thể sản phẩm
/// </summary>
public class ProductVariation
{
    /// <summary>
    /// ID mẫu mã (dùng khi update)
    /// </summary>
    public string? Id { get; set; }
    
    /// <summary>
    /// Các trường thuộc tính (Màu: Đen, Size: M)
    /// </summary>
    public List<VariationField>? Fields { get; set; }
    
    /// <summary>
    /// Hình ảnh mẫu mã (URLs)
    /// </summary>
    public List<string>? Images { get; set; }
    
    /// <summary>
    /// Giá nhập
    /// </summary>
    public int? LastImportedPrice { get; set; }
    
    /// <summary>
    /// Giá bán lẻ
    /// </summary>
    public int? RetailPrice { get; set; }
    
    /// <summary>
    /// Giá tại quầy
    /// </summary>
    public int? PriceAtCounter { get; set; }
    
    /// <summary>
    /// Khối lượng
    /// </summary>
    public int? Weight { get; set; }
    
    /// <summary>
    /// Mã barcode
    /// </summary>
    public string? Barcode { get; set; }
    
    /// <summary>
    /// Mã tùy chỉnh
    /// </summary>
    public string? CustomId { get; set; }
    
    /// <summary>
    /// Trạng thái ẩn/hiện
    /// </summary>
    public bool? IsHidden { get; set; }
    
    /// <summary>
    /// Thông tin tồn kho cho mẫu mã này
    /// </summary>
    public List<VariationWarehouse>? VariationsWarehouses { get; set; }
}

/// <summary>
/// Trường thuộc tính của mẫu mã
/// </summary>
public class VariationField
{
    /// <summary>
    /// Tên trường (ví dụ: "Màu")
    /// </summary>
    [Required]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Giá trị (ví dụ: "Đen")
    /// </summary>
    [Required]
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// Thông tin tồn kho
/// </summary>
public class VariationWarehouse
{
    /// <summary>
    /// Số lượng tồn
    /// </summary>
    public int RemainQuantity { get; set; }
    
    /// <summary>
    /// Mã kho
    /// </summary>
    [Required]
    public string WarehouseId { get; set; } = string.Empty;
    
    /// <summary>
    /// Vị trí lô
    /// </summary>
    public string? BatchPosition { get; set; }
    
    /// <summary>
    /// Vị trí kệ
    /// </summary>
    public string? ShelfPosition { get; set; }
}

/// <summary>
/// Response wrapper cho API PanCake
/// </summary>
public class PanCakeApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Message { get; set; }
}

/// <summary>
/// Response từ API GET /shops
/// </summary>
public class ShopsResponse
{
    public bool Success { get; set; }
    public List<ShopInfo>? Shops { get; set; }
}

/// <summary>
/// Thông tin shop
/// </summary>
public class ShopInfo
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? AvatarUrl { get; set; }
}

// ==================== WEBHOOK MODELS ====================

/// <summary>
/// Model nhận webhook từ PanCake
/// </summary>
public class PanCakeWebhookRequest
{
    /// <summary>
    /// Loại sự kiện (order.created, order.updated, order.confirmed, order.cancelled, etc.)
    /// </summary>
    public string? Event { get; set; }
    
    /// <summary>
    /// Dữ liệu đơn hàng
    /// </summary>
    public WebhookOrderData? Data { get; set; }
    
    /// <summary>
    /// Chữ ký để xác thực webhook
    /// </summary>
    public string? Signature { get; set; }
    
    /// <summary>
    /// Timestamp
    /// </summary>
    public long? Timestamp { get; set; }
}

/// <summary>
/// Dữ liệu đơn hàng từ webhook
/// </summary>
public class WebhookOrderData
{
    /// <summary>
    /// ID đơn hàng
    /// </summary>
    public long? Id { get; set; }
    
    /// <summary>
    /// Mã đơn hàng (ORDER-XXXXXX)
    /// </summary>
    public string? Code { get; set; }
    
    /// <summary>
    /// ID cửa hàng
    /// </summary>
    public int? ShopId { get; set; }
    
    /// <summary>
    /// ID chi nhánh
    /// </summary>
    public int? BranchId { get; set; }
    
    /// <summary>
    /// Trạng thái đơn hàng
    /// </summary>
    public string? Status { get; set; }
    
    /// <summary>
    /// Tổng giá trị đơn hàng
    /// </summary>
    public decimal? TotalAmount { get; set; }
    
    /// <summary>
    /// Số tiền giảm giá
    /// </summary>
    public decimal? DiscountAmount { get; set; }
    
    /// <summary>
    /// Số tiền khách phải trả
    /// </summary>
    public decimal? FinalAmount { get; set; }
    
    /// <summary>
    /// Phương thức thanh toán
    /// </summary>
    public string? PaymentMethod { get; set; }
    
    /// <summary>
    /// Trạng thái thanh toán
    /// </summary>
    public string? PaymentStatus { get; set; }
    
    /// <summary>
    /// Thông tin khách hàng
    /// </summary>
    public WebhookCustomer? Customer { get; set; }
    
    /// <summary>
    /// Danh sách sản phẩm trong đơn
    /// </summary>
    public List<WebhookOrderItem>? Items { get; set; }
    
    /// <summary>
    /// Địa chỉ giao hàng
    /// </summary>
    public WebhookShippingAddress? ShippingAddress { get; set; }
    
    /// <summary>
    /// Ghi chú đơn hàng
    /// </summary>
    public string? Note { get; set; }
    
    /// <summary>
    /// Ngày tạo đơn
    /// </summary>
    public DateTime? CreatedAt { get; set; }
    
    /// <summary>
    /// Ngày cập nhật
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Thông tin khách hàng trong webhook
/// </summary>
public class WebhookCustomer
{
    /// <summary>
    /// ID khách hàng
    /// </summary>
    public long? Id { get; set; }
    
    /// <summary>
    /// Tên khách hàng
    /// </summary>
    public string? Name { get; set; }
    
    /// <summary>
    /// Số điện thoại
    /// </summary>
    public string? Phone { get; set; }
    
    /// <summary>
    /// Email
    /// </summary>
    public string? Email { get; set; }
}

/// <summary>
/// Thông tin sản phẩm trong đơn hàng webhook
/// </summary>
public class WebhookOrderItem
{
    /// <summary>
    /// ID sản phẩm
    /// </summary>
    public long? ProductId { get; set; }
    
    /// <summary>
    /// Tên sản phẩm
    /// </summary>
    public string? ProductName { get; set; }
    
    /// <summary>
    /// ID variation/mẫu mã
    /// </summary>
    public long? VariationId { get; set; }
    
    /// <summary>
    /// Tên variation
    /// </summary>
    public string? VariationName { get; set; }
    
    /// <summary>
    /// SKU
    /// </summary>
    public string? Sku { get; set; }
    
    /// <summary>
    /// Số lượng
    /// </summary>
    public int? Quantity { get; set; }
    
    /// <summary>
    /// Đơn giá
    /// </summary>
    public decimal? Price { get; set; }
    
    /// <summary>
    /// Tổng tiền (Số lượng * Đơn giá)
    /// </summary>
    public decimal? TotalPrice { get; set; }
}

/// <summary>
/// Địa chỉ giao hàng
/// </summary>
public class WebhookShippingAddress
{
    /// <summary>
    /// Tên người nhận
    /// </summary>
    public string? ReceiverName { get; set; }
    
    /// <summary>
    /// Số điện thoại người nhận
    /// </summary>
    public string? ReceiverPhone { get; set; }
    
    /// <summary>
    /// Địa chỉ đầy đủ
    /// </summary>
    public string? FullAddress { get; set; }
    
    /// <summary>
    /// Tỉnh/Thành phố
    /// </summary>
    public string? Province { get; set; }
    
    /// <summary>
    /// Quận/Huyện
    /// </summary>
    public string? District { get; set; }
    
    /// <summary>
    /// Phường/Xã
    /// </summary>
    public string? Ward { get; set; }
}

/// <summary>
/// Model THỰC TẾ của webhook từ PanCake (flat structure)
/// </summary>
public class PanCakeOrderWebhook
{
    [JsonConverter(typeof(StringOrNumberConverter))]
    public string? Id { get; set; }
    
    public string? Event_Type { get; set; }  // snake_case từ PanCake
    
    public int? Shop_Id { get; set; }
    
    public int? Branch_Id { get; set; }
    
    public int? Status { get; set; }  // Mã trạng thái: 0, 1, 2, 3, 4, 8, 9, 11...
    
    public string? Status_Name { get; set; }  // Tên trạng thái: "new", "returning", "delivering", etc.
    
    public string? Order_Code { get; set; }
    
    public decimal? Total_Price { get; set; }
    
    public decimal? Total_Discount { get; set; }
    
    public decimal? Total_Price_After_Sub_Discount { get; set; }
    
    public string? Payment_Method { get; set; }
    
    public string? Payment_Status { get; set; }
    
    public PanCakeWebhookCustomer? Creator { get; set; }
    
    public PanCakeWebhookShippingAddress? Shipping_Address { get; set; }
    
    public List<PanCakeWebhookItem>? Items { get; set; }
    
    public string? Note { get; set; }
    
    public string? Inserted_At { get; set; }  // ISO datetime string
    
    public string? Updated_At { get; set; }
    
    public long? Timestamp { get; set; }
    
    public string? Signature { get; set; }
}

/// <summary>
/// Customer/Creator info từ webhook PanCake
/// </summary>
public class PanCakeWebhookCustomer
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Phone_Number { get; set; }
}

/// <summary>
/// Shipping address từ webhook PanCake
/// </summary>
public class PanCakeWebhookShippingAddress
{
    public string? Full_Name { get; set; }
    public string? Phone_Number { get; set; }
    public string? Full_Address { get; set; }
    public string? Address { get; set; }
    public string? Province_Name { get; set; }
    public string? District_Name { get; set; }
    public string? Commune_Name { get; set; }
}

/// <summary>
/// Order item từ webhook PanCake
/// </summary>
public class PanCakeWebhookItem
{
    public long? Id { get; set; }
    public string? Product_Id { get; set; }
    public string? Variation_Id { get; set; }
    public int? Quantity { get; set; }
    public decimal? Price { get; set; }
    public decimal? Total_Discount { get; set; }
    public PanCakeWebhookVariationInfo? Variation_Info { get; set; }
}

/// <summary>
/// Variation info từ webhook item
/// </summary>
public class PanCakeWebhookVariationInfo
{
    public string? Name { get; set; }
    public string? Display_Id { get; set; }
    public string? Barcode { get; set; }
    public decimal? Retail_Price { get; set; }
}

// ============================================
// CUSTOMER WEBHOOK MODELS
// ============================================

/// <summary>
/// Model THỰC TẾ của customer webhook từ PanCake (flat structure)
/// Nhận khi có sự kiện khách hàng: create, update, delete
/// </summary>
public class PanCakeCustomerWebhook
{
    public string? Id { get; set; }  // UUID format: "5fa7e409-c1d3-47f6-ba84-33adf5575a5f"
    
    public string? Event_Type { get; set; }  // "create", "update", "delete"
    
    public string? Type { get; set; }  // "customers"
    
    public long? Shop_Id { get; set; }
    
    public string? Name { get; set; }
    
    public List<string>? Phone_Numbers { get; set; }  // Array: ["0358244557"]
    
    public List<string>? Emails { get; set; }  // Array hoặc null
    
    public CustomerBirthday? Birthday { get; set; }  // Object: {"date":"08","month":"06","year":"2002"}
    
    public string? Level_Name { get; set; }  // Tên cấp độ khách hàng
    
    public int? Reward_Point { get; set; }  // Điểm tích lũy
    
    public int? Adjust_Points { get; set; }  // Điểm điều chỉnh
    
    public string? Gender { get; set; }  // "male", "female", "other" hoặc null
    
    public string? Billing_Address_Street { get; set; }  // Địa chỉ
    
    public List<string>? Assignee_Ids { get; set; }  // Array của UUID
    
    public string? Page_Id { get; set; }  // Page ID (nếu từ social)
    
    public string? Page_Customer { get; set; }  // Page customer info
    
    public string? Conversation_Id { get; set; }  // Conversation ID
    
    public string? Webhook_Partner { get; set; }  // Partner webhook info
    
    public object? Ad_Clicks { get; set; }  // Ad clicks data
}

/// <summary>
/// Birthday object trong customer webhook
/// </summary>
public class CustomerBirthday
{
    public string? Date { get; set; }  // "08"
    public string? Month { get; set; }  // "06"
    public string? Year { get; set; }  // "2002"
}

// ============================================
// INVENTORY SYNC MODELS
// ============================================

/// <summary>
/// Request để đồng bộ tồn kho từ ERP lên PanCake
/// Warehouse IDs sẽ được tự động lấy từ thông tin sản phẩm
/// </summary>
public class InventorySyncRequest
{
    /// <summary>
    /// BẮT BUỘC: Shop ID trên PanCake (ví dụ: 1635971586)
    /// </summary>
    [Required(ErrorMessage = "Shop ID là bắt buộc")]
    public long ShopId { get; set; }
    
    /// <summary>
    /// BẮT BUỘC: Mã vật tư (sẽ dùng để search product trên PanCake)
    /// </summary>
    [Required(ErrorMessage = "Mã vật tư là bắt buộc")]
    public string MaVt { get; set; } = string.Empty;
    
    /// <summary>
    /// BẮT BUỘC: Số lượng tồn kho cần cập nhật
    /// Hệ thống sẽ tự động cập nhật vào tất cả các kho của sản phẩm
    /// </summary>
    [Required(ErrorMessage = "Số lượng tồn kho là bắt buộc")]
    public int RemainQuantity { get; set; }
}

/// <summary>
/// Thông tin tồn kho của một kho cụ thể
/// </summary>
public class InventoryWarehouseItem
{
    /// <summary>
    /// Số lượng tồn
    /// </summary>
    [Required]
    public int RemainQuantity { get; set; }
    
    /// <summary>
    /// BẮT BUỘC: Warehouse ID trên PanCake (UUID format)
    /// </summary>
    [Required(ErrorMessage = "Warehouse ID là bắt buộc")]
    public string WarehouseId { get; set; } = string.Empty;
}

/// <summary>
/// Response từ API search product variations
/// </summary>
public class ProductVariationSearchResponse
{
    public bool Success { get; set; }
    
    /// <summary>
    /// Danh sách product variations (trực tiếp là array)
    /// </summary>
    public List<ProductVariationInfo>? Data { get; set; }
    
    public int? PageNumber { get; set; }
    public int? PageSize { get; set; }
    public int? TotalEntries { get; set; }
    public int? TotalPages { get; set; }
    public string? Message { get; set; }
}

/// <summary>
/// Thông tin chi tiết của product variation
/// </summary>
public class ProductVariationInfo
{
    /// <summary>
    /// Variation ID (UUID)
    /// </summary>
    public string? Id { get; set; }
    
    /// <summary>
    /// Product ID (UUID)  
    /// </summary>
    public string? ProductId { get; set; }
    
    /// <summary>
    /// Barcode
    /// </summary>
    public string? Barcode { get; set; }
    
    /// <summary>
    /// Display ID
    /// </summary>
    public string? DisplayId { get; set; }
    
    /// <summary>
    /// Danh sách warehouse info
    /// </summary>
    public List<VariationWarehouseInfo>? VariationsWarehouses { get; set; }
    
    /// <summary>
    /// Thông tin sản phẩm
    /// </summary>
    public ProductInfo? Product { get; set; }
}

/// <summary>
/// Thông tin sản phẩm cơ bản
/// </summary>
public class ProductInfo
{
    public string? Name { get; set; }
    public string? DisplayId { get; set; }
}

/// <summary>
/// Thông tin warehouse của variation
/// </summary>
public class VariationWarehouseInfo
{
    public string? WarehouseId { get; set; }
    public int? RemainQuantity { get; set; }
    public string? BatchPosition { get; set; }
    public string? ShelfPosition { get; set; }
}



/// <summary>
/// Request để update quantity trên PanCake
/// </summary>
public class UpdateQuantityRequest
{
    [Required]
    public List<InventoryWarehouseItem> VariationsWarehouses { get; set; } = new();
}

/// <summary>
/// Response từ update quantity API
/// </summary>
public class UpdateQuantityResponse
{
    public bool Success { get; set; }
    public object? Data { get; set; }
    public string? Message { get; set; }
}

/// <summary>
/// Result trả về từ inventory sync service
/// </summary>
public class InventorySyncResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ProductId { get; set; }
    public string? VariationId { get; set; }
    public string? MaVt { get; set; }
    public int? TotalQuantityUpdated { get; set; }
}

/// <summary>
/// Response từ API GET /shops/{SHOP_ID}/warehouses
/// </summary>
public class WarehousesResponse
{
    public bool Success { get; set; }
    public List<WarehouseInfo>? Data { get; set; }
    public string? Message { get; set; }
}

/// <summary>
/// Thông tin kho hàng
/// </summary>
public class WarehouseInfo
{
    /// <summary>
    /// ID kho (UUID)
    /// </summary>
    public string? Id { get; set; }
    
    /// <summary>
    /// Tên kho
    /// </summary>
    public string? Name { get; set; }
    
    /// <summary>
    /// Địa chỉ kho
    /// </summary>
    public string? Address { get; set; }
    
    /// <summary>
    /// Trạng thái kho (true = hoạt động)
    /// </summary>
    public bool? IsActive { get; set; }
}
