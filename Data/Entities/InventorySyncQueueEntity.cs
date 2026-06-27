using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API_PanCake.Data.Entities
{
    /// <summary>
    /// Entity cho bảng InventorySyncQueue
    /// Queue để lưu danh sách ma_vt cần đồng bộ tồn kho lên PanCake
    /// </summary>
    [Table("InventorySyncQueue")]
    public class InventorySyncQueueEntity
    {
        [Key]
        [Column("Id")]
        public int Id { get; set; }

        /// <summary>
        /// Mã vật tư cần đồng bộ
        /// </summary>
        [Column("MaVt")]
        [MaxLength(25)]
        [Required]
        public string MaVt { get; set; } = string.Empty;

        /// <summary>
        /// Shop ID trên PanCake (mặc định 1635971586)
        /// </summary>
        [Column("ShopId")]
        [Required]
        public long ShopId { get; set; }

        /// <summary>
        /// Trạng thái: 0=Pending, 1=Processing, 2=Success, 3=Failed
        /// </summary>
        [Column("Status")]
        [Required]
        public byte Status { get; set; }

        /// <summary>
        /// Số lần thử
        /// </summary>
        [Column("RetryCount")]
        public int RetryCount { get; set; }

        /// <summary>
        /// Thời gian tạo
        /// </summary>
        [Column("CreatedAt")]
        [Required]
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Thời gian xử lý
        /// </summary>
        [Column("ProcessedAt")]
        public DateTime? ProcessedAt { get; set; }

        /// <summary>
        /// Thông báo lỗi (nếu có)
        /// </summary>
        [Column("ErrorMessage")]
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Số lượng tồn đã tính (để debug)
        /// </summary>
        [Column("TotalQuantity")]
        public decimal? TotalQuantity { get; set; }
    }

    /// <summary>
    /// Enum cho trạng thái sync
    /// </summary>
    public enum InventorySyncStatus : byte
    {
        Pending = 0,
        Processing = 1,
        Success = 2,
        Failed = 3
    }
}
