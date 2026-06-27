import re

path = r'D:\API_PanCake2\API_PanCake\API_PanCake\Services\OrderSyncService.cs'
with open(path, 'r', encoding='utf-8', newline='') as f:
    content = f.read()

first_vals = content.find(') VALUES (')
dxb_start = content.find('var sql = $@"', 0, first_vals + 100)
dxb_end = content.find('");', dxb_start) + 2
dxb_block = content[dxb_start:dxb_end]

vals_pos = dxb_block.find(') VALUES (')
vals_text = dxb_block[vals_pos + 10:]  # skip ") VALUES ("
# Now find the closing ) of VALUES - it's the one before );
# Strategy: find the final ")" in the block that closes VALUES
# Since there are no nested parens in VALUES for m65, the first ) at depth 1 is the close
# But GETDATE() has parens... so we track string state separately
# The VALUES clause ends at the ) that is NOT inside a function call
# Since function calls (GETDATE()) have no commas inside them, we can find it by
# looking for ) followed by ";" or newline+"

# Actually the simplest: find the last ")"; in the block
# The VALUES ends right before )";
last_val_close = dxb_block.rfind(')";')
vals_text = dxb_block[vals_pos + 10:last_val_close]  # Everything between VALUES ( and );

print(f"Values text length: {len(vals_text)}")
print(f"Starts with: {repr(vals_text[:50])}")
print(f"Ends with: {repr(vals_text[-50:])}")

# Now count values - count commas NOT inside single-quoted strings
# This is SQL, so strings use single quotes ''
# Multi-char strings like '' inside a string are escaped quotes
depth = 0
in_str = False
values = []
current = ''
i = 0
while i < len(vals_text):
    ch = vals_text[i]
    
    if in_str:
        # Handle '' escape in SQL strings
        if ch == "'" and i + 1 < len(vals_text) and vals_text[i + 1] == "'":
            current += "''"
            i += 2
            continue
        elif ch == "'":
            in_str = False
            current += ch
        else:
            current += ch
        i += 1
        continue
    
    if ch == "'":
        in_str = True
        current += ch
    elif ch == ',':
        values.append(current.strip())
        current = ''
    elif ch == '\r' or ch == '\n':
        # Skip whitespace-only continuation
        pass
    else:
        current += ch
    i += 1

if current.strip():
    values.append(current.strip())

print(f"\nVALUE COUNT: {len(values)}")
for i, v in enumerate(values, 1):
    print(f"  {i:3d}. {v}")

# Count columns
cols_section = dxb_block[dxb_block.find('INSERT INTO'):dxb_block.find(') VALUES')]
cols_text = cols_section[cols_section.find('(')+1:]
cols = [c.strip() for c in cols_text.split(',') if c.strip()]
print(f"\nCOLUMN COUNT: {len(cols)}")

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
