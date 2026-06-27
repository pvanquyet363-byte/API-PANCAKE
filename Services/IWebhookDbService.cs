using API_PanCake.Data.Entities;

namespace API_PanCake.Services
{
    public interface IWebhookDbService
    {
        Task EnsureDatabaseCreatedAsync();
        
        // Order webhook methods
        Task<WebhookOrderEntity> SaveWebhookAsync(WebhookOrderEntity webhookOrder);
        Task<List<WebhookOrderEntity>> GetWebhooksAsync(int skip = 0, int take = 100, bool? isProcessed = null);
        Task MarkAsProcessedAsync(int webhookId, string? note = null);
        Task<bool> WebhookExistsAsync(string? panCakeOrderId, string? eventType);
        
        // Customer webhook methods
        Task<WebhookCustomerEntity> SaveCustomerWebhookAsync(WebhookCustomerEntity webhookCustomer);
        Task<List<WebhookCustomerEntity>> GetCustomerWebhooksAsync(int skip = 0, int take = 100, bool? isProcessed = null);
        Task MarkCustomerAsProcessedAsync(int webhookId, string? note = null);
        Task<bool> CustomerWebhookExistsAsync(string? panCakeCustomerId, string? eventType);
    }
}
