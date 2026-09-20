using System;
using System.Collections.Generic;

namespace Curio.Models
{
    public class CursorRole
    {
        public int Index { get; set; }
        public string RegistryKey { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> Keywords { get; set; } = new();

        public static List<CursorRole> GetStandardRoles()
        {
            return new List<CursorRole>
            {
                new CursorRole
                {
                    Index = 0,
                    RegistryKey = "Arrow",
                    DisplayName = "通常の選択 (Normal)",
                    Description = "通常の矢印カーソル",
                    Keywords = new List<string> { "arrow", "normal", "ptr", "default", "pointer", "通常", "矢印", "標準" }
                },
                new CursorRole
                {
                    Index = 1,
                    RegistryKey = "Help",
                    DisplayName = "ヘルプの選択 (Help)",
                    Description = "ヘルプ表示時のカーソル",
                    Keywords = new List<string> { "help", "question", "ヘルプ", "疑問", "助け" }
                },
                new CursorRole
                {
                    Index = 2,
                    RegistryKey = "AppStarting",
                    DisplayName = "バックグラウンドで作業中 (Working)",
                    Description = "処理中（操作可能）のカーソル",
                    Keywords = new List<string> { "appstarting", "working", "progress", "バックグラウンド", "処理中", "作業中" }
                },
                new CursorRole
                {
                    Index = 3,
                    RegistryKey = "Wait",
                    DisplayName = "待ち時間 (Busy)",
                    Description = "処理中（操作不可）のカーソル",
                    Keywords = new List<string> { "wait", "busy", "sandglass", "hourglass", "watch", "待ち時間", "待ち", "ビジー" }
                },
                new CursorRole
                {
                    Index = 4,
                    RegistryKey = "Crosshair",
                    DisplayName = "領域選択 (Precision)",
                    Description = "精密な選択・十字カーソル",
                    Keywords = new List<string> { "crosshair", "cross", "precision", "target", "領域選択", "精密", "十字" }
                },
                new CursorRole
                {
                    Index = 5,
                    RegistryKey = "IBeam",
                    DisplayName = "テキスト選択 (Text)",
                    Description = "文字入力・選択カーソル",
                    Keywords = new List<string> { "ibeam", "beam", "text", "select_text", "テキスト選択", "テキスト", "文字", "アイビーム" }
                },
                new CursorRole
                {
                    Index = 6,
                    RegistryKey = "NWPen",
                    DisplayName = "手書き (Handwriting)",
                    Description = "ペン・手書き入力カーソル",
                    Keywords = new List<string> { "nwpen", "pen", "write", "handwriting", "手書き", "ペン" }
                },
                new CursorRole
                {
                    Index = 7,
                    RegistryKey = "No",
                    DisplayName = "利用不可 (Unavailable)",
                    Description = "操作禁止・ドロップ不可カーソル",
                    Keywords = new List<string> { "no", "unavail", "forbidden", "stop", "disabled", "drop_no", "利用不可", "不可", "禁止" }
                },
                new CursorRole
                {
                    Index = 8,
                    RegistryKey = "SizeNS",
                    DisplayName = "上下の拡大/縮小 (Vertical)",
                    Description = "垂直方向のリサイズカーソル",
                    Keywords = new List<string> { "sizens", "ns", "resize_ns", "vert", "vertical", "上下の拡大", "上下", "縦" }
                },
                new CursorRole
                {
                    Index = 9,
                    RegistryKey = "SizeWE",
                    DisplayName = "左右の拡大/縮小 (Horizontal)",
                    Description = "水平方向のリサイズカーソル",
                    Keywords = new List<string> { "sizewe", "we", "resize_we", "horz", "horizontal", "左右の拡大", "左右", "横" }
                },
                new CursorRole
                {
                    Index = 10,
                    RegistryKey = "SizeNWSE",
                    DisplayName = "斜め 1 の拡大/縮小 (Diagonal 1)",
                    Description = "左上-右下方向のリサイズカーソル",
                    Keywords = new List<string> { "sizenwse", "nwse", "resize_nwse", "diag1", "斜め1", "斜め１", "左上" }
                },
                new CursorRole
                {
                    Index = 11,
                    RegistryKey = "SizeNESW",
                    DisplayName = "斜め 2 の拡大/縮小 (Diagonal 2)",
                    Description = "右上-左下方向のリサイズカーソル",
                    Keywords = new List<string> { "sizenesw", "nesw", "resize_nesw", "diag2", "斜め2", "斜め２", "右上" }
                },
                new CursorRole
                {
                    Index = 12,
                    RegistryKey = "SizeAll",
                    DisplayName = "移動 (Move)",
                    Description = "オブジェクト移動カーソル",
                    Keywords = new List<string> { "sizeall", "move", "all", "resize_all", "移動" }
                },
                new CursorRole
                {
                    Index = 13,
                    RegistryKey = "UpArrow",
                    DisplayName = "代替選択 (Alternate)",
                    Description = "上向き矢印・代替選択カーソル",
                    Keywords = new List<string> { "uparrow", "up", "alternate", "alt", "代替選択", "上向き" }
                },
                new CursorRole
                {
                    Index = 14,
                    RegistryKey = "Hand",
                    DisplayName = "リンクの選択 (Link)",
                    Description = "ハイパーリンク・手カーソル",
                    Keywords = new List<string> { "hand", "link", "point", "finger", "リンクの選択", "リンク", "手" }
                },
                new CursorRole
                {
                    Index = 15,
                    RegistryKey = "Pin",
                    DisplayName = "場所の選択 (Location)",
                    Description = "ピン・位置選択カーソル (Win10/11)",
                    Keywords = new List<string> { "pin", "location", "place", "場所の選択", "場所", "ピン" }
                },
                new CursorRole
                {
                    Index = 16,
                    RegistryKey = "Person",
                    DisplayName = "人の選択 (Person)",
                    Description = "人物・ユーザー選択カーソル (Win10/11)",
                    Keywords = new List<string> { "person", "human", "user", "人の選択", "人", "ユーザー" }
                }
            };
        }
    }
}
