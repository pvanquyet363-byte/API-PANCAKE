import sys

path = r'D:\API_PanCake2\API_PanCake\API_PanCake\Services\OrderSyncService.cs'
with open(path, 'r', encoding='utf-8', newline='') as f:
    content = f.read()

# Fix column list - add ma_gia after ck_thue_yn (in the DXB block)
# The DXB block column list currently has: ma_nvbh, ck_thue_yn, ma_gia, tien_hd
# But ma_gia is NOT there, we need to add it
old_col_line = "ma_nvbh, ck_thue_yn, tien_hd,"
new_col_line = "ma_nvbh, ck_thue_yn, ma_gia, tien_hd,"

if old_col_line in content:
    content = content.replace(old_col_line, new_col_line, 1)
    print("Fixed: added ma_gia to column list")
else:
    print("NOT FOUND - checking...")
    idx = content.find('ma_nvbh')
    if idx >= 0:
        print(f"Found ma_nvbh at {idx}: {repr(content[idx:idx+50])}")

with open(path, 'w', encoding='utf-8', newline='') as f:
    f.write(content)
print("Done!")
