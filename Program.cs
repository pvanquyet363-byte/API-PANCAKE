using API_PanCake.BackgroundServices;
using API_PanCake.Data;
using API_PanCake.Services;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "PanCake Product API",
        Version = "v1",
        Description = "API để tạo và cập nhật sản phẩm trên PanCake POS"
    });
    
    // Enable XML comments for Swagger
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

// Register Database Context
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register HttpClient and PanCakeService
builder.Services.AddHttpClient<IPanCakeService, PanCakeService>();

// Register Webhook Database Service
builder.Services.AddScoped<IWebhookDbService, WebhookDbService>();

// Register PanCake Order Service (để call API lấy thông tin đơn)
builder.Services.AddHttpClient();  // Add default HttpClient factory
builder.Services.AddScoped<IPanCakeOrderService, PanCakeOrderService>();

// Register Order Sync Service (đồng bộ đơn hàng vào ERP)
builder.Services.AddScoped<IOrderSyncService, OrderSyncService>();

// Register Customer Sync Service (đồng bộ khách hàng vào ERP)
builder.Services.AddScoped<ICustomerSyncService, CustomerSyncService>();

// Register Geographic Service (lookup mãQG/Tỉnh/Quận/Phường)
builder.Services.AddScoped<IGeographicService, GeographicService>();

// Register Inventory Sync Service (đồng bộ tồn kho lên PanCake)
builder.Services.AddHttpClient<IPanCakeInventoryService, PanCakeInventoryService>();

// Register Background Service (đồng bộ tồn kho tự động)
// Có thể enable/disable qua appsettings.json: "InventorySync:Enabled"
builder.Services.AddHostedService<InventorySyncBackgroundService>();

var app = builder.Build();

// Log để xác nhận Background Service đã được register
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogWarning("🔧 [PROGRAM] InventorySyncBackgroundService has been registered");
logger.LogWarning("🔧 [PROGRAM] Environment: {Environment}", app.Environment.EnvironmentName);

// ⚠️ Database table được tạo thủ công bằng script SQL
// Xem: SQL_Scripts/Create_WebhookOrders_Table.sql
// (Đã tắt auto-create vì gây crash API)

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "PanCake Product API v1"));

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
