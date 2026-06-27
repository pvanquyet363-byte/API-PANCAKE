using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API_PanCake.Data.Entities
{
    /// <summary>
    /// Entity cho bảng dmkb_api - Quản lý API key của các shop PanCake
    /// </summary>
    [Table("dmkb_api")]
    public class DmkbApiEntity
    {
        /// <summary>
        /// Tên shop
        /// </summary>
        [Column("ten_shop")]
        [MaxLength(128)]
        public string? TenShop { get; set; }

        /// <summary>
        /// API Key để call PanCake API
        /// </summary>
        [Column("key_api")]
        [MaxLength(128)]
        public string? KeyApi { get; set; }

        /// <summary>
        /// Mã kho đơn hàng
        /// </summary>
        [Column("ma_kho_dh")]
        [MaxLength(128)]
        public string? MaKhoDh { get; set; }

        /// <summary>
        /// Mã kho tồn kho
        /// </summary>
        [Column("ma_kho_ton_kho")]
        [MaxLength(2000)]
        public string? MaKhoTonKho { get; set; }

        /// <summary>
        /// Trạng thái (A=Active, I=Inactive)
        /// </summary>
        [Column("status")]
        [MaxLength(1)]
        public string? Status { get; set; }

        /// <summary>
        /// Ngày tạo
        /// </summary>
        [Column("datetime0")]
        public DateTime? Datetime0 { get; set; }

        /// <summary>
        /// Ngày cập nhật
        /// </summary>
        [Column("datetime2")]
        public DateTime? Datetime2 { get; set; }

        /// <summary>
        /// User ID tạo
        /// </summary>
        [Column("user_id0")]
        public int? UserId0 { get; set; }

        /// <summary>
        /// User ID cập nhật
        /// </summary>
        [Column("user_id2")]
        public int? UserId2 { get; set; }

        /// <summary>
        /// ID Shop PanCake (dùng để match với shop_id từ webhook)
        /// </summary>
        [Key]  // Primary key
        [Column("id_shop")]
        [MaxLength(128)]
        public string IdShop { get; set; } = string.Empty;

        /// <summary>
        /// Mã shop
        /// </summary>
        [Column("ma_shop")]
        [MaxLength(128)]
        public string? MaShop { get; set; }
    }
}
