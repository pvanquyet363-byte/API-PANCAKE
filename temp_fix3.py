import sys

path = r'D:\API_PanCake2\API_PanCake\API_PanCake\Services\OrderSyncService.cs'
with open(path, 'r', encoding='utf-8', newline='') as f:
    content = f.read()

# ============================================
# FIX: Add missing ma_gia column to INSERT list
# The VALUES line 13: '', 0, '' corresponds to ma_nvbh, ck_thue_yn, ma_gia
# And VALUES needs tien_hd inserted (currently values stop at 137 but should be 138)
# ============================================
# Fix column list - add ma_gia after ck_thue_yn
old_cols = "ma_nvbh, ck_thue_yn, ma_gia, tien_hd,"
new_cols = "ma_nvbh, ck_thue_yn, ma_gia, tien_hd,"

if old_cols in content:
    print("Found correct column pattern")
else:
    print("NOT FOUND - checking...")
    idx = content.find('ma_nvbh')
    if idx >= 0:
        print(f"ma_nvbh at {idx}: {repr(content[idx:idx+100])}")

# Also fix VALUES: line 13 should be '', 0, '', 0 (for ma_nvbh, ck_thue_yn, ma_gia, tien_hd)
# Current: '', 0, '' (only 3, missing tien_hd)
old_vals_line = "'', 0, '',\r\n                    @nam"
new_vals_line = "'', 0, '', 0,\r\n                    @nam"

if old_vals_line in content:
    print("Found VALUES line to fix!")
    content = content.replace(old_vals_line, new_vals_line, 1)
else:
    print("VALUES line NOT FOUND")
    idx = content.find("'', 0, '',")
    if idx >= 0:
        print(f"Found '', 0, '' at {idx}: {repr(content[idx:idx+50])}")

with open(path, 'w', encoding='utf-8', newline='') as f:
    f.write(content)
print("Done!")
