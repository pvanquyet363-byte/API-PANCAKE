import re

path = r'D:\API_PanCake2\API_PanCake\API_PanCake\Services\OrderSyncService.cs'
with open(path, 'r', encoding='utf-8', newline='') as f:
    content = f.read()

first_vals = content.find(') VALUES (')
dxb_start = content.find('var sql = $@"', 0, first_vals + 100)

# Use FIND (not rfind) to find the FIRST )"; after VALUES
vals_pos = content.find(') VALUES (', first_vals)
vals_start = content.find('(', vals_pos) + 1  # skip "VALUES ("
# Now find the FIRST )"; after vals_start
first_close = content.find(')";', vals_start)

dxb_block = content[dxb_start:first_close + 2]
print(f"Block length: {len(dxb_block)}")
print(f"Block ends: {repr(dxb_block[-30:])}")

# Extract values
vals_text = content[vals_start:first_close]
lines = vals_text.split('\n')
total = 0
for line in lines:
    stripped = line.strip()
    if not stripped:
        continue
    items = stripped.rstrip(',').split(',')
    count = len(items)
    total += count

print(f"\nVALUE COUNT: {total}")
print(f"COLUMN COUNT: 137")

if total == 137:
    print("PERFECT MATCH: 137 columns == 137 values")
elif total == 138:
    print("MATCH: 137 columns == 138 values")
else:
    print(f"MISMATCH: 137 columns vs {total} values")
    # Show line by line
    for line in lines:
        stripped = line.strip()
        if stripped:
            items = stripped.rstrip(',').split(',')
            print(f"  {len(items):2d}: {stripped[:100]}")
