# CodeWalker CLI

CLI Windows x64 untuk build CodeWalker custom (`CodeWalker.Core.dll`). Membutuhkan `.NET Framework 4.8`.

```powershell
.\codewalker-cli.exe help
.\codewalker-cli.exe version --json
.\codewalker-cli.exe rpf list 'D:\mods\example.rpf' --contains .ymap --game-dir 'D:\Steam\steamapps\common\Grand Theft Auto V' --json
.\codewalker-cli.exe rpf extract 'D:\mods\example.rpf' 'subfolder\example.ymap' '.\example.ymap' --game-dir 'D:\Steam\steamapps\common\Grand Theft Auto V'
.\codewalker-cli.exe xml export '.\example.ymap' '.\xml-out' --game-dir 'D:\Steam\steamapps\common\Grand Theft Auto V' --names-dir '.\stream' --json
.\codewalker-cli.exe xml import '.\xml-out\example.ymap.xml' '.\rebuilt.ymap' --json
```

- **Resolusi hash (`xml export`)**: otomatis memuat `strings.txt` di sebelah `.exe` + nama aset di folder file input. Tambahkan `--names-dir PATH` (scan rekursif `.ydr/.ydd/.yft/.ytd/.ybn/.ycd/.ytyp/.ymap/.sps`) dan/atau `--game-dir PATH` (index seluruh GTA V) untuk mengubah `hash_XXXXXXXX` menjadi nama asli arketipe & shader.
- **Tipe didukung XML**: Legacy `YMAP`, `YTYP`, `YDR`, `YDD`, `YFT`, `YTD`, `YBN`, dan `YCD`. Simpan folder tekstur `.dds` di sebelah `.xml` agar ter-embed kembali saat `xml import`.
- **Proteksi output**: CLI selalu menolak menimpa file/folder output yang sudah ada dan memakai staging sebelum rename. Exit code: `0` sukses, `1` gagal operasi, `2` argumen salah.
- **Rebuild**: jalankan `.\Build.ps1` (otomatis memakai `..\CodeWalker\bin\Debug\net48`) atau `.\Build.ps1 -CodeWalkerBin 'D:\path\to\CodeWalker\bin\Debug\net48'`.
