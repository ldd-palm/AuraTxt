using AuraTxt.Cli.Commands;
using AuraTxt.Core.Services;

namespace AuraTxt.Cli.Menus;

public class InteractiveMenu(ConfigService config)
{
    public async Task RunAsync()
    {
        while (true)
        {
            Console.Clear();
            Console.WriteLine("AuraTxt 配置工具 (auracfg)");
            Console.WriteLine(new string('─', 30));
            Console.WriteLine(" [1] 模型平台管理");
            Console.WriteLine(" [2] 功能动作管理");
            Console.WriteLine(" [3] 界面设置");
            Console.WriteLine(" [0] 退出");
            Console.Write("\n请选择：");

            var key = Console.ReadKey(true).KeyChar;
            if (key == '0') break;
            if (key == '1') await ModelMenuAsync();
            else if (key == '2') await ActionMenuAsync();
            else if (key == '3') SettingsMenu();
        }
    }

    private async Task ModelMenuAsync()
    {
        Console.Clear();
        Console.WriteLine("── 模型平台管理 ──");
        Console.WriteLine("[1] 查看所有 [2] 添加 [3] 修改 [4] 删除 [0] 返回");
        Console.Write("选择：");
        var key = Console.ReadKey(true).KeyChar;
        Console.WriteLine();
        var cmd = new ModelCommand(config);
        if (key == '1') await cmd.ExecuteAsync(["--list"]);
        else if (key == '2') await AddModelInteractive(cmd);
        else if (key == '3') await EditModelInteractive(cmd);
        else if (key == '4') await DeleteModelInteractive(cmd);
        if (key != '0') { Console.Write("\n按任意键继续…"); Console.ReadKey(true); }
    }

    private static async Task AddModelInteractive(ModelCommand cmd)
    {
        Console.Write("平台 ID: ");      var id    = Console.ReadLine()?.Trim() ?? "";
        Console.Write("显示名称: ");     var name  = Console.ReadLine()?.Trim() ?? "";
        Console.Write("API Base URL: "); var url   = Console.ReadLine()?.Trim() ?? "";
        Console.Write("API Key: ");      var key   = Console.ReadLine()?.Trim() ?? "";
        Console.Write("模型名称: ");     var model = Console.ReadLine()?.Trim() ?? "";
        await cmd.ExecuteAsync(["--set","--id",id,"--display",name,"--url",url,"--key",key,"--model",model]);
    }

    private async Task EditModelInteractive(ModelCommand cmd)
    {
        await cmd.ExecuteAsync(["--list"]);
        Console.Write("\n输入要修改的 ID: ");
        var id = Console.ReadLine()?.Trim() ?? "";
        if (string.IsNullOrEmpty(id)) return;
        Console.WriteLine("（直接回车保持原值）");
        Console.Write("新显示名称: "); var name  = Console.ReadLine()?.Trim();
        Console.Write("新 URL: ");     var url   = Console.ReadLine()?.Trim();
        Console.Write("新 Key: ");     var key   = Console.ReadLine()?.Trim();
        Console.Write("新模型名: ");   var mdl   = Console.ReadLine()?.Trim();
        var a = new List<string> { "--update", "--id", id };
        if (!string.IsNullOrEmpty(name)) { a.Add("--display"); a.Add(name); }
        if (!string.IsNullOrEmpty(url))  { a.Add("--url");     a.Add(url);  }
        if (!string.IsNullOrEmpty(key))  { a.Add("--key");     a.Add(key);  }
        if (!string.IsNullOrEmpty(mdl))  { a.Add("--model");   a.Add(mdl);  }
        await cmd.ExecuteAsync(a.ToArray());
    }

    private async Task DeleteModelInteractive(ModelCommand cmd)
    {
        await cmd.ExecuteAsync(["--list"]);
        Console.Write("\n输入要删除的 ID: ");
        var id = Console.ReadLine()?.Trim() ?? "";
        if (string.IsNullOrEmpty(id)) return;
        Console.Write($"确认删除 '{id}'？(y/N) ");
        if (Console.ReadLine()?.Trim().ToLower() == "y")
            await cmd.ExecuteAsync(["--delete", "--id", id]);
    }

    private async Task ActionMenuAsync()
    {
        Console.Clear();
        Console.WriteLine("── 功能动作管理 ──");
        Console.WriteLine("[1] 查看所有 [2] 添加 [3] 修改 [4] 删除 [0] 返回");
        Console.Write("选择：");
        var key = Console.ReadKey(true).KeyChar;
        Console.WriteLine();
        var cmd = new ActionCommand(config);
        if (key == '1') await cmd.ExecuteAsync(["--list"]);
        else if (key == '2') await AddActionInteractive(cmd);
        else if (key == '3') await EditActionInteractive(cmd);
        else if (key == '4') await DeleteActionInteractive(cmd);
        if (key != '0') { Console.Write("\n按任意键继续…"); Console.ReadKey(true); }
    }

    private async Task AddActionInteractive(ActionCommand cmd)
    {
        var hv  = new HotkeyValidator();
        var cfg = config.Load();
        Console.Write("动作 ID: ");        var id        = Console.ReadLine()?.Trim() ?? "";
        Console.Write("动作名称: ");       var name      = Console.ReadLine()?.Trim() ?? "";
        Console.Write("图标 (lucide): ");  var icon      = Console.ReadLine()?.Trim() ?? "";
        Console.WriteLine("可用模型：");
        Console.WriteLine("  $google-translate  $youdao-dict");
        foreach (var (mid, m) in cfg.Models) Console.WriteLine($"  {mid} ({m.Alias})");
        Console.Write("绑定模型 ID: ");    var modelId   = Console.ReadLine()?.Trim() ?? "";
        Console.Write("是否交互式 (y/N): ");
        var interactive = (Console.ReadLine()?.Trim().ToLower() == "y").ToString().ToLower();
        Console.Write("Prompt 内容: ");    var prompt    = Console.ReadLine()?.Trim() ?? "";

        string hotkey = "";
        while (true)
        {
            Console.Write("快捷键 (留空跳过, Esc取消): ");
            var line = ReadLineWithEsc();
            if (line is null) { Console.WriteLine("已取消"); return; }
            if (string.IsNullOrEmpty(line)) break;
            var (res, conflict) = hv.Validate(line, cfg.Actions);
            if (res == HotkeyValidationResult.InvalidFormat)    { Console.WriteLine("格式无效，示例：Alt+T"); continue; }
            if (res == HotkeyValidationResult.SystemReserved)   { Console.WriteLine("系统保留热键"); continue; }
            if (res == HotkeyValidationResult.Conflict)         { Console.WriteLine($"已被「{conflict}」使用"); continue; }
            Console.Write($"设置快捷键为 {line}？(y/N) ");
            if (Console.ReadLine()?.Trim().ToLower() == "y") { hotkey = line; break; }
        }

        var args = new List<string>
            { "--set","--id",id,"--name",name,"--icon",icon,
              "--model-id",modelId,"--interactive",interactive,"--prompt",prompt };
        if (!string.IsNullOrEmpty(hotkey)) { args.Add("--hotkey"); args.Add(hotkey); }
        await cmd.ExecuteAsync(args.ToArray());
    }

    private async Task EditActionInteractive(ActionCommand cmd)
    {
        await cmd.ExecuteAsync(["--list"]);
        Console.Write("\n输入要修改的 ID: ");
        var id = Console.ReadLine()?.Trim() ?? "";
        if (string.IsNullOrEmpty(id)) return;
        Console.WriteLine("（直接回车保持原值）");
        Console.Write("新名称: ");    var name   = Console.ReadLine()?.Trim();
        Console.Write("新图标: ");    var icon   = Console.ReadLine()?.Trim();
        Console.Write("新 Prompt: "); var prompt = Console.ReadLine()?.Trim();

        var cfg = config.Load();
        var hv  = new HotkeyValidator();
        string hotkey = "";
        while (true)
        {
            Console.Write("新快捷键 (留空跳过, Esc取消): ");
            var line = ReadLineWithEsc();
            if (line is null) break;
            if (string.IsNullOrEmpty(line)) break;
            var (res, conflict) = hv.Validate(line, cfg.Actions, excludeId: id);
            if (res == HotkeyValidationResult.InvalidFormat)  { Console.WriteLine("格式无效"); continue; }
            if (res == HotkeyValidationResult.SystemReserved) { Console.WriteLine("系统保留热键"); continue; }
            if (res == HotkeyValidationResult.Conflict)       { Console.WriteLine($"已被「{conflict}」使用"); continue; }
            Console.Write($"设置快捷键为 {line}？(y/N) ");
            if (Console.ReadLine()?.Trim().ToLower() == "y") { hotkey = line; break; }
        }

        var args = new List<string> { "--update", "--id", id };
        if (!string.IsNullOrEmpty(name))   { args.Add("--name");   args.Add(name);   }
        if (!string.IsNullOrEmpty(icon))   { args.Add("--icon");   args.Add(icon);   }
        if (!string.IsNullOrEmpty(prompt)) { args.Add("--prompt"); args.Add(prompt); }
        if (!string.IsNullOrEmpty(hotkey)) { args.Add("--hotkey"); args.Add(hotkey); }
        await cmd.ExecuteAsync(args.ToArray());
    }

    private async Task DeleteActionInteractive(ActionCommand cmd)
    {
        await cmd.ExecuteAsync(["--list"]);
        Console.Write("\n输入要删除的 ID: ");
        var id = Console.ReadLine()?.Trim() ?? "";
        if (string.IsNullOrEmpty(id)) return;
        Console.Write($"确认删除 '{id}'？(y/N) ");
        if (Console.ReadLine()?.Trim().ToLower() == "y")
            await cmd.ExecuteAsync(["--delete", "--id", id]);
    }

    private void SettingsMenu()
    {
        Console.Clear();
        new SettingsCommand(config).ExecuteAsync(["--show"]).Wait();
        Console.WriteLine("\n修改设置（留空保持原值）：");
        Console.Write("字体大小: ");     var fs = Console.ReadLine()?.Trim();
        Console.Write("透明度 (0-1): "); var op = Console.ReadLine()?.Trim();
        Console.Write("触发延迟(ms): "); var dm = Console.ReadLine()?.Trim();
        var args = new List<string> { "--set" };
        if (!string.IsNullOrEmpty(fs)) { args.Add("--font-size"); args.Add(fs); }
        if (!string.IsNullOrEmpty(op)) { args.Add("--opacity");   args.Add(op); }
        if (!string.IsNullOrEmpty(dm)) { args.Add("--delay-ms");  args.Add(dm); }
        if (args.Count > 1)
            new SettingsCommand(config).ExecuteAsync(args.ToArray()).Wait();
        Console.Write("按任意键继续…"); Console.ReadKey(true);
    }

    private static string? ReadLineWithEsc()
    {
        var sb = new System.Text.StringBuilder();
        while (true)
        {
            var k = Console.ReadKey(true);
            if (k.Key == ConsoleKey.Escape) { Console.WriteLine(); return null; }
            if (k.Key == ConsoleKey.Enter)  { Console.WriteLine(); return sb.ToString(); }
            if (k.Key == ConsoleKey.Backspace && sb.Length > 0)
            { sb.Remove(sb.Length - 1, 1); Console.Write("\b \b"); continue; }
            if (k.KeyChar != '\0') { sb.Append(k.KeyChar); Console.Write(k.KeyChar); }
        }
    }
}
