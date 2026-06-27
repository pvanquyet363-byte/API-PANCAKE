# Script to analyze and fix DXB and SDF INSERT blocks in OrderSyncService.cs

import re

FILE_PATH = r"D:\API_PanCake2\API_PanCake\API_PanCake\Services\OrderSyncService.cs"

with open(FILE_PATH, 'r', encoding='utf-8') as f:
    content = f.read()

lines = content.split('\n')

# Find DXB and SDF INSERT blocks
# DXB is the first var sql = $@" (around line 378)
# SDF is the second var sql = $@" that has m760

dxb_start = None
sdf_start = None
in_dxb_values = False
in_sdf_values = False

dxb_columns = []
dxb_values = []
sdf_columns = []
sdf_values = []

current_block = None
brace_count = 0

for i, line in enumerate(lines):
    # Detect start of DXB INSERT block
    if 'var sql = $@"' in line and 'm65$' in content[max(0, content.find(line)-500):content.find(line)+200]:
        if dxb_start is None:
            dxb_start = i
            current_block = 'DXB'
            print(f"Found DXB block at line {i+1}")

    # Detect start of SDF INSERT block
    if 'var sql = $@"' in line and 'm760$' in content[max(0, content.find(line)-500):content.find(line)+200]:
        if sdf_start is None:
            sdf_start = i
            current_block = 'SDF'
            print(f"Found SDF block at line {i+1}")

# Now let's find columns and VALUES sections more precisely
print("\n--- Analyzing DXB block ---")
# DXB INSERT starts at line 378 (index 377)
# Columns: lines 379-397 (index 378-396)
# VALUES: lines 398-416 (index 397-415)

# Extract DXB columns
dxb_col_lines = lines[378:397]  # Lines 379-397 (0-indexed: 378-396)
dxb_cols_text = ' '.join(dxb_col_lines)
dxb_cols = [c.strip() for c in dxb_cols_text.replace('INSERT INTO m65${partition} (', '').replace(') VALUES', '').split(',')]
dxb_cols = [c for c in dxb_cols if c]
print(f"DXB columns count: {len(dxb_cols)}")
print(f"First few DXB cols: {dxb_cols[:5]}")
print(f"Last few DXB cols: {dxb_cols[-5:]}")

# Extract DXB VALUES
dxb_val_lines = lines[397:416]  # Lines 398-416
print(f"\nDXB VALUES lines:")
for j, vline in enumerate(dxb_val_lines):
    print(f"  Line {398+j}: {vline.strip()}")

# Count DXB VALUES items
dxb_values_text = '\n'.join(dxb_val_lines)
# Replace newlines with commas for counting
dxb_values_clean = dxb_values_text.replace('\n', '').replace('                ', ' ')
# Count items by splitting on commas, but need to handle embedded strings
# Simple count: count commas that separate values
dxb_items = []
for vline in dxb_val_lines:
    items = vline.strip().split(',')
    # Clean each item
    for item in items:
        item = item.strip().rstrip(',')
        if item:
            dxb_items.append(item)

print(f"\nDXB VALUES count: {len(dxb_items)}")

# Print all DXB values
print("\nDXB VALUES:")
for j, item in enumerate(dxb_items):
    print(f"  {j+1}: {item}")

# Now SDF
print("\n--- Analyzing SDF block ---")
# SDF INSERT starts at line 539 (index 538)
# Columns: lines 540-574 (index 539-573)
# VALUES: lines 575-610 (index 574-609)

# Extract SDF columns
sdf_col_lines = lines[539:574]  # Lines 540-574 (0-indexed: 539-573)
sdf_cols_text = '\n'.join(sdf_col_lines)
sdf_cols = [c.strip() for c in sdf_cols_text.replace('INSERT INTO m760${partition} (', '').replace(') VALUES', '').split(',')]
sdf_cols = [c for c in sdf_cols if c]
print(f"SDF columns count: {len(sdf_cols)}")
print(f"First few SDF cols: {sdf_cols[:5]}")
print(f"Last few SDF cols: {sdf_cols[-5:]}")

# Extract SDF VALUES
sdf_val_lines = lines[574:610]  # Lines 575-610
print(f"\nSDF VALUES lines:")
for j, vline in enumerate(sdf_val_lines):
    print(f"  Line {575+j}: {vline.strip()}")

# Count SDF VALUES items
sdf_items = []
for vline in sdf_val_lines:
    items = vline.strip().split(',')
    for item in items:
        item = item.strip().rstrip(',')
        if item:
            sdf_items.append(item)

print(f"\nSDF VALUES count: {len(sdf_items)}")

# Print all SDF values
print("\nSDF VALUES:")
for j, item in enumerate(sdf_items):
    print(f"  {j+1}: {item}")

print("\n--- Summary ---")
print(f"DXB columns: {len(dxb_cols)}, DXB VALUES: {len(dxb_items)}")
print(f"SDF columns: {len(sdf_cols)}, SDF VALUES: {len(sdf_items)}")

if len(dxb_cols) != len(dxb_items):
    print(f"DXB MISMATCH! Columns: {len(dxb_cols)}, Values: {len(dxb_items)}, Diff: {len(dxb_items) - len(dxb_cols)}")

if len(sdf_cols) != len(sdf_items):
    print(f"SDF MISMATCH! Columns: {len(sdf_cols)}, Values: {len(sdf_items)}, Diff: {len(sdf_items) - len(sdf_cols)}")
