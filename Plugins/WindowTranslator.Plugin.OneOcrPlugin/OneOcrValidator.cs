using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WindowTranslator.Modules;
using WindowTranslator.Plugin.OneOcrPlugin.Properties;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;

namespace WindowTranslator.Plugin.OneOcrPlugin;

public class OneOcrValidator(IContentDialogService dialogService, ILogger<OneOcrValidator> logger) : ITargetSettingsValidator
{
    private static readonly ValidateResult Invalid = ValidateResult.Invalid(Resources.OneOcr, Resources.NotFoundModule);
    private readonly IContentDialogService dialogService = dialogService;
    private readonly ILogger<OneOcrValidator> logger = logger;

    public async ValueTask<ValidateResult> Validate(TargetSettings settings)
    {
        // 如果当前没有选中 OneOcr，直接放行
        if (settings.SelectedPlugins[nameof(IOcrModule)] != nameof(OneOcr))
        {
            return ValidateResult.Valid;
        }

        try
        {
            // 获取引擎底层在运行期间硬编码期望的真实目录：C:\ProgramData\StudioFreesia\WindowTranslator\OneOcr
            var commonAppData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            var targetDir = Path.Combine(commonAppData, "StudioFreesia", "WindowTranslator", "OneOcr");

            // 1. 如果该目标文件夹不存在，我们直接帮它创建好（避免写入 Error.txt 时发生闪退）
            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            // 我们需要验证这三个引擎核心文件在目标运行目录中是否存在
            var requiredFiles = new[] { "oneocr.dll", "oneocr.onemodel", "onnxruntime.dll" };

            // 2. 检查这三个文件是否已经在目标运行目录中
            foreach (var file in requiredFiles)
            {
                var filePath = Path.Combine(targetDir, file);
                if (!File.Exists(filePath))
                {
                    this.logger?.LogWarning("OneOcr 运行目录文件缺失: {file}，路径: {filePath}", file, filePath);
                    
                    // 如果缺少，返回友好的报错，指导用户如何放文件
                    return ValidateResult.Invalid(
                        Resources.OneOcr, 
                        $"找不到依赖模块。请手动将以下三个文件：\n- oneocr.dll\n- oneocr.onemodel\n- onnxruntime.dll\n\n直接复制到这个系统文件夹下：\n{targetDir}"
                    );
                }
            }

            this.logger?.LogInformation("OneOcr 运行目录文件验证成功，直接放行加载。");
            return ValidateResult.Valid;
        }
        catch (Exception ex)
        {
            this.logger?.LogError(ex, "验证 OneOcr 本地运行目录时出错");
            return ValidateResult.Invalid(Resources.OneOcr, $"验证出错: {ex.Message}");
        }
    }
}