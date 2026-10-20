using System.Collections.Generic;
using BrickDefense.Core;

namespace BrickDefense.Presentation
{
    // Presentation translates known diagnostics; report codes and module contracts stay unchanged.
    internal static class ChineseUi
    {
        public static string Error(Issue issue)
        {
            switch(issue.Code)
            {
                case "BATTLE_LEVEL_INVALID": return "至少选择一个空白出生点，且每个出生点都要有通往核心的路线。";
                case "BATTLE_ROUTE_INVALID": return "路线必须沿相邻的可通行格到达目标核心。";
                case "BATTLE_ALREADY_RUNNING": return "请先结束当前战斗。";
                case "BATTLE_NOT_FINISHED": return "战斗结束后才能重试。";
                case "WAVE_PLAN_MISSING": return "请填写波次设置。";
                case "WAVE_COUNT_INVALID": return "波数必须为 1～20。";
                case "WAVE_SIZE_INVALID": return "每波敌人总数必须为 1～50。";
                case "WAVE_TOTAL_INVALID": return "一局的敌人总数不能超过 400。";
                case "ENEMY_HEALTH_INVALID": return "敌人生命必须为 1～10000。";
                case "ENEMY_SPEED_INVALID": return "速度必须为 0.1～20 格/秒。";
                case "CORE_DAMAGE_INVALID": return "抵达核心的伤害必须为 1～1000。";
                case "SPAWN_INTERVAL_INVALID": return "出怪间隔必须为 0.1～10 秒。";
                case "COMBAT_CONFIG_INVALID": return "请检查场景中的核心生命与防御塔参数。";
                case "NO_SPAWNS": return "请至少选择一个空白出生点。";
                case "SPAWN_DUPLICATE": return "同一格不能重复设为出生点。";
                case "SPAWN_OUT_OF_BOUNDS": return "请在棋盘内选择出生点。";
                case "SPAWN_OCCUPIED": return "出生点必须是空白格。";
                case "NO_ROUTE": return "出生点没有通往任何核心的路线。";
                default: return Message(issue.Message);
            }
        }

        private static readonly Dictionary<string,string> Messages=new Dictionary<string,string>
        {
            {"Camera permission was not granted.","未获得相机权限，请在设备设置中允许访问相机。"},
            {"No camera found. Import a photo instead.","未找到相机，请改为导入照片。"},
            {"Keep every cube fully visible on a plain background and photograph from above.","请使用干净背景，从正上方拍摄，确保全部积木完整入镜。"},
            {"Camera did not start. Check device permissions or import a photo.","相机未能启动，请检查权限或改为导入照片。"},
            {"The camera is not ready.","相机尚未准备好。"},
            {"Choose a PNG or JPG photo.","请选择 PNG 或 JPG 照片。"},
            {"Photo missing or larger than 32 MB.","照片不存在，或文件超过 32 MB。"},
            {"Could not decode this image.","无法读取这张图片。"},
            {"Resize the photo to a maximum side of 4096 pixels.","请将照片最长边缩小到 4096 像素以内。"},
            {"No photo is loaded.","尚未导入照片。"},
            {"A photo must be ready and idle.","请先导入照片，并等待当前分析结束。"},
            {"Color calibration is incomplete.","颜色标定尚未完成，请采样背景和三种角色颜色。"},
            {"Only a completed successful scan can be confirmed.","只有分析成功的照片才能生成关卡。"},
            {"Invalid scan record ID.","扫描记录编号无效。"}
        };
        public static string Message(string value) => value!=null && Messages.TryGetValue(value,out string chinese)?chinese:value;
    }
}
