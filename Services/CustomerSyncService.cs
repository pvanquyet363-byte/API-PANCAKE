using API_PanCake.Data;
using API_PanCake.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace API_PanCake.Services
{
    public class CustomerSyncService : ICustomerSyncService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<CustomerSyncService> _logger;
        private readonly IConfiguration _configuration;

        public CustomerSyncService(
            AppDbContext context,
            ILogger<CustomerSyncService> logger,
            IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<CustomerSyncResult> SyncCustomerToERPAsync(PanCakeCustomerWebhook customerData)
        {
            try
            {
                _logger.LogInformation("=== BẮT ĐẦU ĐỒNG BỘ KHÁCH HÀNG VÀO ERP ===");
                _logger.LogInformation("Customer Name: {Name}, Shop: {ShopId}", customerData.Name, customerData.Shop_Id);

                // Tạo mã KH = KHLE + Tên Shop (sanitize tên shop)
                var shopId = (int)(customerData.Shop_Id ?? 0);
                var shopName = await GetShopNameAsync(shopId);
                var sanitizedShopName = SanitizeShopName(shopName);
                var maKh = $"KHLE{sanitizedShopName}";

                _logger.LogInformation("Mã KH dự kiến: {MaKh} (Shop: {ShopName})", maKh, shopName);

                // CHECK: Nếu mã KH theo shop đã tồn tại trong dmkh, KHÔNG tạo mã mới
                var existingMaKh = await CheckMaKhExistsAsync(maKh);
                if (existingMaKh != null)
                {
                    _logger.LogInformation("Mã KH {MaKh} đã tồn tại trong dmkh.", existingMaKh);
                    return new CustomerSyncResult
                    {
                        Success = true,
                        MaKh = existingMaKh,
                        IsNewCustomer = false,
                        Message = $"Khách hàng dùng mã master theo shop: {existingMaKh}"
                    };
                }

                // INSERT MÃ KH MỚI THEO SHOP VÀO dmkh
                var createdMaKh = await InsertMaKhToERPAsync(customerData, maKh);

                _logger.LogInformation("Đồng bộ khách hàng thành công! Mã KH: {MaKh}", createdMaKh);

                return new CustomerSyncResult
                {
                    Success = true,
                    MaKh = createdMaKh,
                    IsNewCustomer = true,
                    Message = $"Đã tạo khách hàng master theo shop với mã: {createdMaKh}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi đồng bộ khách hàng vào ERP");
                return new CustomerSyncResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    IsNewCustomer = false
                };
            }
        }

        /// <summary>
        /// Lấy tên shop từ bảng dmkb_api theo ShopId
        /// </summary>
        private async Task<string> GetShopNameAsync(int shopId)
        {
            try
            {
                var shopConfig = await _context.DmkbApis
                    .Where(x => x.IdShop.Trim() == shopId.ToString() && x.Status == "1")
                    .FirstOrDefaultAsync();

                if (shopConfig != null && !string.IsNullOrWhiteSpace(shopConfig.TenShop))
                {
                    _logger.LogInformation("Tên shop từ dmkb_api: {TenShop}", shopConfig.TenShop);
                    return shopConfig.TenShop.Trim();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không lấy được tên shop từ dmkb_api cho ShopId: {ShopId}", shopId);
            }

            // Fallback: dùng ShopId
            _logger.LogWarning("Không tìm thấy tên shop, dùng ShopId: {ShopId}", shopId);
            return shopId.ToString();
        }

        /// <summary>
        /// Sanitize tên shop để tạo mã KH hợp lệ:
        /// - Bỏ khoảng trắng, ký tự đặc biệt
        /// - Viết hoa
        /// - Giới hạn độ dài
        /// </summary>
        private string SanitizeShopName(string shopName)
        {
            if (string.IsNullOrWhiteSpace(shopName))
                return "";

            // Loại bỏ ký tự không phải chữ/số, thay khoảng trắng bằng không gì
            var sanitized = new string(shopName
                .Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-')
                .ToArray());

            // Viết hoa toàn bộ
            sanitized = sanitized.ToUpperInvariant();

            // Giới hạn độ dài tối đa (còn lại cho "KHLE" = 5 ký tự, tổng ma_kh thường <= 20)
            if (sanitized.Length > 15)
                sanitized = sanitized.Substring(0, 15);

            return sanitized;
        }

        /// <summary>
        /// Kiểm tra mã KH đã tồn tại trong dmkh chưa
        /// </summary>
        private async Task<string?> CheckMaKhExistsAsync(string maKh)
        {
            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                var sql = @"
                    SELECT TOP 1 ma_kh
                    FROM dmkh
                    WHERE ma_kh = @MaKh
                    AND kh_yn = 1";

                using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@MaKh", maKh);

                var result = await command.ExecuteScalarAsync();
                return result?.ToString()?.Trim();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi check mã KH tồn tại: {MaKh}", maKh);
                return null;
            }
        }

        /// <summary>
        /// Insert mã KH theo shop vào bảng dmkh
        /// </summary>
        private async Task<string> InsertMaKhToERPAsync(PanCakeCustomerWebhook customerData, string maKh)
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            using var transaction = connection.BeginTransaction();

            try
            {
                _logger.LogInformation("Inserting {MaKh} vào dmkh...", maKh);

                // Parse birthday
                DateTime? ngaySinh = null;
                if (customerData.Birthday != null &&
                    !string.IsNullOrEmpty(customerData.Birthday.Year) &&
                    !string.IsNullOrEmpty(customerData.Birthday.Month) &&
                    !string.IsNullOrEmpty(customerData.Birthday.Date))
                {
                    try
                    {
                        var year = int.Parse(customerData.Birthday.Year);
                        var month = int.Parse(customerData.Birthday.Month);
                        var day = int.Parse(customerData.Birthday.Date);
                        ngaySinh = new DateTime(year, month, day);
                    }
                    catch
                    {
                        _logger.LogWarning("Không parse được birthday");
                    }
                }

                // Lấy phone và email
                var phoneNumber = customerData.Phone_Numbers?.FirstOrDefault();
                var email = customerData.Emails?.FirstOrDefault();

                // Map gender
                char? gioiTinh = null;
                if (!string.IsNullOrEmpty(customerData.Gender))
                {
                    gioiTinh = customerData.Gender.ToLower() switch
                    {
                        "male" => 'M',
                        "female" => 'F',
                        _ => null
                    };
                }

                var sql = @"
                    INSERT INTO dmkh (
                        ma_kh,
                        ten_kh,
                        kh_yn,
                        dia_chi,
                        dien_thoai,
                        e_mail,
                        ngay_sinh,
                        gioi_tinh,
                        diem,
                        ghi_chu,
                        datetime0,
                        user_id0,
                        status
                    ) VALUES (
                        @MaKh,
                        @TenKh,
                        1,
                        @DiaChi,
                        @DienThoai,
                        @Email,
                        @NgaySinh,
                        @GioiTinh,
                        @Diem,
                        @GhiChu,
                        GETDATE(),
                        0,
                        'A'
                    )";

                using var command = new SqlCommand(sql, connection, transaction);
                command.Parameters.AddWithValue("@MaKh", maKh);
                command.Parameters.AddWithValue("@TenKh", customerData.Name ?? "Khách Lẻ");
                command.Parameters.AddWithValue("@DiaChi", (object?)customerData.Billing_Address_Street ?? DBNull.Value);
                command.Parameters.AddWithValue("@DienThoai", phoneNumber ?? "");
                command.Parameters.AddWithValue("@Email", (object?)email ?? DBNull.Value);
                command.Parameters.AddWithValue("@NgaySinh", (object?)ngaySinh ?? DBNull.Value);
                command.Parameters.AddWithValue("@GioiTinh", (object?)gioiTinh ?? DBNull.Value);
                command.Parameters.AddWithValue("@Diem", customerData.Reward_Point ?? 0);
                command.Parameters.AddWithValue("@GhiChu",
                    $"Đồng bộ từ PanCake - Shop: {customerData.Shop_Id}, Event: {customerData.Event_Type}");

                await command.ExecuteNonQueryAsync();

                await transaction.CommitAsync();

                _logger.LogInformation("Đã insert {MaKh} vào dmkh.", maKh);

                return maKh;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi khi insert {MaKh}, rollback transaction", maKh);
                throw;
            }
        }
    }
}
