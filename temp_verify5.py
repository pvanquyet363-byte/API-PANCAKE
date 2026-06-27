import re

path = r'D:\API_PanCake2\API_PanCake\API_PanCake\Services\OrderSyncService.cs'
with open(path, 'r', encoding='utf-8', newline='') as f:
    content = f.read()

start = content.find('var sql = $@"')
end = content.find('");', start) + 2
sql_block = content[start:end]

# The VALUES section starts at ") VALUES (" and ends at the closing ) before ");
# Find the last ")"; in the block - the VALUES clause ends right before it
last_val_close = sql_block.rfind(')";')
values_text = sql_block[sql_block.find(') VALUES (') + 10:last_val_close]

print(f"Values ends with: {repr(sql_block[last_val_close-10:last_val_close+10])}")

# Now count values by line - properly handle commas inside strings
lines = [l.strip() for l in values_text.split('\n') if l.strip() and l.strip() != ')']
total = 0
for line in lines:
    # Count commas not inside strings
    count = 0
    in_str = False
    str_char = None
    i = 0
    while i < len(line):
        ch = line[i]
        if in_str:
            if ch == str_char and i+1 < len(line) and line[i+1] == str_char:
                i += 2
                continue
            elif ch == str_char:
                in_str = False
                str_char = None
            i += 1
            continue
        if ch in "('\"":
            in_str = True
            str_char = ch
        elif ch == ',':
            count += 1
        i += 1
    # Number of values = number of commas + 1
    vals_on_line = count + 1
    total += vals_on_line
    print(f"  {vals_on_line:2d}: {line}")

print(f"\nTOTAL VALUES: {total}")

# Schema
schema = [
    "stt_rec", "ma_dvcs", "ma_ct", "loai_ct", "so_lo", "ngay_lo", "ma_nk", "ma_gd",
    "ngay_lct", "ngay_ct", "so_ct",
    "ma_nt", "ty_gia",
    "ong_ba", "ma_kh", "dien_giai",
    "ma_nx", "tk",
    "t_so_luong",
    "t_tien_nt", "t_tien", "t_thue_nt", "t_thue", "t_tt_nt", "t_tt",
    "ma_tt",
    "t_tien2", "t_tien_nt2", "t_ck", "t_ck_nt",
    "t_cp_bh", "t_cp_bh_nt", "t_cp_vc", "t_cp_vc_nt", "t_cp_khac", "t_cp_khac_nt", "t_cp", "t_cp_nt",
    "ngay_hl", "ma_dc", "ma_htvc",
    "so_hd0", "stt_rec_hd0", "so_ct0", "ngay_ct0", "ngay_ct2", "ngay_ct3",
    "ma_nvbh", "ck_thue_yn", "ma_gia", "tien_hd",
    "nam", "ky", "xtag", "status",
    "datetime0", "datetime2", "user_id0", "user_id2",
    "contract_id", "bcontract_id", "fee_id", "so_ddh", "so_hd", "so_dt", "so_po", "so_bg",
    "job_id", "prd_id", "dept_id", "mo_nbr",
    "fcode1", "fcode2", "fcode3", "fdate1", "fdate2", "fdate3", "fqty1", "fqty2", "fqty3",
    "fnote1", "fnote2", "fnote3",
    "s1", "s2", "s3", "s4", "s5", "s6", "s7", "s8", "s9",
    "dd_giaonhan", "uu_tien",
    "tile_tu", "tien_tu", "ma_qly_d", "ma_ksnb_d", "ma_giamdoc_d", "ngay_qly_d", "ngay_ksnb_d", "ngay_giamdoc_d",
    "han_tt_cl", "tg_xuat_phat",
    "t_tien_gui", "t_tien_gui_nt", "tl_gui",
    "t_cm", "t_cm_nt",
    "dx_yn", "ghi_chu",
    "tl_fb", "tien_fb_nt", "tien_fb",
    "ma_httt", "xuat_yn", "stt_rec_dp", "so_ct_dp", "check_cxk", "cm_yn", "hm_yn",
    "ma_loai_pt", "tg_giao",
    "ma_qg_giao", "ma_tinh_giao", "ma_quan_giao", "ma_phuong_giao",
    "ma_qg", "ma_tinh", "ma_quan", "ma_phuong",
    "lh_yn", "bien_so_lh", "ten_nguoi_lh", "sdt_lh", "cccd_lh", "chuc_vu_lh"
]
print(f"SCHEMA COUNT: {len(schema)}")
if total != len(schema):
    print(f"MISMATCH: {total} values vs {len(schema)} schema columns")
else:
    print(f"MATCH: {total} values == {len(schema)} schema columns")
