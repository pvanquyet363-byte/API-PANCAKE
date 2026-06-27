import sys

path = r'D:\API_PanCake2\API_PanCake\API_PanCake\Services\OrderSyncService.cs'
with open(path, 'r', encoding='utf-8', newline='') as f:
    content = f.read()

old_block = "                ) VALUES (\r\n                    @stt_rec, @ma_dvcs, 'DXB', '1', '', NULL, '', '1',\r\n                    @ngay_lct, @ngay_ct, @so_ct,\r\n                    'VND', 1,\r\n                    @ong_ba, @ma_kh, @dien_giai,\r\n                    '', '',\r\n                    @t_so_luong,\r\n                    @t_tien_nt, @t_tien, @t_tien2, @t_tien_nt2,\r\n                    0, 0,\r\n                    @t_tt_nt, @t_tt,\r\n                    '',\r\n                    @t_ck, @t_ck_nt,\r\n                    0, 0, 0, 0, 0, 0, 0, 0,\r\n                    NULL,\r\n                    '', '',\r\n                    '', '', '', NULL, NULL, NULL, '', 0, '',\r\n                    '', 0, '',\r\n                    0,\r\n                    0, 0,\r\n                    '', '0',\r\n                    GETDATE(), GETDATE(), @user_id, @user_id,\r\n                    '', '', '', '', '', '', '', '',\r\n                    '', '', '', '',\r\n                    '', '', '', NULL, NULL, NULL, 0, 0, 0,\r\n                    '', '', '',\r\n                    '', '', 'HOHO', 0, 0, 0, NULL, NULL, NULL,\r\n                    '', 0,\r\n                    0, 0, 0,\r\n                    '', '', '', NULL, NULL, NULL,\r\n                    0, '',\r\n                    0, 0, 0,\r\n                    0, 0,\r\n                    0, @ghi_chu,\r\n                    0, 0, 0,\r\n                    @ma_httt, 0, '', '', '', 0, 0,\r\n                    '', '',\r\n                    @ma_qg_giao, @ma_tinh_giao, @ma_quan_giao, @ma_phuong_giao,\r\n                    @ma_qg, @ma_tinh, @ma_quan, @ma_phuong,\r\n                    0, '', '', '', '', '', '', '', '',\r\n                    0, '', '', '', '', '', '', '',\r\n                    0, '', '', '', '', '', '', '', ''\r\n                )\";"

new_block = "                ) VALUES (\r\n                    @stt_rec, @ma_dvcs, 'DXB', '1', '', NULL, '', '1',\r\n                    @ngay_lct, @ngay_ct, @so_ct,\r\n                    'VND', 1,\r\n                    @ong_ba, @ma_kh, @dien_giai,\r\n                    '', '',\r\n                    @t_so_luong,\r\n                    @t_tien_nt, @t_tien, 0, 0,\r\n                    @t_tt_nt, @t_tt,\r\n                    '',\r\n                    0, 0, @t_ck, @t_ck_nt,\r\n                    0, 0, 0, 0, 0, 0, 0, 0,\r\n                    NULL, '', '',\r\n                    '', '', '', NULL, NULL, NULL, '', 0, '',\r\n                    '', 0, '',\r\n                    @nam, @ky, '', '0',\r\n                    GETDATE(), GETDATE(), @user_id, @user_id,\r\n                    '', '', '', '', '', '', '', '',\r\n                    '', '', '', '',\r\n                    '', '', '', NULL, NULL, NULL, 0, 0, 0,\r\n                    '', '', '',\r\n                    0, 0, '', '', '', 0, 0, 0,\r\n                    0, 0, 0,\r\n                    '', '', '', NULL, NULL, NULL,\r\n                    0, '',\r\n                    0, 0, 0,\r\n                    0, 0,\r\n                    0, @ghi_chu,\r\n                    0, 0, 0,\r\n                    @ma_httt, 0, '', '', '', 0, 0,\r\n                    '', '',\r\n                    @ma_qg_giao, @ma_tinh_giao, @ma_quan_giao, @ma_phuong_giao,\r\n                    @ma_qg, @ma_tinh, @ma_quan, @ma_phuong,\r\n                    0, '', '', '', '', '', '', '',\r\n                    0, '', '', '', '', '', '', '',\r\n                    0, '', '', '', '', '', '', '', ''\r\n                )\";"

if old_block in content:
    print("Found old block!")
    new_content = content.replace(old_block, new_block, 1)
    with open(path, 'w', encoding='utf-8', newline='') as f:
        f.write(new_content)
    print("Replacement done!")
else:
    print("NOT FOUND - trying debug")
    # Check for unique substrings
    for needle in ['@t_tien2', '@t_tien_nt2', 'HOHO', '@t_ck, @t_ck_nt']:
        idx = content.find(needle)
        if idx >= 0:
            print(f"Found '{needle}' at char {idx}: {repr(content[idx:idx+80])}")
