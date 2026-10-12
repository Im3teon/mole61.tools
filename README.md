# mole61.tools

由 **Im3teon** 獨立維護的摩爾莊園 Windows 工具。以 [SaviorDT/getFish](https://github.com/SaviorDT/getFish) 為基礎修改，並非原作者的官方版本。

## 本版功能

- 重新整理 GUI，依日常、戰鬥及工具分類。
- 彩虹瀑布：自動進圖、採收螢火草、確認獎勵與每日上限。
- 摩界之門：自動進圖，依序採集毛毛豆種子、拉姆菌、捕蠅草種子。
- 採集輸入留空：各項採到上限；輸入 1～999：各項最多指定回數。
- 固定座標點擊，不使用 OCR；提供停止、逾時、獎勵關閉重試與錯誤診斷。

## 下載與使用

請從本專案 [Releases](https://github.com/Im3teon/mole61.tools/releases) 下載。

1. 使用 Windows 10／11，需安裝 .NET Framework 4.7.2 或相容較新版。
2. 自行準備 Flash Player 並登入遊戲；本程式不包含 Flash Player 或遊戲資源。
3. 開啟 `mole61.tools.exe`，必要時使用「選擇應用程式」選取 Flash Player。
4. 選擇所需功能；採集可在計數視窗按「停止」。Ctrl + Backspace 會結束工具。

已在本機以只有 EXE 的空資料夾驗證啟動、儲存設定與結束。可以單獨傳送 EXE 作為基本使用方式，但這不包含 .NET Framework、Flash Player 或選用功能資料，也不代表所有電腦環境均已驗證。

發布 ZIP 包含 EXE、同名 `.exe.config` 與使用說明；建議完整解壓縮，放在可寫入的資料夾。`settings.json` 為執行後產生的個人設定，不需分享。自動登入等選用功能可能需要使用者自己的 `帳號密碼.txt`，請勿分享帳密檔案。

## 與上游的區別

- 程式名稱與檔名為 `mole61.tools`，版本從 `1.0.0` 開始獨立編號。
- 更新來源僅指向 `Im3teon/mole61.tools`，不再使用上游的更新清單。
- 預設關閉自動更新；初始 `manifest.json` 為空，後續有正式發行版才加入。
- 保留上游來源說明。新增功能與介面由本版本維護，不代表所有原有功能都已重新實測。

## 建置

使用 Visual Studio Build Tools／Visual Studio，安裝 .NET Framework 4.7.2 targeting pack 及支援 C# preview 的編譯器：

```powershell
MSBuild 撈金魚/撈金魚.csproj /restore /p:Configuration=Release
MSBuild tests/FireflyGrass.Tests.csproj
MSBuild tests/ForestHarvest.Tests.csproj
./tests/bin/FireflyGrass.Tests.exe
./tests/bin/ForestHarvest.Tests.exe
```

本地錄影、完整遊戲截圖、個人設定與帳密不提交；測試的錄影輸入為選用。細節見 [tests/README.md](tests/README.md) 及 [修改紀錄](docs/CHANGELOG.md)。

## 來源與授權狀態

原始專案：[SaviorDT/getFish](https://github.com/SaviorDT/getFish)。保留原始版權資訊。本次檢查未找到上游明確的 LICENSE；本倉庫不將上游程式碼重新宣告為 MIT 或其他開源授權。公開可見不等於授予再散布權利，散布前應確認原作者授權。遊戲畫面樣本、Flash Player 與第三方套件的權利屬各自權利人。

