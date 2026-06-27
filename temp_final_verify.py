path = r'D:\API_PanCake2\API_PanCake\API_PanCake\Services\OrderSyncService.cs'
with open(path, 'r', encoding='utf-8', newline='') as f:
    content = f.read()

# Extract DXB block
first_vals = content.find(') VALUES (')
dxb_start = content.find('var sql = $@"', 0, first_vals + 100)
vals_pos = content.find(') VALUES (', first_vals)
vals_start = content.find('(', vals_pos) + 1
first_close = content.find(')";', vals_start)
vals_text = content[vals_start:first_close]

# Count columns in INSERT
cols_start = content.find('INSERT INTO', dxb_start)
cols_end = content.find(') VALUES', dxb_start)
cols_text = content[cols_start:cols_end]
cols_inner = cols_text[cols_text.find('(')+1:]
cols = [c.strip() for c in cols_inner.split(',') if c.strip()]

# Count values
lines = vals_text.split('\n')
total_vals = 0
for line in lines:
    stripped = line.strip()
    if not stripped: continue
    items = stripped.rstrip(',').split(',')
    total_vals += len(items)

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

print(f"INSERT COLUMNS: {len(cols)}")
print(f"VALUES COUNT:   {total_vals}")
print(f"SCHEMA COLUMNS: {len(schema)}")
print()

if len(cols) != len(schema):
    print(f"INSERT vs SCHEMA: {len(cols)} != {len(schema)}")
    # Show mismatches
    for i, (c, s) in enumerate(zip(cols, schema)):
        if c != s:
            print(f"  Pos {i+1}: INSERT='{c}' vs SCHEMA='{s}'")
    if len(cols) > len(schema):
        print(f"  Extra in INSERT: {cols[len(schema):]}")
    else:
        print(f"  Missing in INSERT: {schema[len(cols):]}")

if total_vals != len(cols):
    print(f"\nVALUES vs COLUMNS: {total_vals} != {len(cols)}")
else:
    print(f"MATCH: VALUES({total_vals}) == COLUMNS({len(cols)})")

# Show values line by line
print("\nVALUES lines:")
lines = vals_text.split('\n')
for i, line in enumerate(lines, 1):
    stripped = line.strip()
    if not stripped: continue
    items = stripped.rstrip(',').split(',')
    print(f"  L{i:2d}: {len(items):3d} | {stripped[:100]}")
