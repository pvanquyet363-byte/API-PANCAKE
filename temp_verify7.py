import re

path = r'D:\API_PanCake2\API_PanCake\API_PanCake\Services\OrderSyncService.cs'
with open(path, 'r', encoding='utf-8', newline='') as f:
    content = f.read()

# Find the DXB block - it's the first one (m65 table)
# Position 18795 is the first VALUES
first_vals = content.find(') VALUES (')
# Extract from var sql = $@" to );
dxb_start = content.find('var sql = $@"', 0, first_vals + 100)
dxb_end = content.find('");', dxb_start) + 2
dxb_block = content[dxb_start:dxb_end]

print(f"DXB block length: {len(dxb_block)}")
print(f"DXB block snippet: {repr(dxb_block[:100])}")

# Find VALUES section
vals_pos = dxb_block.find(') VALUES (')
vals_text = dxb_block[vals_pos + 10:]  # skip ") VALUES ("
# vals_text ends with )"; - get everything before that
vals_text = vals_text[:-4]  # remove )";

# Count values manually by splitting on commas at depth 0
depth = 0
in_str = False
str_char = None
values = []
current = ''
for i, ch in enumerate(vals_text):
    if in_str:
        if ch == str_char and i+1 < len(vals_text) and vals_text[i+1] == str_char:
            current += ch + ch
            i += 2
            continue
        elif ch == str_char:
            in_str = False
            str_char = None
        current += ch
        continue
    
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
        values.append(current.strip())
        current = ''
    else:
        current += ch

if current.strip():
    values.append(current.strip())

print(f"\nVALUE COUNT: {len(values)}")
for i, v in enumerate(values, 1):
    print(f"  {i:3d}. {v}")

print(f"\n{'='*60}")
# Now count columns
cols_pos_start = dxb_block.find('INSERT INTO')
cols_pos_end = dxb_block.find(') VALUES')
cols_section = dxb_block[cols_pos_start:cols_pos_end]
# Extract column names
cols_text = cols_section[cols_section.find('(')+1:]
cols = [c.strip() for c in cols_text.split(',') if c.strip()]
print(f"COLUMN COUNT: {len(cols)}")
for i, c in enumerate(cols, 1):
    print(f"  {i:3d}. {c}")

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
print(f"\nSCHEMA COUNT: {len(schema)}")

if len(cols) != len(values):
    print(f"\nMISMATCH: {len(cols)} columns vs {len(values)} values")
elif len(cols) != len(schema):
    print(f"\nSCHEMA MISMATCH: {len(cols)} INSERT cols vs {len(schema)} SCHEMA cols")
else:
    mismatches = []
    for i, (c, s) in enumerate(zip(cols, schema), 1):
        if c != s:
            mismatches.append(f"  Pos {i}: INSERT='{c}', SCHEMA='{s}'")
    if mismatches:
        print(f"\nCOLUMN ORDER MISMATCHES:")
        for m in mismatches:
            print(m)
    else:
        print(f"\nPERFECT MATCH: {len(cols)} == {len(values)} == {len(schema)}")
