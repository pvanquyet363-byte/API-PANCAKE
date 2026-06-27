# Manual count of the VALUES lines in the DXB block
values_per_line = [
    8,   # @stt_rec, @ma_dvcs, 'DXB', '1', '', NULL, '', '1',
    3,   # @ngay_lct, @ngay_ct, @so_ct,
    2,   # 'VND', 1,
    3,   # @ong_ba, @ma_kh, @dien_giai,
    2,   # '', '',
    1,   # @t_so_luong,
    4,   # @t_tien_nt, @t_tien, 0, 0,
    2,   # @t_tt_nt, @t_tt,
    1,   # '',
    4,   # 0, 0, @t_ck, @t_ck_nt,
    8,   # 0, 0, 0, 0, 0, 0, 0, 0,
    3,   # NULL, '', '',
    9,   # '', '', '', NULL, NULL, NULL, '', 0, '',
    3,   # '', 0, '',
    4,   # @nam, @ky, '', '0',
    4,   # GETDATE(), GETDATE(), @user_id, @user_id,
    8,   # '', '', '', '', '', '', '', '',
    4,   # '', '', '', '',
    9,   # '', '', '', NULL, NULL, NULL, 0, 0, 0,
    3,   # '', '', '',
    8,   # 0, 0, '', '', '', 0, 0, 0,
    3,   # 0, 0, 0,
    6,   # '', '', '', NULL, NULL, NULL,
    2,   # 0, '',
    3,   # 0, 0, 0,
    2,   # 0, 0,
    2,   # 0, @ghi_chu,
    3,   # 0, 0, 0,
    7,   # @ma_httt, 0, '', '', '', 0, 0,
    2,   # '', '',
    4,   # @ma_qg_giao, @ma_tinh_giao, @ma_quan_giao, @ma_phuong_giao,
    4,   # @ma_qg, @ma_tinh, @ma_quan, @ma_phuong,
    8,   # 0, '', '', '', '', '', '', '',
    8,   # 0, '', '', '', '', '', '', '',
    9,   # 0, '', '', '', '', '', '', '', ''  <- 9 items
]

total = sum(values_per_line)
print(f"Line count: {len(values_per_line)}")
print(f"Total values: {total}")
print(f"Schema columns: 138")
print()

# Show running total to find discrepancy
running = 0
for i, n in enumerate(values_per_line, 1):
    running += n
    if running > 137:
        print(f">>> OVERFLOW at line {i}: {n} items, running total {running}")
    elif running == 137:
        print(f"    Line {i}: {n} items, running total {running} (PERFECT)")
    else:
        pass  # silent

print(f"\nFINAL: {total} values, {total - 137} extra")
