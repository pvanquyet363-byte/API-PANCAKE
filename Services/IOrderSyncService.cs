using API_PanCake.Services;

namespace API_PanCake.Services
{
    public interface IOrderSyncService
    {
        /// <summary>
        /// Đồng bộ đơn hàng từ PanCake vào ERP system (m65/d65 tables - Xuất bán)
        /// </summary>
        Task<OrderSyncResult> SyncOrderToERPAsync(int shopId, PanCakeOrderDetail orderDetail, string orderId);
    }

    public class OrderSyncResult
    {
        public bool Success { get; set; }
        public string? SttRec { get; set; }
        public string? SoCt { get; set; }
        public string? MaCt { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
