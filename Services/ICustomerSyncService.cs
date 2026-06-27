namespace API_PanCake.Services
{
    public interface ICustomerSyncService
    {
        /// <summary>
        /// Đồng bộ khách hàng từ PanCake webhook vào ERP system (bảng dmkh)
        /// </summary>
        /// <param name="customerData">Dữ liệu customer từ webhook</param>
        /// <returns>Kết quả đồng bộ</returns>
        Task<CustomerSyncResult> SyncCustomerToERPAsync(Models.PanCakeCustomerWebhook customerData);
    }

    public class CustomerSyncResult
    {
        public bool Success { get; set; }
        public string? MaKh { get; set; }  // Mã khách hàng trong ERP
        public string? ErrorMessage { get; set; }
        public bool IsNewCustomer { get; set; }  // True nếu là khách hàng mới insert, False nếu đã tồn tại
        public string? Message { get; set; }
    }
}
