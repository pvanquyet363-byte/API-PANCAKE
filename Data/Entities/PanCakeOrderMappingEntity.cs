using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API_PanCake.Data.Entities
{
    /// <summary>
    /// Bảng mapping giữa PanCake Order và ERP (m65/d65)
    /// Dùng để track đơn hàng đã sync chưa, tránh duplicate
    /// </summary>
    [Table("PanCakeOrderMapping")]
    public class PanCakeOrderMappingEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int MappingId { get; set; }

        /// <summary>
        /// PanCake Order ID (string vì DB lưu nvarchar)
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string PanCakeOrderId { get; set; } = "";

        /// <summary>
        /// Shop ID trong PanCake
        /// </summary>
        [Required]
        public int ShopId { get; set; }

        /// <summary>
        /// STT_REC trong ERP (m65/d65) - Format: A000000001DXB (13 chars)
        /// </summary>
        [Required]
        [MaxLength(13)]
        public string SttRec { get; set; } = string.Empty;

        /// <summary>
        /// Mã chứng từ (ma_ct) - VD: DXB (Xuất bán)
        /// </summary>
        [MaxLength(3)]
        public string? MaCt { get; set; }

        /// <summary>
        /// Số chứng từ (so_ct)
        /// </summary>
        [MaxLength(12)]
        public string? SoCt { get; set; }

        /// <summary>
        /// Partition của bảng (YYYYMM) - VD: 202603
        /// </summary>
        [MaxLength(6)]
        public string? Partition { get; set; }

        /// <summary>
        /// Lần đầu đồng bộ
        /// </summary>
        public DateTime FirstSyncedAt { get; set; }

        /// <summary>
        /// Lần cuối cập nhật
        /// </summary>
        public DateTime? LastSyncedAt { get; set; }

        /// <summary>
        /// Số lần đồng bộ (update)
        /// </summary>
        public int SyncCount { get; set; } = 1;

        /// <summary>
        /// Trạng thái đơn PanCake lần cuối sync
        /// </summary>
        [MaxLength(50)]
        public string? LastStatus { get; set; }

        /// <summary>
        /// Ghi chú
        /// </summary>
        [MaxLength(500)]
        public string? Note { get; set; }
    }
}
