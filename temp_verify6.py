import re

path = r'D:\API_PanCake2\API_PanCake\API_PanCake\Services\OrderSyncService.cs'
with open(path, 'r', encoding='utf-8', newline='') as f:
    content = f.read()

# Count all ) VALUES ( patterns to see how many blocks
count = content.count(') VALUES (')
print(f"Number of 'VALUES (' clauses in file: {count}")

# Find all positions
pos = 0
while True:
    idx = content.find(') VALUES (', pos)
    if idx == -1:
        break
    # Get context
    before = content[max(0,idx-50):idx+20]
    print(f"  At {idx}: ...{repr(before)}")
    pos = idx + 1
