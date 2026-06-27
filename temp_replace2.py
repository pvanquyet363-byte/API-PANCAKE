import sys

path = r'D:\API_PanCake2\API_PanCake\API_PanCake\Services\OrderSyncService.cs'
with open(path, 'r', encoding='utf-8', newline='') as f:
    content = f.read()

# ============================================
# FIX 1: Column list - fix t_tien2/t_tien_nt2 order, remove tien_tu_nt, fix ngay_hl line
# ============================================
old_col_block = "stt_rec, ma_dvcs, ma_ct, loai_ct, so_lo, ngay_lo, ma_nk, ma_gd,\r\n                    ngay_lct, ngay_ct, so_ct,\r\n                    ma_nt, ty_gia,\r\n                    ong_ba, ma_kh, dien_giai,\r\n                    ma_nx, tk,\r\n                    t_so_luong,\r\n                    t_tien_nt, t_tien, t_tien2, t_tien_nt2,\r\n                    t_thue_nt, t_thue,\r\n                    t_tt_nt, t_tt,\r\n                    ma_tt,\r\n                    t_ck, t_ck_nt,\r\n                    t_cp_bh, t_cp_bh_nt, t_cp_vc, t_cp_vc_nt, t_cp_khac, t_cp_khac_nt, t_cp, t_cp_nt,\r\n                    ngay_hl,\r\n                    ma_dc, ma_htvc,\r\n                    so_hd0, stt_rec_hd0, so_ct0, ngay_ct0, ngay_ct2, ngay_ct3,\r\n                    ma_nvbh, ck_thue_yn, ma_gia,\r\n                    tien_hd,\r\n                    nam, ky,\r\n                    xtag, status,\r\n                    datetime0, datetime2, user_id0, user_id2,\r\n                    contract_id, bcontract_id, fee_id, so_ddh, so_hd, so_dt, so_po, so_bg,\r\n                    job_id, prd_id, dept_id, mo_nbr,\r\n                    fcode1, fcode2, fcode3, fdate1, fdate2, fdate3, fqty1, fqty2, fqty3,\r\n                    fnote1, fnote2, fnote3,\r\n                    s1, s2, s3, s4, s5, s6, s7, s8, s9,\r\n                    dd_giaonhan, uu_tien,\r\n                    tile_tu, tien_tu, tien_tu_nt,\r\n                    ma_qly_d"

new_col_block = "stt_rec, ma_dvcs, ma_ct, loai_ct, so_lo, ngay_lo, ma_nk, ma_gd,\r\n                    ngay_lct, ngay_ct, so_ct,\r\n                    ma_nt, ty_gia,\r\n                    ong_ba, ma_kh, dien_giai,\r\n                    ma_nx, tk,\r\n                    t_so_luong,\r\n                    t_tien_nt, t_tien, t_thue_nt, t_thue,\r\n                    t_tt_nt, t_tt,\r\n                    ma_tt,\r\n                    t_tien2, t_tien_nt2, t_ck, t_ck_nt,\r\n                    t_cp_bh, t_cp_bh_nt, t_cp_vc, t_cp_vc_nt, t_cp_khac, t_cp_khac_nt, t_cp, t_cp_nt,\r\n                    ngay_hl, ma_dc, ma_htvc,\r\n                    so_hd0, stt_rec_hd0, so_ct0, ngay_ct0, ngay_ct2, ngay_ct3,\r\n                    ma_nvbh, ck_thue_yn, ma_gia, tien_hd,\r\n                    nam, ky, xtag, status,\r\n                    datetime0, datetime2, user_id0, user_id2,\r\n                    contract_id, bcontract_id, fee_id, so_ddh, so_hd, so_dt, so_po, so_bg,\r\n                    job_id, prd_id, dept_id, mo_nbr,\r\n                    fcode1, fcode2, fcode3, fdate1, fdate2, fdate3, fqty1, fqty2, fqty3,\r\n                    fnote1, fnote2, fnote3,\r\n                    s1, s2, s3, s4, s5, s6, s7, s8, s9,\r\n                    dd_giaonhan, uu_tien,\r\n                    tile_tu, tien_tu, ma_qly_d"

if old_col_block in content:
    print("Found column block!")
    content = content.replace(old_col_block, new_col_block, 1)
else:
    print("Column block NOT FOUND")

# ============================================
# FIX 2: Add @nam and @ky parameters
# ============================================
# Add after cmd.Parameters.AddWithValue("@t_tien", tTien);
old_param = 'cmd.Parameters.AddWithValue("@t_tien", tTien);\r\n                cmd.Parameters.AddWithValue("@t_tien_nt2", tTienNt);\r\n                cmd.Parameters.AddWithValue("@t_tien2", tTien);'
new_param = 'cmd.Parameters.AddWithValue("@t_tien", tTien);\r\n                cmd.Parameters.AddWithValue("@nam", ngayCt.Year);\r\n                cmd.Parameters.AddWithValue("@ky", ngayCt.Month);\r\n                cmd.Parameters.AddWithValue("@t_tien_nt2", tTienNt);\r\n                cmd.Parameters.AddWithValue("@t_tien2", tTien);'

if old_param in content:
    print("Found param block!")
    content = content.replace(old_param, new_param, 1)
else:
    print("Param block NOT FOUND")

with open(path, 'w', encoding='utf-8', newline='') as f:
    f.write(content)
print("Done!")
