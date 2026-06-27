using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API_PanCake.Data.Entities
{
    [Table("WebhookCustomers")]
    public class WebhookCustomerEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int WebhookId { get; set; }
        
        [MaxLength(100)]
        public string? PanCakeCustomerId { get; set; }
        
        [MaxLength(100)]
        public string? CustomerCode { get; set; }
        
        public int? ShopId { get; set; }
        
        [MaxLength(50)]
        public string? EventType { get; set; }
        
        [MaxLength(200)]
        public string? CustomerName { get; set; }
        
        [MaxLength(50)]
        public string? CustomerPhone { get; set; }
        
        [MaxLength(200)]
        public string? CustomerEmail { get; set; }
        
        [MaxLength(500)]
        public string? CustomerAddress { get; set; }
        
        [MaxLength(50)]
        public string? CustomerLevel { get; set; }
        
        public int? CustomerPoints { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal? TotalSpent { get; set; }
        
        [MaxLength(50)]
        public string? Gender { get; set; }
        
        public DateTime? DateOfBirth { get; set; }
        
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
