using API_PanCake.Data;
using API_PanCake.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace API_PanCake.Services
{
    public class WebhookDbService : IWebhookDbService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<WebhookDbService> _logger;

        public WebhookDbService(AppDbContext context, ILogger<WebhookDbService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task EnsureDatabaseCreatedAsync()
        {
            try
            {
                _logger.LogInformation("Checking if database and tables exist...");
                var canConnect = await _context.Database.CanConnectAsync();
                
                if (!canConnect)
                {
                    _logger.LogWarning("Cannot connect to database. Creating database...");
                    await _context.Database.EnsureCreatedAsync();
                    _logger.LogInformation("Database created successfully!");
                }
                else
                {
                    _logger.LogInformation("Database exists. Ensuring schema is up to date...");
                    await _context.Database.EnsureCreatedAsync();
                    _logger.LogInformation("Database schema is ready!");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while ensuring database created");
                throw;
            }
        }

        public async Task<WebhookOrderEntity> SaveWebhookAsync(WebhookOrderEntity webhookOrder)
        {
            try
            {
                _logger.LogInformation("Saving webhook to database. Order Code: {OrderCode}, Event: {EventType}", webhookOrder.OrderCode, webhookOrder.EventType);

                // Tìm record cũ (nếu có)
                var existingRecord = await _context.WebhookOrders
                    .FirstOrDefaultAsync(w => w.PanCakeOrderId == webhookOrder.PanCakeOrderId && w.EventType == webhookOrder.EventType);

                if (existingRecord != null)
                {
                    // UPDATE: Cập nhật record cũ với data mới
                    _logger.LogInformation("Webhook already exists. Updating... Webhook ID: {WebhookId}", existingRecord.WebhookId);
                    
                    // Copy các fields còn lại vào record cũ (giữ nguyên WebhookId)
                    existingRecord.OrderCode = webhookOrder.OrderCode;
                    existingRecord.ShopId = webhookOrder.ShopId;
                    existingRecord.EventType = webhookOrder.EventType;
                    existingRecord.Status = webhookOrder.Status;
                    existingRecord.TotalAmount = webhookOrder.TotalAmount;
                    existingRecord.CustomerName = webhookOrder.CustomerName;
                    existingRecord.CustomerEmail = webhookOrder.CustomerEmail;
                    existingRecord.RawWebhookData = webhookOrder.RawWebhookData;
                    existingRecord.PanCakeTimestamp = webhookOrder.PanCakeTimestamp;
                    existingRecord.Signature = webhookOrder.Signature;
                    existingRecord.ProcessingNote = webhookOrder.ProcessingNote;
                    existingRecord.Signature = "test";
                    // Không update WebhookReceivedAt, IsProcessed, ProcessedAt để giữ lịch sử

                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Webhook updated successfully! Webhook ID: {WebhookId}", existingRecord.WebhookId);
                    return existingRecord;
                }
                else
                {
                    // INSERT: Tạo mới
                    _logger.LogInformation("New webhook. Inserting...");
                    webhookOrder.WebhookReceivedAt = DateTime.Now;
                    await _context.WebhookOrders.AddAsync(webhookOrder);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Webhook saved successfully! Webhook ID: {WebhookId}", webhookOrder.WebhookId);
                    return webhookOrder;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while saving webhook to database");
                throw;
            }
        }

        public async Task<List<WebhookOrderEntity>> GetWebhooksAsync(int skip = 0, int take = 100, bool? isProcessed = null)
        {
            try
            {
                var query = _context.WebhookOrders.AsQueryable();
                if (isProcessed.HasValue)
                {
                    query = query.Where(w => w.IsProcessed == isProcessed.Value);
                }
                return await query.OrderByDescending(w => w.WebhookReceivedAt).Skip(skip).Take(take).ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while getting webhooks from database");
                throw;
            }
        }

        public async Task MarkAsProcessedAsync(int webhookId, string? note = null)
        {
            try
            {
                var webhook = await _context.WebhookOrders.FindAsync(webhookId);
                if (webhook != null)
                {
                    webhook.IsProcessed = true;
                    webhook.ProcessedAt = DateTime.Now;
                    webhook.ProcessingNote = note;
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Webhook marked as processed. Webhook ID: {WebhookId}", webhookId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while marking webhook as processed");
                throw;
            }
        }

        public async Task<bool> WebhookExistsAsync(string? panCakeOrderId, string? eventType)
        {
            if (string.IsNullOrWhiteSpace(panCakeOrderId) || string.IsNullOrWhiteSpace(eventType))
            {
                return false;
            }
            try
            {
                return await _context.WebhookOrders.AnyAsync(w => w.PanCakeOrderId == panCakeOrderId && w.EventType == eventType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while checking if webhook exists");
                return false;
            }
        }

        // ============= CUSTOMER WEBHOOK METHODS =============
        
        public async Task<WebhookCustomerEntity> SaveCustomerWebhookAsync(WebhookCustomerEntity webhookCustomer)
        {
            try
            {
                _logger.LogInformation("Saving customer webhook to database. Customer: {CustomerName}, Event: {EventType}", 
                    webhookCustomer.CustomerName, webhookCustomer.EventType);

                // Tìm record cũ (nếu có)
                var existingRecord = await _context.WebhookCustomers
                    .FirstOrDefaultAsync(w => w.PanCakeCustomerId == webhookCustomer.PanCakeCustomerId && w.EventType == webhookCustomer.EventType);

                if (existingRecord != null)
                {
                    // UPDATE: Cập nhật record cũ với data mới
                    _logger.LogInformation("Customer webhook already exists. Updating... Webhook ID: {WebhookId}", existingRecord.WebhookId);
                    
                    existingRecord.CustomerCode = webhookCustomer.CustomerCode;
                    existingRecord.ShopId = webhookCustomer.ShopId;
                    existingRecord.EventType = webhookCustomer.EventType;
                    existingRecord.CustomerName = webhookCustomer.CustomerName;
                    existingRecord.CustomerPhone = webhookCustomer.CustomerPhone;
                    existingRecord.CustomerEmail = webhookCustomer.CustomerEmail;
                    existingRecord.CustomerAddress = webhookCustomer.CustomerAddress;
                    existingRecord.CustomerLevel = webhookCustomer.CustomerLevel;
                    existingRecord.CustomerPoints = webhookCustomer.CustomerPoints;
                    existingRecord.TotalSpent = webhookCustomer.TotalSpent;
                    existingRecord.Gender = webhookCustomer.Gender;
                    existingRecord.DateOfBirth = webhookCustomer.DateOfBirth;
                    existingRecord.RawWebhookData = webhookCustomer.RawWebhookData;
                    existingRecord.PanCakeTimestamp = webhookCustomer.PanCakeTimestamp;
                    existingRecord.Signature = webhookCustomer.Signature;
                    existingRecord.ProcessingNote = webhookCustomer.ProcessingNote;
                    
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Customer webhook updated successfully! Webhook ID: {WebhookId}", existingRecord.WebhookId);
                    return existingRecord;
                }
                else
                {
                    // INSERT: Tạo mới
                    _logger.LogInformation("New customer webhook. Inserting...");
                    webhookCustomer.WebhookReceivedAt = DateTime.Now;
                    await _context.WebhookCustomers.AddAsync(webhookCustomer);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Customer webhook saved successfully! Webhook ID: {WebhookId}", webhookCustomer.WebhookId);
                    return webhookCustomer;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while saving customer webhook to database");
                throw;
            }
        }

        public async Task<List<WebhookCustomerEntity>> GetCustomerWebhooksAsync(int skip = 0, int take = 100, bool? isProcessed = null)
        {
            try
            {
                var query = _context.WebhookCustomers.AsQueryable();
                if (isProcessed.HasValue)
                {
                    query = query.Where(w => w.IsProcessed == isProcessed.Value);
                }
                return await query.OrderByDescending(w => w.WebhookReceivedAt).Skip(skip).Take(take).ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while getting customer webhooks from database");
                throw;
            }
        }

        public async Task MarkCustomerAsProcessedAsync(int webhookId, string? note = null)
        {
            try
            {
                var webhook = await _context.WebhookCustomers.FindAsync(webhookId);
                if (webhook != null)
                {
                    webhook.IsProcessed = true;
                    webhook.ProcessedAt = DateTime.Now;
                    webhook.ProcessingNote = note;
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Customer webhook marked as processed. Webhook ID: {WebhookId}", webhookId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while marking customer webhook as processed");
                throw;
            }
        }

        public async Task<bool> CustomerWebhookExistsAsync(string? panCakeCustomerId, string? eventType)
        {
            if (string.IsNullOrEmpty(panCakeCustomerId) || string.IsNullOrEmpty(eventType))
            {
                return false;
            }
            try
            {
                return await _context.WebhookCustomers.AnyAsync(w => w.PanCakeCustomerId == panCakeCustomerId && w.EventType == eventType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while checking if customer webhook exists");
                return false;
            }
        }
    }
}
