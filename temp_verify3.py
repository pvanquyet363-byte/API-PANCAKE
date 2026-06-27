import re

path = r'D:\API_PanCake2\API_PanCake\API_PanCake\Services\OrderSyncService.cs'
with open(path, 'r', encoding='utf-8', newline='') as f:
    content = f.read()

# Find the DXB SQL block
start = content.find('var sql = $@"')
end = content.find('");', start) + 2
sql_block = content[start:end]

# Find VALUES section - text from "VALUES (" to the closing ")
# First find the ) VALUES ( pattern
pattern_start = sql_block.find(') VALUES (')
values_text = sql_block[pattern_start + 10:-2]  # skip ") VALUES (" and trailing "

# Count commas at depth 0 not inside string literals
depth = 0
in_str = False
str_char = None
comma_count = 0
i = 0
while i < len(values_text):
    ch = values_text[i]
    
    if in_str:
        # Handle SQL string escapes: '' inside a string is an escaped quote
        if ch == str_char and i+1 < len(values_text) and values_text[i+1] == str_char:
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
    elif ch == '(':
        depth += 1
    elif ch == ')':
        depth -= 1
    elif ch == ',' and depth == 0:
        comma_count += 1
    i += 1

value_count = comma_count + 1
print(f"VALUE COUNT (comma+1 method): {value_count}")

# Manually count each line
lines = [l.strip() for l in values_text.split('\n') if l.strip() and not l.strip().startswith(')')]
total = 0
for line in lines:
    items = line.rstrip(',').split(',')
    count = len(items)
    total += count
    print(f"  {count:2d}: {line}")

print(f"\nMANUAL LINE COUNT: {total}")
print(f"\nFINAL: {total} values")

# Also verify the schema from user has 138 columns
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
print(f"\nSCHEMA COUNT: {len(schema)}")
print(f"\nSTATUS: {len(schema)} columns, {total} values")
