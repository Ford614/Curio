# Curio - マウスカーソルインストーラー

[![Latest Release](https://img.shields.io/github/v/release/Ford614/Curio?label=Latest%20Release)](https://github.com/Ford614/Curio/releases/latest)
[![GitHub Stars](https://img.shields.io/github/stars/Ford614/Curio?style=flat)](https://github.com/Ford614/Curio/stargazers)
[![GitHub Downloads](https://img.shields.io/github/downloads/Ford614/Curio/total?label=Downloads)](https://github.com/Ford614/Curio/releases)

[🇯🇵 日本語] | [🇺🇸 English](README.en.md)

**[⬇️ Download Curio](https://github.com/Ford614/Curio/releases/latest)**　**[⭐ Star on GitHub](https://github.com/Ford614/Curio)**　**[❤️ Support / Donate](https://ko-fi.com/ford614)**

---

`Curio` は、Windows 10 / 11向けのマウスカーソル管理・編集アプリケーションです。

Web配布サイトからのダウンロード、ドラッグ＆ドロップ、Windowsの「プログラムから開く」など、さまざまな方法で `.cur` / `.ani` / `.zip` ファイルを取り込み、ファイルの自動検出・分類から、Windowsで使用できるカーソルスキームとしての登録・管理までをまとめて行えます。

さらに、内蔵のPaint Editorを使用してCUR / ANIカーソルの編集や、PNG / GIFからのカーソル作成も行えます。

---

## 🔥 主な特徴

### 1. ✨ Modern / OLD UIスタイル

* 「Modern」と「OLD」の2種類のUIスタイルに対応。
* 設定画面からいつでもUIスタイルを切り替えられます。
* カラーテーマは「システム」「ライト」「ダーク」に対応。
* UIスタイルとテーマの設定は `%LOCALAPPDATA%\Curio\settings.json` に保存され、次回起動時にも自動的に反映されます。

### 2. 🌐 Webから直接カーソルを追加

* Curioに内蔵されたWebブラウザでカーソル配布サイトを閲覧できます。
* `.cur` / `.ani` / `.zip` ファイルのダウンロードを自動検知し、Curioへ取り込めます。
* デフォルトのWebサイトを設定から変更できます。

### 3. 📥 ドラッグ＆ドロップ

Windowsのエクスプローラーから以下のファイルやフォルダをCurioへ直接ドラッグ＆ドロップできます。

* `.cur`
* `.ani`
* `.zip`
* カーソルを含むフォルダ

### 4. 🔗 Windowsファイル関連付け・「プログラムから開く」「送る」対応

* Windowsの「プログラムから開く」からCurioを選択してファイルを開けます。
* 「送る」などのWindowsのファイル操作からCurioへファイルを渡せます。
* コマンドライン引数によるファイルの読み込みにも対応しています。

### 5. 🛡️ 厳格なファイル取り込みセキュリティ

Curioでは、外部から取得したファイルを安全に取り込めるよう、以下の対策を行っています。

* `.exe` / `.msi` / `.bat` / `.cmd` / `.ps1` などの実行可能ファイルを自動的に遮断
* ZIP展開時のディレクトリトラバーサル（Zip Slip）を防止
* ファイルサイズの上限チェック
* カーソルファイル以外の不要なファイルを自動的に除外

Curioは取り込んだ実行可能ファイルを実行することを目的としていません。

### 6. 🔍 自動検出 & キーワードマッピング

ファイル名からカーソルの役割を自動的に判定し、Windowsのカーソル役割へ割り当てます。

例：

* 通常選択
* ヘルプ選択
* バックグラウンドで作業中
* 待ち状態
* テキスト選択
* 手書き
* 利用不可
* 上下左右のサイズ変更
* 移動
* 代替選択
* リンク選択
* 場所の選択
* 人の選択

など。

### 7. 🖱️ カーソルプレビュー

`.cur` / `.ani` ファイルを読み込み、カーソルのプレビューを一覧で確認できます。

### 8. 🔐 管理者権限不要

Curioはユーザー単位の設定・保存領域を使用するため、通常の利用では管理者権限を必要としません。

カーソルスキームはユーザー単位で管理されます。

### 9. 📁 保存先フォルダの設定

一括登録したカーソルファイルの保存先を設定画面から変更できます。

デフォルトの保存先：

```text
%LOCALAPPDATA%\Curio\Schemes\
```

### 10. 📋 カーソルスキーム管理

登録済みのカーソルスキームをCurioから管理できます。

* 登録済みスキームの一覧表示
* スキームの選択
* スキームの適用
* スキームの削除

などに対応しています。

### 11. 🌐 日本語 / English UI

Curioは以下の言語に対応しています。

* 日本語（ja-JP）
* English（en-US）

設定画面から言語を切り替えられます。

メイン画面、設定画面、Paint Editor、ANIタイムライン、主要なダイアログなどが選択した言語へ切り替わります。

### 12. 🎨 内蔵 Paint Editor

Curioにはカーソルを編集・作成するためのPaint Editorが内蔵されています。

対応機能：

* CUR編集
* ANIフレーム編集
* PNG読み込み
* GIF読み込み
* ペン
* 消しゴム
* スポイト
* 塗りつぶし
* 範囲選択
* コピー / ペースト
* Undo / Redo
* Hotspot編集
* ピクセルグリッド
* キャンバスサイズ変更
* フレーム追加
* フレーム複製
* フレーム削除
* フレーム並べ替え
* フレームごとの表示時間設定
* アニメーションプレビュー
* ANI保存

CUR / ANIを読み込んで編集したり、PNG / GIFからカーソルを作成したりできます。

---

## 🛠️ 動作環境

* **OS:** Windows 10 / Windows 11
* **アーキテクチャ:** x64
* **Webブラウザ機能:** Microsoft Edge WebView2 Runtime

Curioの公式配布版（MSI / ZIP）は **.NET 10 self-contained** でビルドされています。

そのため、Curioを実行するために.NET 10 Desktop Runtimeを別途インストールする必要はありません。

Webブラウザ機能を使用する場合は、Microsoft Edge WebView2 Runtimeが必要です。

---

## 📦 インストール方法

### MSI版

通常はこちらがおすすめです。

1. GitHub Releasesから `Curio-v1.0.0-win-x64.msi` をダウンロードします。
2. MSIファイルをダブルクリックしてインストーラーを起動します。
3. インストール先を選択します。
4. 必要に応じてスタートメニューやデスクトップのショートカットを選択します。
5. インストールを実行します。
6. スタートメニューまたはデスクトップからCurioを起動します。

### ZIP版

インストールせずに使用したい場合はこちらを利用できます。

1. GitHub Releasesから `Curio-v1.0.0-win-x64.zip` をダウンロードします。
2. ZIPファイルを任意の場所へ展開します。
3. 展開したフォルダ内の `Curio.exe` を実行します。

ZIP版ではWindowsへのインストール作業は必要ありません。

---

## 📖 使い方

### 1. UIスタイル・テーマの設定

1. 「⚙️ 設定」タブを開きます。
2. 「UIスタイル」から「Modern」または「OLD」を選択します。
3. 「テーマ」から「システム」「ライト」「ダーク」を選択します。
4. 設定は `%LOCALAPPDATA%\Curio\settings.json` に保存され、次回起動時にも自動的に適用されます。

### 2. 言語の変更

1. 「⚙️ 設定」タブを開きます。
2. 「言語」から「日本語」または「English」を選択します。
3. 選択した言語がCurioのUIへ反映されます。

### 3. カーソルの登録

1. 「📂 カーソル一括登録」タブを開きます。
2. フォルダを指定するか、カーソルファイルやフォルダをドラッグ＆ドロップします。
3. 必要に応じてCurio内蔵のWebブラウザからカーソルをダウンロードします。
4. 検出されたファイルを確認します。
5. 自動的に割り当てられたカーソルの役割を確認・調整します。
6. 「⚡ 一括インストール」をクリックします。
7. カーソルスキームがWindowsへ登録されます。

### 4. カーソルの編集

1. CUR / ANIファイルをCurioへ読み込みます。
2. Paint Editorを開きます。
3. ペン、消しゴム、スポイト、塗りつぶし、選択などのツールを使用します。
4. 必要に応じてHotspotやキャンバスサイズを変更します。
5. ANIの場合はタイムラインからフレームを編集します。
6. 編集したカーソルをCURまたはANIとして保存します。

---

## 📁 設定ファイル・保存先

### 設定ファイル

```text
%LOCALAPPDATA%\Curio\settings.json
```

### カーソルスキームの保存先

```text
%LOCALAPPDATA%\Curio\Schemes\<スキーム名>\
```

### Windowsへのスキーム登録

```text
HKEY_CURRENT_USER\Control Panel\Cursors\Schemes
```

Curioはユーザー単位でカーソルスキームを管理します。

---

## 🌐 WebView2について

CurioのWebブラウザ機能にはMicrosoft Edge WebView2を使用しています。

WebView2 Runtimeがインストールされていない環境では、CurioのWebブラウザ機能が利用できない場合があります。

Webブラウザを使用しない場合でも、Curio本体のカーソル管理・編集機能は利用できます。

---

## 📄 ライセンス

Curio本体は **Curio Source Available License Version 1.0** のもとで公開されています。

Curioには第三者プロジェクトのソースコードが含まれています。

第三者コンポーネントおよび各ライセンスについては、[`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md) を参照してください。

---

## 🙏 Third-Party Software

Curioでは、カーソル編集機能の一部に第三者プロジェクトのソースコードを利用しています。

### Cursor-Palette

* Repository: https://github.com/DoomSalat/Cursor-Palette
* License: MIT License
* Copyright: Copyright (c) 2026 Capitan Salat

適用されるMIT Licenseは以下に含まれています。

```text
ThirdParty/CursorPalette/LICENSE
```

第三者コードのライセンスはCurio本体のライセンスとは異なります。

詳細については `THIRD-PARTY-NOTICES.md` を参照してください。

---

## 🔗 Links

* **GitHub:** https://github.com/Ford614/Curio
* **Releases:** https://github.com/Ford614/Curio/releases

---

## ⭐ Support Curio

Curioが役に立った場合は、GitHubで⭐を付けてもらえると開発の励みになります。

Curioの開発を支援していただける場合は、以下からDonateできます。

**[❤️ Support / Donate](https://ko-fi.com/ford614)**

バグ報告、機能提案、改善案などもGitHub Issuesから受け付けています。

---

## ⚠️ 注意事項

CurioはWindowsのカーソルスキームやレジストリを変更します。

カーソルスキームの削除や保存先フォルダの変更を行う場合は、内容を確認してから操作してください。

Curioで扱うファイルについても、信頼できる配布元から取得したものを使用することを推奨します。
