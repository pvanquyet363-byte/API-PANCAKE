using API_PanCake.Data;
using API_PanCake.Data.Entities;
using API_PanCake.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace API_PanCake.Services
{
    public class OrderSyncService : IOrderSyncService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<OrderSyncService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IGeographicService _geographicService;
        private readonly AppDbContext _dbContext;

        public OrderSyncService(
            AppDbContext context,
            ILogger<OrderSyncService> logger,
            IConfiguration configuration,
            IGeographicService geographicService,
            AppDbContext dbContext)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
            _geographicService = geographicService;
            _dbContext = dbContext;
        }

        public async Task<OrderSyncResult> SyncOrderToERPAsync(int shopId, PanCakeOrderDetail orderDetail, string pancakeOrderId)
        {
            try
            {
                _logger.LogInformation("Bắt đầu sync order ID {OrderId} vào ERP", orderDetail.Id);

                var shopConfig = await _context.DmkbApis
                    .Where(x => x.IdShop.Trim() == shopId.ToString() && x.Status == "1")
                    .FirstOrDefaultAsync();

                if (shopConfig == null)
                {
                    return new OrderSyncResult
                    {
                        Success = false,
                        ErrorMessage = $"Không tìm thấy cấu hình cho Shop ID: {shopId}"
                    };
                }
                var existingMapping = await _dbContext.PanCakeOrderMappings.FirstOrDefaultAsync(m => m.PanCakeOrderId == pancakeOrderId && m.ShopId == shopId);

                if (existingMapping != null)
                {
                    _logger.LogWarning("⚠️ Đơn hàng đã được đồng bộ trước đó!");
                    _logger.LogInformation("   STT_REC cũ: {SttRec}, Lần đầu sync: {FirstSync}, Số lần sync: {Count}",
                        existingMapping.SttRec, existingMapping.FirstSyncedAt, existingMapping.SyncCount);

                    // XÓA records cũ trong ERP trước khi INSERT mới
                    await DeleteOldERPRecordsAsync(existingMapping);

                    _logger.LogInformation("🔄 Đang CẬP NHẬT đơn hàng (đè vào đơn cũ)...");
                }
                else
                {
                    _logger.LogInformation("✨ Đơn hàng MỚI - chưa đồng bộ lần nào");
                }

                DateTime ngayCt;
                string partition;

                if (!string.IsNullOrEmpty(orderDetail.Inserted_At))
                {
                    if (DateTime.TryParse(orderDetail.Inserted_At, out var insertedDate))
                    {
                        ngayCt = insertedDate;
                        partition = insertedDate.ToString("yyyyMM");
                    }
                    else
                    {
                        _logger.LogWarning("Không parse được inserted_at: '{InsertedAt}', dùng ngày hiện tại", orderDetail.Inserted_At);
                        ngayCt = DateTime.Now;
                        partition = ngayCt.ToString("yyyyMM");
                    }
                }
                else
                {
                    ngayCt = DateTime.Now;
                    partition = ngayCt.ToString("yyyyMM");
                }

                // Xác định loại phiếu: returned/returning -> SDF (m760/d760), ngược lại -> DXB (m65/d65)
                var isSDF = IsSDFOrder(orderDetail);
                _logger.LogInformation("Phân loại phiếu: {VoucherType} (Status: {Status})",
                    isSDF ? "SDF (Trả hàng)" : "DXB (Xuất bán)", orderDetail.Status_Name);

                var connectionString = _configuration.GetConnectionString("DefaultConnection");

                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            // ===== INSERT/UPDATE KHÁCH HÀNG VÀO dmkh =====
                            var maKh = await InsertOrUpdateCustomerAsync(connection, transaction, shopId, orderDetail, shopConfig);
                            _logger.LogInformation("Đã insert/update khách hàng: {MaKh}", maKh);

                            // Luôn sinh STT_REC cho DXB trước
                            var (sttRec, soCt) = await GenerateSTTRECAndSoCTAsync(connection, transaction);

                            _logger.LogInformation("Generated DXB - STT_REC: {SttRec}, SO_CT: {SoCt}, Partition: {Partition}",
                                sttRec, soCt, partition);

                            // ===== LUÔN INSERT DXB (m65) =====
                            if (orderDetail.Status != 0)
                            {
                                _logger.LogInformation("Đồng bộ vào bảng m65${Partition} (Xuất bán)...", partition);
                                await InsertMasterRecordAsync(connection, transaction, sttRec, soCt, ngayCt, partition, shopId, orderDetail, shopConfig, maKh, isSDF);
                                _logger.LogInformation("Hoàn thành INSERT m65${Partition}", partition);

                                if (orderDetail.Items != null && orderDetail.Items.Count > 0)
                                {
                                    await InsertDetailRecordsAsync(connection, transaction, sttRec, soCt, ngayCt, partition, shopId, orderDetail, shopConfig);
                                }

                                await ExecuteAfterUpdateProceduresAsync(connection, transaction, sttRec, partition);
                            }

                            // ===== ĐƠN TRẢ LẠI: INSERT THÊM SDF (m760) =====
                            string? sttRecSdf = null;
                            if (isSDF)
                            {
                                var (sRec, sCt) = await GenerateSTTRECAndSoCTAsyncSDF(connection, transaction);
                                sttRecSdf = sRec;
                                
                                _logger.LogInformation("Generated SDF - STT_REC: {SttRec}, SO_CT: {SoCt}, Partition: {Partition}",
                                    sttRecSdf, sCt, partition);

                                _logger.LogInformation("Đồng bộ vào bảng m760${Partition} (Trả hàng)...", partition);
                                await InsertSDFMasterRecordAsync(connection, transaction, sttRecSdf, sCt, ngayCt, partition, shopId, orderDetail, shopConfig,maKh);
                                _logger.LogInformation("Hoàn thành INSERT m760${Partition}", partition);

                                if (orderDetail.Items != null && orderDetail.Items.Count > 0)
                                {
                                    await InsertSDFDetailRecordsAsync(connection, transaction, sttRecSdf, sCt, ngayCt, partition, shopId, orderDetail, shopConfig);
                                }

                                await ExecuteAfterUpdate2ProceduresAsync(connection, transaction, sttRecSdf, partition);
                            }

                            await transaction.CommitAsync();

                            _logger.LogInformation("Sync DXB thành công - STT_REC: {SttRec}", sttRec);
                            if (sttRecSdf != null)
                            {
                                _logger.LogInformation("Sync SDF thành công - STT_REC: {SttRec}", sttRecSdf);
                            }

                            return new OrderSyncResult
                            {
                                Success = true,
                                SttRec = sttRec,
                                SoCt = soCt,
                                MaCt = isSDF ? "DXB+SDF" : "DXB"
                            };
                        }
                        catch (Exception ex)
                        {
                            await transaction.RollbackAsync();
                            _logger.LogError(ex, "LỖI khi sync, đã rollback transaction");
                            throw new Exception($"Lỗi sync ERP: {ex.Message}", ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LỖI NGHIÊM TRỌNG khi sync order vào ERP");
                return new OrderSyncResult
                {
                    Success = false,
                    ErrorMessage = $"Lỗi: {ex.Message}. Chi tiết: {ex.InnerException?.Message ?? "N/A"}"
                };
            }
        }

        private async Task DeleteOldERPRecordsAsync(Data.Entities.PanCakeOrderMappingEntity mapping)
        {
            try
            {
                _logger.LogInformation("🗑️ Đang xóa records cũ trong ERP...");
                _logger.LogInformation("   STT_REC: {SttRec}, Partition: {Partition}, MaCt: {MaCt}",
                    mapping.SttRec, mapping.Partition, mapping.MaCt);

                var connectionString = _dbContext.Database.GetConnectionString();
                using (var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    var partition = mapping.Partition ?? "000000";
                    var sttRec = mapping.SttRec;
                    var maCt = mapping.MaCt ?? "DXB";

                    string deleteSql;
                    if (maCt == "SDF")
                    {
                        // SDF: xóa từ bảng m760/d760/i760/c760
                        deleteSql = $@"
                            DELETE FROM d760${partition} WHERE stt_rec = @stt_rec;
                            DELETE FROM m760${partition} WHERE stt_rec = @stt_rec;
                            DELETE FROM i760${partition} WHERE stt_rec = @stt_rec;
                            DELETE FROM c760$000000 WHERE stt_rec = @stt_rec;";
                        _logger.LogInformation("Xóa SDF records (m760/d760/i760/c760) cho STT_REC: {SttRec}", sttRec);
                    }
                    else
                    {
                        // DXB: xóa từ bảng m65/d65/i65/c65
                        deleteSql = $@"
                            DELETE FROM d65${partition} WHERE stt_rec = @stt_rec;
                            DELETE FROM m65${partition} WHERE stt_rec = @stt_rec;
                            DELETE FROM i65${partition} WHERE stt_rec = @stt_rec;
                            DELETE FROM c65$000000 WHERE stt_rec = @stt_rec;";
                        _logger.LogInformation("Xóa DXB records (m65/d65/i65/c65) cho STT_REC: {SttRec}", sttRec);
                    }

                    using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(deleteSql, connection))
                    {
                        cmd.Parameters.AddWithValue("@stt_rec", sttRec);
                        var rowsAffected = await cmd.ExecuteNonQueryAsync();
                        _logger.LogInformation("✅ Đã xóa {Rows} records cũ ({MaCt})", rowsAffected, maCt);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Lỗi khi xóa records cũ trong ERP");
                // Không throw - vẫn tiếp tục insert mới
            }
        }

        /// <summary>
        /// Xác định loại phiếu:
        /// - returned / returning -> SDF (m760/d760)
        /// - các status khác -> DXB (m65/d65)
        /// </summary>
        private bool IsSDFOrder(PanCakeOrderDetail orderDetail)
        {
            var status = (orderDetail.Status ?? 0);
            var statusName = (orderDetail.Status_Name ?? "").ToLowerInvariant();

            // Status codes: returned = 13, returning = 14 (tra cứu theo PanCake)
            // Hoặc status_name chứa "return"
            return status == 13 || status == 14 ||
                   statusName.Contains("returned");
                   //statusName.Contains("returning") ||

        }

        /// <summary>
        /// Map payment_method từ PanCake sang mã hình thức thanh toán trong ERP
        /// </summary>
        private int GetPaymentType(
           decimal? cod,
           decimal? transferMoney,
           decimal? prepaid)
        {
            if ((cod ?? 0) > 0)
                return 1; // Tiền mặt

            if ((transferMoney ?? 0) > 0 || (prepaid ?? 0) > 0)
                return 2; // Chuyển khoản

            return 2;
        }
        private async Task<(string sttRec, string soCt)> GenerateSTTRECAndSoCTAsync(
            SqlConnection connection,
            SqlTransaction transaction)
        {
            try
            {
                // Lấy STT_REC dạng AXXXXXXXXX (1 prefix + 9 số = 10 ký tự)
                // Nếu STT_REC trong DB dài hơn, chỉ lấy 10 ký tự đầu
                var sqlSttRec = @"
                    SELECT TOP 1
                        CASE 
                            WHEN LEN(stt_rec) >= 10 THEN SUBSTRING(stt_rec, 1, 10)
                            ELSE stt_rec
                        END AS max_stt_rec
                    FROM c65$000000
                    WHERE stt_rec LIKE 'A%' AND LEN(stt_rec) >= 10
                    ORDER BY stt_rec DESC";

                string maxSttRec = "A000000000";

                using (var cmd = new SqlCommand(sqlSttRec, connection, transaction))
                {
                    var result = await cmd.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value)
                    {
                        var dbValue = result.ToString() ?? "A000000000";
                        // Đảm bảo luôn có 10 ký tự, loại bỏ suffix nếu có
                        maxSttRec = dbValue.Length >= 10 ? dbValue.Substring(0, 10) : dbValue.PadLeft(10, '0');
                        _logger.LogInformation("STT_REC từ DB: {DbValue} -> Dùng: {MaxSttRec}", dbValue, maxSttRec);
                    }
                }

                string sttRecPrefix = maxSttRec.Substring(0, 1);
                string sttRecNumberStr = maxSttRec.Substring(1, 9);

                if (!int.TryParse(sttRecNumberStr, out int sttRecNumber))
                {
                    _logger.LogWarning("Không parse được STT_REC number '{NumberStr}', đặt = 0", sttRecNumberStr);
                    sttRecNumber = 0;
                }

                int nextSttRecNumber = sttRecNumber + 1;
                // STT_REC luôn có 10 ký tự: 1 prefix + 9 số
                string newSttRec = $"{sttRecPrefix}{nextSttRecNumber:D9}DXB";

                string newSoCt = nextSttRecNumber.ToString();

                _logger.LogInformation("Tạo STT_REC mới: {SttRec}, SO_CT: {SoCt}", newSttRec, newSoCt);
                return (newSttRec, newSoCt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LỖI khi generate STT_REC/SO_CT");
                return ("A000000001", "1");
            }
        }

        private async Task<(string sttRec, string soCt)> GenerateSTTRECAndSoCTAsyncSDF(
    SqlConnection connection,
    SqlTransaction transaction)
        {
            try
            {
                // Lấy STT_REC dạng AXXXXXXXXX (1 prefix + 9 số = 10 ký tự)
                // Nếu STT_REC trong DB dài hơn, chỉ lấy 10 ký tự đầu
                var sqlSttRec = @"
                    SELECT TOP 1
                        CASE 
                            WHEN LEN(stt_rec) >= 10 THEN SUBSTRING(stt_rec, 1, 10)
                            ELSE stt_rec
                        END AS max_stt_rec
                    FROM c760$000000
                    WHERE stt_rec LIKE 'A%' AND LEN(stt_rec) >= 10
                    ORDER BY stt_rec DESC";

                string maxSttRec = "A000000000";

                using (var cmd = new SqlCommand(sqlSttRec, connection, transaction))
                {
                    var result = await cmd.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value)
                    {
                        var dbValue = result.ToString() ?? "A000000000";
                        // Đảm bảo luôn có 10 ký tự, loại bỏ suffix nếu có
                        maxSttRec = dbValue.Length >= 10 ? dbValue.Substring(0, 10) : dbValue.PadLeft(10, '0');
                        _logger.LogInformation("STT_REC từ DB: {DbValue} -> Dùng: {MaxSttRec}", dbValue, maxSttRec);
                    }
                }

                string sttRecPrefix = maxSttRec.Substring(0, 1);
                string sttRecNumberStr = maxSttRec.Substring(1, 9);

                if (!int.TryParse(sttRecNumberStr, out int sttRecNumber))
                {
                    _logger.LogWarning("Không parse được STT_REC number '{NumberStr}', đặt = 0", sttRecNumberStr);
                    sttRecNumber = 0;
                }

                int nextSttRecNumber = sttRecNumber + 1;
                // STT_REC luôn có 10 ký tự: 1 prefix + 9 số
                string newSttRec = $"{sttRecPrefix}{nextSttRecNumber:D9}SDF";

                string newSoCt = nextSttRecNumber.ToString();

                _logger.LogInformation("Tạo STT_REC mới: {SttRec}, SO_CT: {SoCt}", newSttRec, newSoCt);
                return (newSttRec, newSoCt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LỖI khi generate STT_REC/SO_CT");
                return ("A000000001", "1");
            }
        }

        private async Task ExecuteAfterUpdateProceduresAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            string sttRec,
            string partition)
        {
            var voucherId = "DXB";
            var inquiryPartition = $"i65${partition}";
            var primePartition = $"m65${partition}";
            var detailPartition = $"d65${partition}";
            var masterTable = "c65$000000";
            var operation = "New";

            await RunSP(connection, transaction, voucherId, inquiryPartition, primePartition, detailPartition, masterTable, sttRec, operation);
        }

        private async Task ExecuteAfterUpdate2ProceduresAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            string sttRec,
            string partition)
        {
            var voucherId = "SDF";
            var inquiryPartition = $"i760${partition}";
            var primePartition = $"m760${partition}";
            var detailPartition = $"d760${partition}";
            var masterTable = "c760$000000";
            var operation = "New";

            await RunSP(connection, transaction, voucherId, inquiryPartition, primePartition, detailPartition, masterTable, sttRec, operation);
        }

        private async Task RunSP(SqlConnection connection, SqlTransaction transaction,
            string voucherId, string inquiryPartition, string primePartition,
            string detailPartition, string masterTable, string sttRec, string operation)
        {
            var sql1 = @"
                EXEC SSELIB$App$Voucher$UpdateInquiryTable
                    @id, @inquiryPartition, @primePartition, @detailPartition,
                    'stt_rec', @stt_rec, @operation";

            using (var cmd = new SqlCommand(sql1, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@id", voucherId);
                cmd.Parameters.AddWithValue("@inquiryPartition", inquiryPartition);
                cmd.Parameters.AddWithValue("@primePartition", primePartition);
                cmd.Parameters.AddWithValue("@detailPartition", detailPartition);
                cmd.Parameters.AddWithValue("@stt_rec", sttRec);
                cmd.Parameters.AddWithValue("@operation", operation);
                await cmd.ExecuteNonQueryAsync();
            }

            var sql2 = @"
                EXEC SSELIB$App$Voucher$UpdateGrandTable
                    @id, @masterTable, @primePartition, 'stt_rec', @stt_rec";

            using (var cmd = new SqlCommand(sql2, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@id", voucherId);
                cmd.Parameters.AddWithValue("@masterTable", masterTable);
                cmd.Parameters.AddWithValue("@primePartition", primePartition);
                cmd.Parameters.AddWithValue("@stt_rec", sttRec);
                await cmd.ExecuteNonQueryAsync();
            }

            var sql3 = @"
                EXEC SSELIB$App$Voucher$UpdateGeneral
                    @id, @primePartition, @detailPartition, @inquiryPartition, @primePartition2, @stt_rec";

            using (var cmd = new SqlCommand(sql3, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@id", voucherId);
                cmd.Parameters.AddWithValue("@primePartition", primePartition);
                cmd.Parameters.AddWithValue("@detailPartition", detailPartition);
                cmd.Parameters.AddWithValue("@inquiryPartition", inquiryPartition);
                cmd.Parameters.AddWithValue("@primePartition2", primePartition);
                cmd.Parameters.AddWithValue("@stt_rec", sttRec);
                await cmd.ExecuteNonQueryAsync();
            }

            _logger.LogInformation("Stored procedures hoàn thành cho {VoucherId}", voucherId);
        }

        /// <summary>
        /// Insert hoặc Update khách hàng vào bảng dmkh
        /// ma_kh = "KHLE" + SHOP_ID (cố định cho tất cả khách của shop đó)
        /// Thông tin khách: ten_kh, dia_chi, dt từ shipping_address của đơn hàng
        /// </summary>
        private async Task<string> InsertOrUpdateCustomerAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            int shopId,
            PanCakeOrderDetail orderDetail,
            Data.Entities.DmkbApiEntity shopConfig)
        {
            // Tất cả khách hàng của shop này dùng chung 1 mã
            var maKh = $"KHLE{shopId}";

            var customerName = orderDetail.Customer?.Name?.Trim() ?? orderDetail.Shipping_Address?.Full_Name?.Trim() ?? "";
            var customerPhone = orderDetail.Customer?.Phone_Number?.Trim() ?? orderDetail.Shipping_Address?.Phone_Number?.Trim() ?? "";
            var customerAddress = orderDetail.Customer?.Address ?? orderDetail.Shipping_Address?.Full_Address?.Trim() ?? "";
            var customerDistrict = orderDetail.Shipping_Address?.District_Name?.Trim() ?? "";
            var customerProvince = orderDetail.Shipping_Address?.Province_Name?.Trim() ?? "";

            // Ghép địa chỉ đầy đủ
            if (string.IsNullOrEmpty(customerAddress))
            {
                customerAddress = $"{customerDistrict}, {customerProvince}".Trim();
                if (customerAddress.StartsWith(", "))
                    customerAddress = customerAddress.Substring(2);
            }

            // Kiểm tra khách hàng đã tồn tại chưa
            var checkSql = "SELECT COUNT(1) FROM dmkh WHERE ma_kh = @ma_kh";
            int exists = 0;
            using (var checkCmd = new SqlCommand(checkSql, connection, transaction))
            {
                checkCmd.Parameters.AddWithValue("@ma_kh", maKh);
                var result = await checkCmd.ExecuteScalarAsync();
                exists = Convert.ToInt32(result);
            }

            if (exists == 0)
            {
            //    // UPDATE thông tin khách hàng
            //    var updateSql = @"
            //        UPDATE dmkh SET
            //            ten_kh = @ten_kh,
            //            dia_chi = @dia_chi
            //        WHERE ma_kh = @ma_kh and dien_thoai=@dien_thoai";
            //    using (var updateCmd = new SqlCommand(updateSql, connection, transaction))
            //    {
            //        updateCmd.Parameters.AddWithValue("@ma_kh", maKh);
            //        updateCmd.Parameters.AddWithValue("@ten_kh", customerName.Length > 100 ? customerName.Substring(0, 100) : customerName);
            //        updateCmd.Parameters.AddWithValue("@dia_chi", customerAddress.Length > 255 ? customerAddress.Substring(0, 255) : customerAddress);
            //        updateCmd.Parameters.AddWithValue("@dien_thoai", customerPhone);
            //        await updateCmd.ExecuteNonQueryAsync();
            //    }
            //    _logger.LogInformation("Đã UPDATE khách hàng {MaKh}: {TenKh}", maKh, customerName);
            //}
            //else
            //{
                // INSERT khách hàng mới - chỉ insert những fields cần thiết
                var insertSql = @"
                    INSERT INTO dmkh (ma_kh, ten_kh, dia_chi, status,dien_thoai)
                    VALUES (@ma_kh, @ten_kh, @dia_chi, '1',@dien_thoai)";
                using (var insertCmd = new SqlCommand(insertSql, connection, transaction))
                {
                    insertCmd.Parameters.AddWithValue("@ma_kh", maKh);
                    insertCmd.Parameters.AddWithValue("@ten_kh", customerName.Length > 100 ? customerName.Substring(0, 100) : customerName);
                    insertCmd.Parameters.AddWithValue("@dia_chi", customerAddress.Length > 255 ? customerAddress.Substring(0, 255) : customerAddress);
                    insertCmd.Parameters.AddWithValue("@dien_thoai", customerPhone);
                    await insertCmd.ExecuteNonQueryAsync();
                }
                _logger.LogInformation("Đã INSERT khách hàng mới {MaKh}: {TenKh}", maKh, customerName);
            }

            return maKh;
        }

        /// <summary>
        /// Insert vào m65$YYYYMM (DXB - Xuất bán)
        /// Bao gồm: geographic fields, ong_ba (shipper), fcode3 (đơn vị vận chuyển)
        /// </summary>
        private async Task InsertMasterRecordAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            string sttRec,
            string soCt,
            DateTime ngayCt,
            string partition,
            int shopId,
            PanCakeOrderDetail orderDetail,
            Data.Entities.DmkbApiEntity shopConfig,
            string maKh, bool isSDF)
        {
            var tableName = $"m65${partition}";

            // Tính tổng từ items
            decimal totalQuantity = 0;
            decimal totalAmount = 0;
            if (orderDetail.Items != null && orderDetail.Items.Count > 0)
            {
                foreach (var item in orderDetail.Items)
                {
                    var quantity = item.Quantity ?? 0;
                    var price = item.Variation_Info?.Retail_Price ?? item.Price ?? 0;
                    totalQuantity += quantity;
                    totalAmount += quantity * price;
                }
            }

            var tTienNt = orderDetail.Total_Price ?? totalAmount;
            var tTien = tTienNt;
            var tCkNt = orderDetail.Total_Discount ?? 0;
            var tCk = tCkNt;
            var tTtNt = tTienNt - tCkNt;
            var tTt = tTien - tCk;

            // Lookup geographic info cho shipping address
            var geoInfo = await _geographicService.LookupAsync(
                orderDetail.Shipping_Address?.Province_Name ?? "",
                orderDetail.Shipping_Address?.District_Name ?? "",
                orderDetail.Shipping_Address?.Commune_Name ?? "");

            // ong_ba: người giao hàng (shipper) - lấy từ shipping_address
            var shipperName = orderDetail.Shipping_Address?.Full_Name ?? "";
            if (shipperName.Length > 24) shipperName = shipperName.Substring(0, 24);

            // fcode3: đơn vị vận chuyển
            var fcode3 = orderDetail.Shipping_Unit ?? "";

            var sql = $@"
                INSERT INTO {tableName} (
                      stt_rec, ma_dvcs, ma_ct, loai_ct, so_lo, ngay_lo, ma_nk, ma_gd,
                    ngay_lct, ngay_ct, so_ct,
                    ma_nt, ty_gia,
                    ong_ba, ma_kh, dien_giai,
                    ma_nx, tk,
                    t_so_luong,
                    t_tien_nt, t_tien, t_tien2, t_tien_nt2,
                    t_thue_nt, t_thue,
                    t_tt_nt, t_tt,
                    ma_tt,
                    t_ck, t_ck_nt,
                    t_cp_bh, t_cp_bh_nt, t_cp_vc, t_cp_vc_nt, t_cp_khac, t_cp_khac_nt, t_cp, t_cp_nt,
                    ngay_hl,
                    ma_dc, ma_htvc,
                    so_hd0, stt_rec_hd0, so_ct0, ngay_ct0, ngay_ct2, ngay_ct3,
                    ma_nvbh, ck_thue_yn, ma_gia,
                    tien_hd,
                    nam, ky,
                    xtag, status,
                    datetime0, datetime2, user_id0, user_id2,
                    contract_id, bcontract_id, fee_id, so_ddh, so_hd, so_dt, so_po, so_bg,
                    job_id, prd_id, dept_id, mo_nbr,
                    fcode1, fcode2, fcode3, fdate1, fdate2, fdate3, fqty1, fqty2, fqty3,
                    fnote1, fnote2, fnote3,
                    s1, s2, s3, s4, s5, s6, s7, s8, s9,
                    dd_giaonhan, uu_tien,
                    tile_tu, tien_tu, tien_tu_nt,
                    ma_qly_d, ma_ksnb_d, ma_giamdoc_d, ngay_qly_d, ngay_ksnb_d, ngay_giamdoc_d,
                    han_tt_cl, tg_xuat_phat,
                    t_tien_gui, t_tien_gui_nt, tl_gui,
                    t_cm, t_cm_nt,
                    dx_yn, ghi_chu,
                    tl_fb, tien_fb_nt, tien_fb,
                    ma_httt, xuat_yn, stt_rec_dp, so_ct_dp, check_cxk, cm_yn, hm_yn,
                    ma_loai_pt, tg_giao,
                    ma_qg_giao, ma_tinh_giao, ma_quan_giao, ma_phuong_giao,
                    ma_qg, ma_tinh, ma_quan, ma_phuong,
                    lh_yn, bien_so_lh, ten_nguoi_lh, sdt_lh, cccd_lh, chuc_vu_lh
                    
                ) VALUES (
                    @stt_rec, @ma_dvcs, 'DXB', '1', '', NULL, '', '1',
                    @ngay_lct, @ngay_ct, @so_ct,
                    'VND', 1,
                    @ong_ba, @ma_kh, @dien_giai,
                    '', '',
                    @t_so_luong,
                    @t_tien_nt, @t_tien, @t_tien2, @t_tien_nt2,
                    0, 0,
                    @t_tt_nt, @t_tt,
                    '',
                    @t_ck, @t_ck_nt,
                    0, 0, 0, 0, 0, 0, 0, 0,
                    NULL,
                    '', '',
                    '', '', '', NULL, NULL, NULL,
                    '', 0, '',
                    0,
                    0, 0,
                    '', '0',
                    GETDATE(), GETDATE(), @user_id, @user_id,
                    '', '', '', '', '', '', '', '',
                    '', '', '', '',
                    '', '', '', NULL, NULL, NULL, 0, 0, 0,
                    '', '', '',
                    '', '', 'HOHO', 0, 0, 0, NULL, NULL, NULL,
                    '', 0,
                    0, 0, 0,
                    '', '', '', NULL, NULL, NULL,
                    0, '',
                    0, 0, 0,
                    0, 0,
                    0, @ghi_chu,
                    0, 0, 0,
                    @ma_httt, 0, '', '', '', 0, 0,
                    '', '',
                    '', '', '', '',
                    '', '', '', '',
                    '', '', '', @isSDF, @test2, @test1
                )";

            using (var cmd = new SqlCommand(sql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@stt_rec", sttRec);
                cmd.Parameters.AddWithValue("@ma_dvcs", "CTY");
                cmd.Parameters.AddWithValue("@loai_ct", 1);
                cmd.Parameters.AddWithValue("@test1", orderDetail.Status);
                cmd.Parameters.AddWithValue("@test2", orderDetail.Status_Name);
                cmd.Parameters.AddWithValue("@isSDF", isSDF ? 1 : 0);
                cmd.Parameters.Add(new SqlParameter("@so_ct", SqlDbType.Int) { Value = int.Parse(soCt) });
                cmd.Parameters.Add(new SqlParameter("@ngay_ct", SqlDbType.Date) { Value = ngayCt.Date });
                cmd.Parameters.Add(new SqlParameter("@ngay_lct", SqlDbType.Date) { Value = ngayCt.Date });
                cmd.Parameters.Add(new SqlParameter("@ngay_hl", SqlDbType.DateTime) { Value = DBNull.Value });
                cmd.Parameters.Add(new SqlParameter("@ngay_ct0", SqlDbType.DateTime) { Value = DBNull.Value });
                cmd.Parameters.Add(new SqlParameter("@ngay_ct2", SqlDbType.DateTime) { Value = DBNull.Value });
                cmd.Parameters.Add(new SqlParameter("@ngay_ct3", SqlDbType.DateTime) { Value = DBNull.Value });

                cmd.Parameters.AddWithValue("@so_lo", DBNull.Value);
                cmd.Parameters.AddWithValue("@ngay_lo", DBNull.Value);
                cmd.Parameters.AddWithValue("@ma_nk", "");
                cmd.Parameters.AddWithValue("@ma_gia", "");
                cmd.Parameters.AddWithValue("@so_hd0", "");
                cmd.Parameters.AddWithValue("@stt_rec_hd0", "");
                cmd.Parameters.AddWithValue("@so_ct0", "");
                cmd.Parameters.AddWithValue("@ma_nvbh", "");
                cmd.Parameters.AddWithValue("@so_ddh", "");
                cmd.Parameters.AddWithValue("@so_hd", "");
                cmd.Parameters.AddWithValue("@so_dt", "");
                cmd.Parameters.AddWithValue("@so_po", "");
                cmd.Parameters.AddWithValue("@so_bg", "");
                cmd.Parameters.AddWithValue("@ma_dc", "");
                cmd.Parameters.AddWithValue("@ma_nx", "");
                cmd.Parameters.AddWithValue("@tk", "");
                cmd.Parameters.AddWithValue("@ma_tt", "");
                cmd.Parameters.AddWithValue("@ma_htvc", "");
                cmd.Parameters.AddWithValue("@xtag", DBNull.Value);

                cmd.Parameters.AddWithValue("@ma_kh", maKh);
                cmd.Parameters.AddWithValue("@ong_ba", shipperName);
                cmd.Parameters.Add(new SqlParameter("@t_so_luong", SqlDbType.Decimal) { Value = totalQuantity, Precision = 18, Scale = 2 });
                cmd.Parameters.Add(new SqlParameter("@t_tien_nt", SqlDbType.Decimal) { Value = tTienNt, Precision = 18, Scale = 2 });
                cmd.Parameters.Add(new SqlParameter("@t_tien", SqlDbType.Decimal) { Value = tTien, Precision = 18, Scale = 2 });
                cmd.Parameters.Add(new SqlParameter("@nam", SqlDbType.Int) { Value = ngayCt.Year });
                cmd.Parameters.Add(new SqlParameter("@ky", SqlDbType.Int) { Value = ngayCt.Month });
                cmd.Parameters.Add(new SqlParameter("@t_tien_nt2", SqlDbType.Decimal) { Value = tTienNt, Precision = 18, Scale = 2 });
                cmd.Parameters.Add(new SqlParameter("@t_tien2", SqlDbType.Decimal) { Value = tTien, Precision = 18, Scale = 2 });
                cmd.Parameters.Add(new SqlParameter("@t_ck_nt", SqlDbType.Decimal) { Value = tCkNt, Precision = 18, Scale = 2 });
                cmd.Parameters.Add(new SqlParameter("@t_ck", SqlDbType.Decimal) { Value = tCk, Precision = 18, Scale = 2 });
                cmd.Parameters.Add(new SqlParameter("@t_tt_nt", SqlDbType.Decimal) { Value = tTtNt, Precision = 18, Scale = 2 });
                cmd.Parameters.Add(new SqlParameter("@t_tt", SqlDbType.Decimal) { Value = tTt, Precision = 18, Scale = 2 });

                var dienGiai = $"Đơn #{orderDetail.Order_Code ?? orderDetail.Id?.ToString() ?? ""}";
                if (!string.IsNullOrEmpty(orderDetail.Shipping_Unit))
                {
                    dienGiai += $" | VT: {orderDetail.Shipping_Unit.Trim()}";
                }
                if (!string.IsNullOrEmpty(orderDetail.Creator?.Name))
                {
                    dienGiai += $" | {orderDetail.Creator.Name.Trim()}";
                }
                if (!string.IsNullOrEmpty(orderDetail.Shipping_Address?.Full_Address))
                {
                    dienGiai += $" | {orderDetail.Shipping_Address.Full_Address.Trim()}";
                }
                if (!string.IsNullOrEmpty(orderDetail.Partner.Extend_Code))
                {
                    dienGiai += $" | {orderDetail.Partner.Extend_Code.Trim()}";
                }
                cmd.Parameters.AddWithValue("@dien_giai", dienGiai.Length > 255 ? dienGiai.Substring(0, 255) : dienGiai);

                var ghiChu = $"PanCake Order #{orderDetail.Order_Code ?? orderDetail.Id?.ToString() ?? ""}, Shop: {shopId}";
                if (!string.IsNullOrEmpty(orderDetail.Status_Name))
                {
                    ghiChu += $", Trạng thái: {orderDetail.Status_Name}";
                }
                cmd.Parameters.AddWithValue("@ghi_chu", ghiChu.Length > 1024 ? ghiChu.Substring(0, 1024) : ghiChu);

                cmd.Parameters.Add(new SqlParameter("@user_id", SqlDbType.Int) { Value = 0 });
                cmd.Parameters.AddWithValue("@fcode3", fcode3);
                cmd.Parameters.AddWithValue("@ma_httt", GetPaymentType(orderDetail.Cod, orderDetail.Transfer_Money, orderDetail.Prepaid));

                // Geographic fields - ensure they are properly typed
                cmd.Parameters.AddWithValue("@ma_qg_giao", geoInfo?.MaQg ?? "VN");
                cmd.Parameters.AddWithValue("@ma_tinh_giao", geoInfo?.MaTinh ?? "");
                cmd.Parameters.AddWithValue("@ma_quan_giao", geoInfo?.MaQuan ?? "");
                cmd.Parameters.AddWithValue("@ma_phuong_giao", geoInfo?.MaPhuong ?? "");
                cmd.Parameters.AddWithValue("@ma_qg", "VN");
                cmd.Parameters.AddWithValue("@ma_tinh", geoInfo?.MaTinh ?? "");
                cmd.Parameters.AddWithValue("@ma_quan", geoInfo?.MaQuan ?? "");
                cmd.Parameters.AddWithValue("@ma_phuong", geoInfo?.MaPhuong ?? "");

                // DEBUG: Log all parameters
                _logger.LogWarning(">>> DXB PARAMS: stt_rec={SttRec}, so_ct={SoCt}, ngay_ct={NgayCt}, ma_kh={MaKh}, t_so_luong={TSoLuong}, t_tien_nt={TTienNt}",
                    sttRec, soCt, ngayCt.Date, $"KHLE{shopId}", totalQuantity, tTienNt);
                _logger.LogWarning(">>> DXB PARAMS2: ma_qg={MaQg}, ma_tinh={MaTinh}, ma_quan={MaQuan}, ma_phuong={MaPhuong}, ma_httt={MaHttt}",
                    "VN", geoInfo?.MaTinh ?? "(null)", geoInfo?.MaQuan ?? "(null)", geoInfo?.MaPhuong ?? "(null)",orderDetail.Payment_Method);
                _logger.LogWarning(">>> DXB PARAMS3: ma_gia={MaGia}, ma_nx={MaNx}, tk={Tk}, ma_tt={MaTt}, ma_dc={MaDc}",
                    "", "", "", "", "");

                // Log all parameter types and values before execution
                _logger.LogWarning(">>> PARAM TYPES: so_ct type={SoCtType}, t_so_luong type={TSoLuongType}, t_tien_nt type={TTienNtType}, nam type={NamType}, ky type={KyType}",
                    int.Parse(soCt).GetType().Name, totalQuantity.GetType().Name, tTienNt.GetType().Name, ngayCt.Year.GetType().Name, ngayCt.Month.GetType().Name);

                _logger.LogError("DXB_INSERT_SQL: {Sql}", sql);
                await cmd.ExecuteNonQueryAsync();
            }

            _logger.LogInformation("Đã insert m65${Partition}", partition);
        }

        /// <summary>
        /// Insert vào m760$YYYYMM (SDF - Trả hàng/Trả lại)
        /// Bao gồm: geographic fields, ong_ba (shipper), fcode3 (đơn vị vận chuyển)
        /// ma_gd=1, tk=131111 theo yêu cầu
        /// </summary>
        private async Task InsertSDFMasterRecordAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            string sttRec,
            string soCt,
            DateTime ngayCt,
            string partition,
            int shopId,
            PanCakeOrderDetail orderDetail,
            Data.Entities.DmkbApiEntity shopConfig,string ma_kh)
        {
            var tableName = $"m760${partition}";
            
            decimal totalQuantity = 0;
            decimal totalAmount = 0;
            if (orderDetail.Items != null && orderDetail.Items.Count > 0)
            {
                foreach (var item in orderDetail.Items)
                {
                    var quantity = item.Quantity ?? 0;
                    var price = item.Variation_Info?.Retail_Price ?? item.Price ?? 0;
                    totalQuantity += quantity;
                    totalAmount += quantity * price;
                }
            }

            var tTienNt = orderDetail.Total_Price ?? totalAmount;
            var tTien = tTienNt;
            var tCkNt = orderDetail.Total_Discount ?? 0;
            var tCk = tCkNt;
            var tTtNt = tTienNt - tCkNt;
            var tTt = tTien - tCk;

            var geoInfo = await _geographicService.LookupAsync(
                orderDetail.Shipping_Address?.Province_Name ?? "",
                orderDetail.Shipping_Address?.District_Name ?? "",
                orderDetail.Shipping_Address?.Commune_Name ?? "");

            var shipperName = orderDetail.Shipping_Address?.Full_Name ?? "";
            if (shipperName.Length > 24) shipperName = shipperName.Substring(0, 24);

            var fcode3 = orderDetail.Shipping_Unit ?? "";

            var dienGiai = $"Trả hàng #{orderDetail.Order_Code ?? orderDetail.Id?.ToString() ?? "UNKNOWN"}";
            if (!string.IsNullOrEmpty(orderDetail.Shipping_Unit))
            {
                dienGiai += $" | VT: {orderDetail.Shipping_Unit.Trim()}";
            }
            if (!string.IsNullOrEmpty(orderDetail.Creator?.Name))
            {
                dienGiai += $" | {orderDetail.Creator.Name.Trim()}";
            }
            if (!string.IsNullOrEmpty(orderDetail.Shipping_Address?.Full_Address))
            {
                dienGiai += $" | {orderDetail.Shipping_Address.Full_Address.Trim()}";
            }
            if (!string.IsNullOrEmpty(orderDetail.Status_Name))
            {
                dienGiai += $" | Status: {orderDetail.Status_Name}";
            }
            if (!string.IsNullOrEmpty(orderDetail.Partner.Extend_Code))
            {
                dienGiai += $" | {orderDetail.Partner.Extend_Code.Trim()}";
            }

            var ghiChu = $"PanCake Order Trả hàng #{orderDetail.Order_Code ?? orderDetail.Id?.ToString() ?? "UNKNOWN"}, Shop: {shopId}";
            if (!string.IsNullOrEmpty(orderDetail.Status_Name))
            {
                ghiChu += $", Trạng thái: {orderDetail.Status_Name}";
            }

            var sql = $@"
                INSERT INTO {tableName} (
                      stt_rec, ma_dvcs, ma_ct, loai_ct, so_lo, ngay_lo, ma_nk, ma_gd, 
    ngay_lct, ngay_ct, so_ct,
    ma_nt, ty_gia,
    ong_ba, ma_kh, dien_giai,
    ma_nx, tk,
    t_so_luong,
    t_tien_nt, t_tien, t_tien2, t_tien_nt2,
    t_thue_nt, t_thue,
    t_tt_nt, t_tt,
    ma_tt,
    t_tien_km_nt, t_tien_km,
    t_thue_km_nt, t_thue_km,
    t_km_nt, t_km,
    t_tc_tien_nt2, t_tc_tien2, t_tc_thue_nt, t_tc_thue, t_tc_tt_nt, t_tc_tt,
    t_ck, t_ck_nt,
    t_cp_bh, t_cp_bh_nt, t_cp_vc, t_cp_vc_nt, t_cp_khac, t_cp_khac_nt, t_cp, t_cp_nt,
    ma_nvbh, ck_thue_yn, ma_gia,
    ten_vtthue, ghi_chuthue, tk_ck,
    so_seri, ma_thue, thue_suat, tk_thue_no, tk_thue_co,
    ma_kh2, so_ct0, ngay_ct0, tt_yn,
    nam, ky, xtag, status,
    datetime0, datetime2, user_id0, user_id2
                ) VALUES (                  
                     @stt_rec, @ma_dvcs,'SDF',1,'', @ngay_lo,'','1', 
    @ngay_lct, @ngay_ct, @so_ct,
    'VND',1,
    @ong_ba, @ma_kh, @dien_giai,
    '','13111',
    @t_so_luong,
    @t_tien_nt, @t_tien, @t_tien2, @t_tien_nt2,
    0, 0,
    @t_tt_nt, @t_tt,
    '',
    0, 0,
    0, 0,
    0, 0,
    0, 0,0,0,0,0,
    @t_ck, @t_ck_nt,
    0,0, 0, 0, 0, 0, 0, 0,
    '', 0, '',
    '', '', '',
    '', '', 0, '', '',
    '', '', '', 0,
    0, 0, '', '0',
    GETDATE(), GETDATE(), '0', '0'
                )";

            using (var cmd = new SqlCommand(sql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@stt_rec", sttRec);
                cmd.Parameters.AddWithValue("@ma_dvcs", "CTY");
                cmd.Parameters.AddWithValue("@loai_ct", 1);
                cmd.Parameters.AddWithValue("@so_ct", int.Parse(soCt));
                cmd.Parameters.AddWithValue("@ngay_ct", ngayCt.Date);
                cmd.Parameters.AddWithValue("@ngay_lct", ngayCt.Date);
                cmd.Parameters.AddWithValue("@so_lo", DBNull.Value);
                cmd.Parameters.AddWithValue("@ngay_lo", DBNull.Value);
                cmd.Parameters.AddWithValue("@ong_ba", shipperName);
                cmd.Parameters.AddWithValue("@ma_kh", ma_kh);
                cmd.Parameters.AddWithValue("@t_so_luong", totalQuantity);
                cmd.Parameters.AddWithValue("@t_tien_nt", tTienNt);
                cmd.Parameters.AddWithValue("@t_tien", tTien);
                cmd.Parameters.AddWithValue("@t_tien_nt2", tTienNt);
                cmd.Parameters.AddWithValue("@t_tien2", tTien);
                cmd.Parameters.AddWithValue("@t_ck_nt", tCkNt);
                cmd.Parameters.AddWithValue("@t_ck", tCk);
                cmd.Parameters.AddWithValue("@t_tt_nt", tTtNt);
                cmd.Parameters.AddWithValue("@t_tt", tTt);
                cmd.Parameters.AddWithValue("@dien_giai", dienGiai.Length > 255 ? dienGiai.Substring(0, 255) : dienGiai);
                cmd.Parameters.AddWithValue("@ghi_chu", ghiChu.Length > 1024 ? ghiChu.Substring(0, 1024) : ghiChu);
                cmd.Parameters.AddWithValue("@user_id", 0);
                //cmd.Parameters.AddWithValue("@nam", ngayCt.Year);
                //cmd.Parameters.AddWithValue("@ky", ngayCt.Month);
                

                await cmd.ExecuteNonQueryAsync();
            }

            _logger.LogInformation("Đã insert m760${Partition}", partition);
        }

        /// <summary>
        /// Insert vào d65$YYYYMM (DXB - Xuất bán) - có ma_httt
        /// </summary>
        private async Task InsertDetailRecordsAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            string sttRec,
            string soCt,
            DateTime ngayCt,
            string partition,
            int shopId,
            PanCakeOrderDetail orderDetail,
            Data.Entities.DmkbApiEntity shopConfig)
        {
            var tableName = $"d65${partition}";
            int lineNumber = 0;

            //var maHttt = GetPaymentType(orderDetail.Cod, orderDetail.Transfer_Money, orderDetail.Prepaid);

            foreach (var item in orderDetail.Items!)
            {
                lineNumber++;

                var sql = $@"
                    INSERT INTO {tableName} (
                        stt_rec, stt_rec0, ma_ct, ngay_ct, so_ct, ma_vt,
                        ma_sp, ma_bp, so_lsx, dvt, he_so,
                        ma_kho, ma_vi_tri, ma_lo, ma_vv, tk_vt,
                        so_luong, gia_nt, gia, gia_nt2, gia2,
                        tien_nt, tien, tien_nt2, tien2,
                        ma_thue, tk_thue, thue_suat, thue, thue_nt,
                        tt, tt_nt,
                        xstatus, xaction,
                        cp_bh, cp_bh_nt, cp_vc, cp_vc_nt, cp_khac, cp_khac_nt, cp, cp_nt,
                        stt_rec_ct, stt_rec0ct,
                        gia_ban0, gia_ban_nt0, gia_ban, gia_ban_nt,
                        gia_ck, gia_ck_nt, ck_tl, ck, ck_nt,
                        sl_dh, ngay_giao, sl_min, sl_max, sl_xuat, sl_giao, sl_tl, sl_hd, sl_tl0,
                        stt_rec_bg, stt_rec0bg,
                        stt_rec_hd, stt_rec0hd,
                        ma_nvbh_i, line_nbr,
                        ma_hd, ma_ku, ma_phi, so_dh_i,
                        ma_td1, ma_td2, ma_td3, sl_td1, sl_td2, sl_td3,
                        ngay_td1, ngay_td2, ngay_td3,
                        gc_td1, gc_td2, gc_td3,
                        s1, s2, s3, s4, s5, s6, s7, s8, s9,
                        lai_nt, lai, ti_suat, gia_gui_nt, gia_gui, sl_gui,
                        stt_rec_so, stt_rec0so, stt_rec0sq, stt_rec_sq,
                        tl_gui, tl_ck,
                        cm_yn, tk_cpcm, km_yn,
                        tien_gui, tien_gui_nt,
                        ten_vt2, quy_cach, ngay_giao_tu,
                        stt_rec_pxgui,
                        kcs_ch, kho_ch, sx_ch,
                        cuoc_boc_xep_yn, cuoc_bom_rot_yn, cuoc_luu_ca_yn, so_ngay_luu_ca,
                        ghep_diem_yn, so_diem_ghep,
                        so_kcs, stt_rec_kcs, stt_rec_sx, stt_rec_kho,
                        ma_combo, vat_po, ten_vt3
                    ) VALUES (
                        @stt_rec, @stt_rec0, 'DXB', @ngay_ct, @so_ct, @ma_vt,
                        '', '', '', @dvt, 1,
                        @ma_kho, '', '', '', '',
                        @so_luong, @gia_nt, @gia, @gia_nt2, @gia2,
                        @tien_nt, @tien, @tien_nt2, @tien2,
                        '', '', 0, 0, 0,
                        0, 0,
                        '', '',
                        0, 0, 0, 0, 0, 0, 0, 0,
                        '', '',
                        @gia_ban0, @gia_ban_nt0, @gia_ban, @gia_ban_nt,
                        0, 0, 0, 0, 0,
                        0, NULL, 0, 0, 0, 0, 0, 0, 0,
                        '', '',
                        '', '',
                        '', @line_nbr,
                        '', '', '', '',
                        '', '', '', 0, 0, 0,
                        NULL, NULL, NULL,
                        '', '', '',
                        '', '', '', 0, 0, 0, NULL, NULL, NULL,
                        0, 0, 0, 0, 0, 0,
                        '', '', '', '',
                        0, 0,
                        0, '', 0,
                        0, 0,
                        @ten_vt, '', NULL,
                        '',
                        0, 0, 0,
                        0, 0, 0, 0,
                        0, 0,
                        '', '', '', '',
                        '', '', ''
                    )";

                using (var cmd = new SqlCommand(sql, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@stt_rec", sttRec);
                    cmd.Parameters.AddWithValue("@stt_rec0", lineNumber.ToString("000"));
                    cmd.Parameters.AddWithValue("@so_ct", int.Parse(soCt));
                    cmd.Parameters.AddWithValue("@ngay_ct", ngayCt);
                    cmd.Parameters.AddWithValue("@line_nbr", lineNumber);

                    var productId = item.Product_Id ?? item.Variation_Id ?? "";
                    var productName = item.Variation_Info?.Name ?? "Sản phẩm PanCake";
                    var barcode = item.Variation_Info?.Barcode ?? "";

                    var maVt = await FindOrCreateProductCodeAsync(connection, transaction, productId, barcode, productName);

                    cmd.Parameters.AddWithValue("@ma_vt", maVt);
                    cmd.Parameters.AddWithValue("@dvt", "Cái");
                    cmd.Parameters.AddWithValue("@ma_kho", shopConfig.MaKhoDh ?? "KH01");
                   

                    var quantity = item.Quantity ?? 0;
                    var price = item.Variation_Info?.Retail_Price ?? item.Price ?? 0;
                    var totalPrice = quantity * price;

                    cmd.Parameters.AddWithValue("@so_luong", quantity);
                    cmd.Parameters.AddWithValue("@gia_nt", price);
                    cmd.Parameters.AddWithValue("@gia", price);
                    cmd.Parameters.AddWithValue("@gia_nt2", price);
                    cmd.Parameters.AddWithValue("@gia2", price);
                    cmd.Parameters.AddWithValue("@tien_nt", totalPrice);
                    cmd.Parameters.AddWithValue("@tien", totalPrice);
                    cmd.Parameters.AddWithValue("@tien_nt2", totalPrice);
                    cmd.Parameters.AddWithValue("@tien2", totalPrice);
                    cmd.Parameters.AddWithValue("@gia_ban0", price);
                    cmd.Parameters.AddWithValue("@gia_ban_nt0", price);
                    cmd.Parameters.AddWithValue("@gia_ban", price);
                    cmd.Parameters.AddWithValue("@gia_ban_nt", price);
                    cmd.Parameters.AddWithValue("@ten_vt", productName.Length > 128 ? productName.Substring(0, 128) : productName);

                    await cmd.ExecuteNonQueryAsync();
                }
            }

            _logger.LogInformation("Đã insert {Count} items vào {TableName}", lineNumber, tableName);
        }

        /// <summary>
        /// Insert vào d760$YYYYMM (SDF - Trả hàng) - có ma_httt
        /// </summary>
        private async Task InsertSDFDetailRecordsAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            string sttRec,
            string soCt,
            DateTime ngayCt,
            string partition,
            int shopId,
            PanCakeOrderDetail orderDetail,
            Data.Entities.DmkbApiEntity shopConfig)
        {
            var tableName = $"d760${partition}";
            int lineNumber = 0;

            //var maHttt = GetPaymentType(orderDetail.Cod, orderDetail.Transfer_Money, orderDetail.Prepaid);

            foreach (var item in orderDetail.Items!)
            {
                lineNumber++;

                var sql = $@"
                    INSERT INTO {tableName} (
                        stt_rec, stt_rec0, ma_ct, ngay_ct, so_ct, ma_vt,
    ma_sp, ma_bp, so_lsx, dvt, he_so,
    ma_kho, ma_vi_tri, ma_lo, ma_vv, tk_vt,
    so_luong, gia_nt, gia, gia_nt2, gia2,
    tien_nt, tien, tien_nt2, tien2,
    thue, thue_nt,
    tt, tt_nt,
    xstatus, xaction,
    cp_bh, cp_bh_nt, cp_vc, cp_vc_nt, cp_khac, cp_khac_nt, cp, cp_nt,
    stt_rec_ct, stt_rec0ct,
    gia_ban0, gia_ban_nt0, gia_ban, gia_ban_nt,
    gia_ck, gia_ck_nt, ck, ck_nt,
    sl_dh, ngay_giao, 
    stt_rec_hd, stt_rec0hd,
    ma_nvbh_i, line_nbr,
    ma_hd, ma_ku, ma_phi, so_dh_i,
    ma_td1, ma_td2, ma_td3, sl_td1, sl_td2, sl_td3,
    ngay_td1, ngay_td2, ngay_td3,
    gc_td1, gc_td2, gc_td3,
    s1, s2, s3, s4, s5, s6, s7, s8, s9,
    cuoc_boc_xep_yn, cuoc_bom_rot_yn, cuoc_luu_ca_yn, so_ngay_luu_ca,
    ghep_diem_yn, so_diem_ghep,
    so_kcs, stt_rec_kcs,
    kcs_ch, km_yn
                    ) VALUES (
                        @stt_rec, @stt_rec0, 'SDF', @ngay_ct, @so_ct, @ma_vt,
    '', '', '', @dvt, 1,
    @ma_kho, '', '', '', '',
    @so_luong, @gia_nt, @gia, @gia_nt2, @gia2,
    @tien_nt, @tien, @tien_nt2, @tien2,
    0, 0,
    0, 0,
    '', '',
    0, 0, 0, 0, 0, 0, 0, 0,
    '', '',
    @gia_ban0, @gia_ban_nt0, @gia_ban, @gia_ban_nt,
    0, 0, 0, 0,
    0, NULL, 
    '', '',
    '', @line_nbr,
    '', '', '', '',
    '', '', '', 0, 0, 0,
    NULL, NULL, NULL,
    '', '', '',
    '', '', '', 0, 0, 0, NULL, NULL, NULL,
    0, 0, 0, 0,
    0, 0,
    '', '',
    0, 0
                    )";

                using (var cmd = new SqlCommand(sql, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@stt_rec", sttRec);
                    cmd.Parameters.AddWithValue("@stt_rec0", lineNumber.ToString("000"));
                    cmd.Parameters.AddWithValue("@so_ct", int.Parse(soCt));
                    cmd.Parameters.AddWithValue("@ngay_ct", ngayCt);
                    cmd.Parameters.AddWithValue("@line_nbr", lineNumber);

                    var productId = item.Product_Id ?? item.Variation_Id ?? "";
                    var productName = item.Variation_Info?.Name ?? "Sản phẩm PanCake";
                    var barcode = item.Variation_Info?.Barcode ?? "";

                    var maVt = await FindOrCreateProductCodeAsync(connection, transaction, productId, barcode, productName);

                    cmd.Parameters.AddWithValue("@ma_vt", maVt);
                    cmd.Parameters.AddWithValue("@dvt", "Cái");
                    cmd.Parameters.AddWithValue("@ma_kho", shopConfig.MaKhoDh ?? "KH01");
                    

                    var quantity = item.Quantity ?? 0;
                    var price = item.Variation_Info?.Retail_Price ?? item.Price ?? 0;
                    var totalPrice = quantity * price;

                    cmd.Parameters.AddWithValue("@so_luong", quantity);
                    cmd.Parameters.AddWithValue("@gia_nt", price);
                    cmd.Parameters.AddWithValue("@gia", price);
                    cmd.Parameters.AddWithValue("@gia_nt2", price);
                    cmd.Parameters.AddWithValue("@gia2", price);
                    cmd.Parameters.AddWithValue("@tien_nt", totalPrice);
                    cmd.Parameters.AddWithValue("@tien", totalPrice);
                    cmd.Parameters.AddWithValue("@tien_nt2", totalPrice);
                    cmd.Parameters.AddWithValue("@tien2", totalPrice);
                    cmd.Parameters.AddWithValue("@gia_ban0", price);
                    cmd.Parameters.AddWithValue("@gia_ban_nt0", price);
                    cmd.Parameters.AddWithValue("@gia_ban", price);
                    cmd.Parameters.AddWithValue("@gia_ban_nt", price);
                    cmd.Parameters.AddWithValue("@ten_vt", productName.Length > 128 ? productName.Substring(0, 128) : productName);

                    await cmd.ExecuteNonQueryAsync();
                }
            }

            _logger.LogInformation("Đã insert {Count} items vào {TableName}", lineNumber, tableName);
        }

        private async Task<string> FindOrCreateProductCodeAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            string productId,
            string barcode,
            string productName)
        {
            var sql = "SELECT TOP 1 ma_vt FROM dmvt WHERE barcode = @barcode OR CAST(ma_vt AS NVARCHAR(100)) = @product_id";
            using (var cmd = new SqlCommand(sql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@barcode", barcode);
                cmd.Parameters.AddWithValue("@product_id", productId);

                var result = await cmd.ExecuteScalarAsync();
                if (result != null)
                {
                    return result.ToString()!;
                }
            }

            return $"PKE{productId.Substring(0, Math.Min(10, productId.Length))}";
        }
    }
}
