using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API_PanCake.Data.Entities
{
    [Table("WebhookOrders")]
    public class WebhookOrderEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int WebhookId { get; set; }
        
        public string? PanCakeOrderId { get; set; }
        
        [MaxLength(50)]
        public string? OrderCode { get; set; }
        
        public int? ShopId { get; set; }
        
        [MaxLength(50)]
        public string? EventType { get; set; }
        
        [MaxLength(50)]
        public string? Status { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal? TotalAmount { get; set; }
        
        [MaxLength(200)]
        public string? CustomerName { get; set; }
        
        [MaxLength(200)]
        public string? CustomerEmail { get; set; }
        
        [Column(TypeName = "nvarchar(max)")]
        public string? RawWebhookData { get; set; }
        
        public DateTime WebhookReceivedAt { get; set; }
        public long? PanCakeTimestamp { get; set; }
        
        [MaxLength(500)]
        public string? Signature { get; set; }
        
        public bool IsProcessed { get; set; }
        public DateTime? ProcessedAt { get; set; }
        
        [MaxLength(1000)]
        public string? ProcessingNote { get; set; }
    }
}
