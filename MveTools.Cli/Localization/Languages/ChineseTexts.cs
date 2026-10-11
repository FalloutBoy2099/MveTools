namespace MveTools.Cli.Localization.Languages
{
	using System;
	using System.Collections.Generic;

	internal static class ChineseTexts
	{
		public static IReadOnlyDictionary<String, String> Entries { get; } = new Dictionary<String, String>(StringComparer.Ordinal)
		{
			["MainMenuTitle"] = "你想做什么？",
			["InspectAction"] = "检查 MVE",
			["ToVideoAction"] = "将 MVE 转换为视频",
			["ToMveAction"] = "将视频转换为 MVE",
			["LanguageAction"] = "更改语言",
			["QuitAction"] = "退出",
			["LanguageTitle"] = "语言",
			["LanguageChanged"] = "{0}",
			["TargetVideo"] = "目标视频",
			["SourceVideo"] = "源视频（AVI 或 MP4）",
			["TargetMve"] = "目标 MVE",
			["FormatTitle"] = "输出格式",
			["AutoFormat"] = "自动：{0}",
			["FileNotFound"] = "找不到文件。",
			["RequiredPath"] = "必须提供路径。",
			["Done"] = "完成",
			["Step"] = "步骤",
			["FormatMp4"] = "MP4/H.264（系统 ffmpeg）"
		};
	}
}