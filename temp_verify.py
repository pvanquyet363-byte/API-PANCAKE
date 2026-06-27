import re

path = r'D:\API_PanCake2\API_PanCake\API_PanCake\Services\OrderSyncService.cs'
with open(path, 'r', encoding='utf-8', newline='') as f:
    content = f.read()

# Find the DXB SQL block
start = content.find('var sql = $@"')
end = content.find('");', start) + 2
sql_block = content[start:end]

# Extract column list (between INSERT INTO ... ( and ) VALUES)
cols_start = sql_block.find('(')
cols_end = sql_block.find(') VALUES')
cols_text = sql_block[cols_start+1:cols_end]
values_text = sql_block[cols_end+9:-2]  # skip ") VALUES (" and trailing "

# Count columns
# Remove line comments and whitespace, split by comma
cols_lines = [l.strip() for l in cols_text.split('\n') if l.strip()]
cols_raw = ' '.join(cols_lines)
# Split by comma, filter empty
cols = [c.strip() for c in cols_raw.split(',') if c.strip() and not c.strip().startswith('--')]
print(f"COLUMN COUNT: {len(cols)}")
for i, c in enumerate(cols, 1):
    print(f"  {i:3d}. {c}")

print()

# Count values - more complex, need to handle multi-line entries
# Strategy: parse character by character tracking parens/quotes
values_parsed = []
current = ''
depth = 0
in_str = False
str_char = None
i = 0
while i < len(values_text):
    ch = values_text[i]
    
    if in_str:
        if ch == str_char and i+1 < len(values_text) and values_text[i+1] == str_char:
            current += ch + ch
            i += 2
            continue
        elif ch == str_char:
            current += ch
            in_str = False
            str_char = None
        else:
            current += ch
            i += 1
            continue
    else:
        if ch in "('\"":
            in_str = True
            str_char = ch
            current += ch
        elif ch == '(':
            depth += 1
            current += ch
        elif ch == ')':
            depth -= 1
            current += ch
        elif ch == ',' and depth == 0:
            val = current.strip()
            if val:
                values_parsed.append(val)
            current = ''
        else:
            current += ch
    i += 1

# Last one
if current.strip():
    values_parsed.append(current.strip())

print(f"VALUE COUNT: {len(values_parsed)}")
for i, v in enumerate(values_parsed, 1):
    print(f"  {i:3d}. {v}")

print()

# Schema columns from user
schema_cols = [
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

print(f"SCHEMA COLUMN COUNT: {len(schema_cols)}")
print()

if len(cols) != len(values_parsed):
    print(f"MISMATCH: {len(cols)} columns vs {len(values_parsed)} values")
elif len(cols) != len(schema_cols):
    print(f"SCHEMA MISMATCH: {len(cols)} columns vs {len(schema_cols)} schema columns")
else:
    # Check column order matches schema
    mismatches = []
    for i, (c, s) in enumerate(zip(cols, schema_cols), 1):
        if c != s:
            mismatches.append(f"  Position {i}: INSERT has '{c}', SCHEMA has '{s}'")
    if mismatches:
        print("COLUMN ORDER MISMATCHES:")
        for m in mismatches:
            print(m)
    else:
        print("ALL GOOD! Column count and order match schema perfectly!")
        print(f"  {len(cols)} columns == {len(values_parsed)} values == {len(schema_cols)} schema columns")
