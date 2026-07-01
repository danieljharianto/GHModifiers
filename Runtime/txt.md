GHModifiers — "modifier stack" untuk Rhino

Ini adalah plugin Rhino 8 (dikompilasi jadi .rhp) yang memberi Grasshopper sebuah modifier stack ala Blender. Anda memilih objek Rhino, menumpuk file .gh di atasnya sebagai "modifier", lalu geometri mengalir melewatinya secara live. Meski nama folder/namespace-nya HelloRhinoCommon (berawal dari template Rhino), produknya adalah GrassGrassGrassHopperHopperHopper / "GGH".

Struktur folder

GHModifiers/
├─ HelloRhinoCommonPlugin.cs   ← titik masuk plugin (satu-satunya kelas turunan PlugIn)
├─ HelloRhinoCommon.csproj     ← konfigurasi build (target Rhino 8, output .rhp)
│
├─ Commands/                   ← perintah command-line Rhino
│   ├─ GghOpenPanelCommand.cs           (_GghOpenPanel → buka Object Properties)
│   └─ GghRefreshSelectedStackCommand.cs (_GghRefreshSelectedStack → jalankan ulang stack)
│
├─ Models/                     ← tipe data murni (tanpa logika)
│   ├─ ModifierStackSpec.cs    (yang DISIMPAN di objek: steps, values, links)
│   └─ ModifierPanelState.cs   (yang DIRENDER UI: view model read-only)
│
├─ Runtime/                    ← engine (semua logika sesungguhnya)
│   ├─ ModifierEngine.cs       (inti 4000 baris: evaluasi, caching, events)
│   ├─ ModifierStackStorage.cs (load/save spec sebagai JSON di UserDictionary objek)
│   └─ GeometryConversion.cs   (geometri Rhino ⇄ "Goo" Grasshopper)
│
├─ UI/                         ← UI berbasis Eto.Forms
│   ├─ ModifierObjectPropertiesPage.cs (mendaftarkan tab "GGH Stack")
│   ├─ ModifierStackPanel.cs           (panel 2000 baris: widget-widget sebenarnya)
│   └─ StackPreviewConduit.cs          (menggambar hasil live di viewport)
│
├─ Properties/                 ← info assembly, resources, launch settings
├─ *.gh / *.ghx                ← contoh definisi modifier (Extrude, Twist, dll.)
├─ ModifierGuide.md            ← spesifikasi cara menulis modifier .gh
└─ README.md

Hal kuncinya adalah pemisahan yang rapi: Models = data, Runtime = logika, UI = presentasi, dan ketiganya hanya berkomunikasi melalui engine.

Cara kerja logikanya

1. Plugin dimuat secara on-demand. HelloRhinoCommonPlugin.OnLoad (HelloRhinoCommonPlugin.cs:42) memaksa Grasshopper dimuat lebih dulu (engine mereferensikan GH_Document dll.), membuat satu instance ModifierEngine, lalu mendaftarkan ModifierObjectPropertiesPage sehingga muncul tab "GGH Stack" di panel Object Properties (sidebar kanan Rhino). Waktu muatnya WhenNeeded, bukan saat startup — memuat GH terlalu dini membuatnya tidak stabil.

2. Setiap objek membawa stack-nya sendiri, tersimpan di dalam file. ModifierStackStorage (Runtime/ModifierStackStorage.cs) menserialisasi ModifierStackSpec ke JSON dan menyimpannya dengan key GGH.ModifierStack.v1 di Attributes.UserDictionary objek Rhino. Jadi stack ikut bersama objek dan bertahan setelah save/buka-ulang. Sebuah ModifierStepSpec mencatat: Path file .gh, Enabled, InputValues dari user, dan InputLinks antar-modifier.

3. File .gh menjadi "kontrak" lewat konvensi. CreateDefinitionContract (ModifierEngine.cs:1739) membuka definisi dan membacanya murni berdasarkan konvensi penamaan (didokumentasikan di ModifierGuide.md):
- Mencari group GH dengan nickname Inputs dan Outputs.
- Param di dalam Inputs menjadi kontrol panel — jenisnya dideteksi dari tipe GH (number slider, point, boolean, colour, string, geometry, value list).
- Param di dalam Outputs menjadi output yang dipublikasikan; satu yang bernickname GeomOut/GeoOut adalah pipa geometri ke modifier berikutnya.
- Param standalone bernickname GeomIn/GeoIn (di luar group, tak terkabel) adalah tempat geometri masuk disuntikkan.

Kontrak di-cache per file+timestamp (DefinitionTemplate), jadi mengedit .gh lalu refresh akan menangkap perubahannya.

4. Evaluasi adalah pipeline ber-cache dan berbasis event. Engine berlangganan event dokumen Rhino (objek diganti/dihapus, seleksi berubah, dokumen dibuka). Ketika sebuah stack "kotor", ia dimasukkan ke _queuedStacks dan diproses saat RhinoApp.Idle (agar solve GH yang berat tak pernah memblokir UI). EvaluateStack (ModifierEngine.cs:876) lalu:
- Mengonversi objek Rhino sumber menjadi geometri (GeometryConversion.TryGetSourceGeometry).
- Menelusuri step secara urut. Untuk tiap step aktif, ia menduplikasi dokumen GH yang ter-cache, menyuntikkan geometri saat ini ke GeomIn, menerapkan nilai/link input dari user, menjalankan NewSolution(Silent), lalu membaca GeomOut — output itu jadi input step berikutnya.
- Memakai cache berbasis revision-counter: jika revision input suatu step tak berubah, output ter-cache dipakai ulang tanpa solve ulang. Inilah yang membuat menggeser slider di step 3 tidak menjalankan ulang step 1–2.
- Menyimpan geometri akhir sebagai PreviewGeometry.

5. Linking antar-modifier & antar-objek. Sebuah input bisa di-link ke output step sebelumnya (link StepOutput) atau ke hasil termodifikasi objek lain (link ObjectPreview), bukan sekadar nilai ketikan. Engine memvalidasi ini (TryValidateInputLink di :1366, TryValidateObjectPreviewInputLink di :1446), termasuk deteksi siklus agar referensi objek-A→objek-B→objek-A ditolak.

6. Preview & apply. StackPreviewConduit (UI/StackPreviewConduit.cs) adalah DisplayConduit yang menyembunyikan objek asli (PreDrawObject) dan menggambar hasil stack secara live, mewarisi warna/material objek sumber. Tidak ada yang di-commit sampai user memilih:
- Apply through step (ApplyThroughStep, :578) — membakar N modifier ke geometri objek sungguhan dan menghapusnya dari stack.
- Bake (BakeFinalResult, :659) — membuat objek baru dari hasil akhir, membiarkan stack tetap utuh.

7. Panel hanyalah renderer tipis. ModifierStackPanel (UI/ModifierStackPanel.cs) berlangganan event StateChanged engine, memanggil GetPanelState(doc) untuk mendapat ModifierPanelState yang sudah dihitung penuh, lalu membangun ulang widget-nya. Aksi user (tambah/hapus/urut-ulang/aktifkan/edit nilai/link) memanggil balik ke metode engine (AddStep, MoveStep, SetStepInputValue, …), yang masing-masing menyimpan spec dan mengantrikan evaluasi lagi. Panel tak memegang logika bisnis — ia hanya merender state dan meneruskan niat user.

Alur data inti, ujung ke ujung

pilih objek  → engine baca spec dari UserDictionary (JSON)
             → bangun kontrak dari tiap .gh (group + nickname)
             → GetPanelState() → panel render inputs/outputs
user ubah    → SetStepInputValue() → simpan spec → antrikan eval
Idle         → EvaluateStack(): geom sumber ─pipe→ step0 ─pipe→ step1 … (cache per revision)
             → PreviewGeometry → conduit gambar live di viewport
apply/bake   → tulis geometri Rhino sungguhan, update/kosongkan stack

Beberapa detail engineering yang perlu diketahui: build menaruh output di C:\RhinoPlugins\GHModifiers\ khusus di Debug untuk menghindari file reparse-point OneDrive yang mengunci .rhp (.csproj:15); Grasshopper/RhinoCommon direferensikan dengan ExcludeAssets="runtime" agar plugin memakai salinan yang sudah dimuat Rhino, bukan mengirim duplikat yang tak kompatibel tipe (.csproj:42); dan hint path di .csproj menunjuk ke instalasi Rhino macOS meski Anda di Windows — jadi build di sini perlu penyesuaian path ke C:\Program Files\Rhino 8\....
