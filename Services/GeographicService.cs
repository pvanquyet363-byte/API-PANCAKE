using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace API_PanCake.Services
{
    public interface IGeographicService
    {
        Task<GeographicInfo?> LookupAsync(string provinceName, string districtName, string communeName);
    }

    public class GeographicInfo
    {
        public string MaQg { get; set; } = "";
        public string MaTinh { get; set; } = "";
        public string MaQuan { get; set; } = "";
        public string MaPhuong { get; set; } = "";
    }

    public class GeographicService : IGeographicService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<GeographicService> _logger;

        public GeographicService(IConfiguration configuration, ILogger<GeographicService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<GeographicInfo?> LookupAsync(string? provinceName, string? districtName, string? communeName)
        {
            if (string.IsNullOrWhiteSpace(provinceName) &&
                string.IsNullOrWhiteSpace(districtName) &&
                string.IsNullOrWhiteSpace(communeName))
            {
                return null;
            }

            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                var result = new GeographicInfo();

                // Lookup mã quốc gia (QG) - luôn trả về "VN"
                result.MaQg = "VN";

                // Lookup tỉnh
                if (!string.IsNullOrWhiteSpace(provinceName))
                {
                    result.MaTinh = await LookupTinhAsync(connection, provinceName);
                }

                // Lookup quận (theo tỉnh)
                if (!string.IsNullOrWhiteSpace(districtName) && !string.IsNullOrWhiteSpace(provinceName))
                {
                    result.MaQuan = await LookupQuanAsync(connection, provinceName, districtName);
                }

                // Lookup phường (theo quận)
                if (!string.IsNullOrWhiteSpace(communeName) && !string.IsNullOrWhiteSpace(districtName))
                {
                    result.MaPhuong = await LookupPhuongAsync(connection, districtName, communeName);
                }

                _logger.LogInformation(
                    "Geo lookup OK - Tinh: {MaTinh}, Quan: {MaQuan}, Phuong: {MaPhuong}",
                    result.MaTinh, result.MaQuan, result.MaPhuong);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lookup geographic info: {P} / {D} / {C}",
                    provinceName, districtName, communeName);
                return null;
            }
        }

        private async Task<string> LookupTinhAsync(SqlConnection connection, string provinceName)
        {
            var sql = @"
                SELECT TOP 1 ma_tinh
                FROM dmtinh
                WHERE LOWER(RTRIM(ten_tinh)) = LOWER(RTRIM(@name))
                   OR LOWER(RTRIM(ten_tinh)) LIKE '%' + LOWER(RTRIM(@name)) + '%'";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@name", provinceName.Trim());
            var result = await cmd.ExecuteScalarAsync();

            if (result != null)
            {
                return result.ToString()?.Trim() ?? "";
            }

            _logger.LogWarning("Không tìm thấy mã tỉnh cho: {Name}", provinceName);
            return "";
        }

        private async Task<string> LookupQuanAsync(SqlConnection connection, string provinceName, string districtName)
        {
            var sql = @"
                SELECT TOP 1 dq.ma_quan
                FROM dmquan dq
                INNER JOIN dmtinh dt ON dt.ma_tinh = dq.ma_tinh
                WHERE LOWER(RTRIM(dq.ten_quan)) = LOWER(RTRIM(@district))
                  AND (LOWER(RTRIM(dt.ten_tinh)) = LOWER(RTRIM(@province))
                       OR LOWER(RTRIM(dt.ten_tinh)) LIKE '%' + LOWER(RTRIM(@province)) + '%')";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@district", districtName.Trim());
            cmd.Parameters.AddWithValue("@province", provinceName.Trim());
            var result = await cmd.ExecuteScalarAsync();

            if (result != null)
            {
                return result.ToString()?.Trim() ?? "";
            }

            // Fallback: không check tỉnh
            var sqlFallback = @"
                SELECT TOP 1 ma_quan
                FROM dmquan
                WHERE LOWER(RTRIM(ten_quan)) = LOWER(RTRIM(@name))
                   OR LOWER(RTRIM(ten_quan)) LIKE '%' + LOWER(RTRIM(@name)) + '%'";

            using var cmdFallback = new SqlCommand(sqlFallback, connection);
            cmdFallback.Parameters.AddWithValue("@name", districtName.Trim());
            var resultFallback = await cmdFallback.ExecuteScalarAsync();

            if (resultFallback != null)
            {
                return resultFallback.ToString()?.Trim() ?? "";
            }

            _logger.LogWarning("Không tìm thấy mã quận cho: {Name}", districtName);
            return "";
        }

        private async Task<string> LookupPhuongAsync(SqlConnection connection, string districtName, string communeName)
        {
            var sql = @"
                SELECT TOP 1 dp.ma_phuong
                FROM dmphuong dp
                INNER JOIN dmquan dq ON dq.ma_quan = dp.ma_quan
                WHERE LOWER(RTRIM(dp.ten_phuong)) = LOWER(RTRIM(@commune))
                  AND LOWER(RTRIM(dq.ten_quan)) = LOWER(RTRIM(@district))";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@commune", communeName.Trim());
            cmd.Parameters.AddWithValue("@district", districtName.Trim());
            var result = await cmd.ExecuteScalarAsync();

            if (result != null)
            {
                return result.ToString()?.Trim() ?? "";
            }

            // Fallback: không check quận
            var sqlFallback = @"
                SELECT TOP 1 ma_phuong
                FROM dmphuong
                WHERE LOWER(RTRIM(ten_phuong)) = LOWER(RTRIM(@name))
                   OR LOWER(RTRIM(ten_phuong)) LIKE '%' + LOWER(RTRIM(@name)) + '%'";

            using var cmdFallback = new SqlCommand(sqlFallback, connection);
            cmdFallback.Parameters.AddWithValue("@name", communeName.Trim());
            var resultFallback = await cmdFallback.ExecuteScalarAsync();

            if (resultFallback != null)
            {
                return resultFallback.ToString()?.Trim() ?? "";
            }

            _logger.LogWarning("Không tìm thấy mã phường cho: {Name}", communeName);
            return "";
        }
    }
}
