import re

path = r'D:\API_PanCake2\API_PanCake\API_PanCake\Services\OrderSyncService.cs'
with open(path, 'r', encoding='utf-8', newline='') as f:
    content = f.read()

first_vals = content.find(') VALUES (')
dxb_start = content.find('var sql = $@"', 0, first_vals + 100)
dxb_end = content.find('");', dxb_start) + 2
dxb_block = content[dxb_start:dxb_end]

print(f"Block length: {len(dxb_block)}")
print(f"Block ends: {repr(dxb_block[-50:])}")

vals_pos = dxb_block.find(') VALUES (')
vals_text = dxb_block[vals_pos + 10:]  # skip ") VALUES ("
# Find last ")"; in the block
last_val_close = dxb_block.rfind(')";')
print(f"\nlast_val_close position: {last_val_close}")
print(f"last_val_close context: {repr(dxb_block[last_val_close-20:last_val_close+20])}")

# Extract just the VALUES values
vals_only = dxb_block[vals_pos + 10:last_val_close]
print(f"\nvals_only length: {len(vals_only)}")
print(f"vals_only ends: {repr(vals_only[-50:])}")

# Now manually count commas in SQL VALUES context
# Strategy: split by newlines and count items per line
lines = vals_only.split('\n')
total = 0
for line in lines:
    stripped = line.strip()
    if not stripped or stripped == ')':
        continue
    # Count commas at depth 0 not in strings
    # But for simplicity, just count items by splitting
    # Each item is separated by comma at top level
    count = len(stripped.split(','))
    total += count
    print(f"  {count:2d}: {stripped[:100]}")

print(f"\nTOTAL (line split): {total}")
