using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ValueTaskSupplement;
using WindowTranslator.Modules;
using WindowTranslator.Plugin.GoogleAppsSctiptPlugin.Properties;

namespace WindowTranslator.Plugin.GoogleAppsSctiptPlugin;

public sealed class GasTranslator : ITranslateModule, IDisposable
{
    // 如果您在游戏设置里不填部署ID，软件会默认调用这个ID（您可以留空或改成您自己的）
    private const string DeployId = "AKfycby067ZUVXLwHXyu7O5nTDAOPkLf9pB4V8MEbnjNqthm5BmUCG7CR14pvw4THyEa_342";
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly LanguageOptions langOptions;
    private readonly HttpClient client;
    private readonly ILogger<GasTranslator> logger;

    public GasTranslator(IOptionsSnapshot<LanguageOptions> langOptions, IOptionsSnapshot<GasOptions> gasOptions, ILogger<GasTranslator> logger)
    {
        this.logger = logger;
        this.langOptions = langOptions.Value;
        var deployId = string.IsNullOrEmpty(gasOptions.Value.DeployId) ? DeployId : gasOptions.Value.DeployId;
        
        // 🌟 核心改定向：直接指向您的网页应用（Web App）匿名执行端点！
        this.client = new()
        {
            BaseAddress = new($"https://script.google.com/macros/s/{deployId}/exec"),
        };
    }

    public string Name => nameof(GasTranslator);

    public void Dispose()
        => this.client.Dispose();

    public async ValueTask<string[]> TranslateAsync(TextInfo[] srcTexts)
    {
        try
        {
            // 🌟 彻底拔除了原先繁琐的获取谷歌账号登录（GetCredential）和 Authorization 授权头注入逻辑。
            // 没有任何 Token 注入，完全匿名（anonymous）发送，绝对不会触发 403，而且永远不需要弹浏览器登录！
            var sourceTexts = srcTexts.Select(t => t.SourceText).ToArray();
            var req = new ScriptRunRequest("translate", [this.langOptions.Source.GetLangCode(), this.langOptions.Target.GetLangCode(), sourceTexts]);
            
            // 🌟 匿名发送标准的 JSON 请求给您的谷歌脚本
            var res = await this.client.PostAsJsonAsync(string.Empty, req, JsonSerializerOptions).ConfigureAwait(false);
            if (res.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound)
            {
                throw new(Resources.PermissionDenied);
            }
            res.EnsureSuccessStatusCode();
            
            var scriptResponse = await res.Content.ReadFromJsonAsync<ScriptRunResponse>(JsonSerializerOptions).ConfigureAwait(false);
            if (scriptResponse?.Error is { } error)
            {
                this.logger.LogWarning("Google翻訳のスクリプト実行エラー: {Message}", error.Message);
                throw new InvalidOperationException(Resources.UnexpectedResponse);
            }
            return scriptResponse?.Response?.Result ?? [];
        }
        catch (JsonException e)
        {
            this.logger.LogWarning("Google翻訳から予期しないレスポンスが返されました");
            throw new InvalidOperationException(Resources.UnexpectedResponse, e);
        }
    }

    private record ScriptRunRequest(string Function, object[] Parameters);
    private record ScriptRunResponse(bool Done, ScriptExecutionResponse? Response, ScriptRunError? Error);
    private record ScriptExecutionResponse(string[]? Result);
    private record ScriptRunError(int Code, string Message, string Status);
}

public class GasOptions : IPluginParam
{
    [DataType(DataType.Password)]
    public string DeployId { get; set; } = string.Empty;
}

file static class Extensions
{
    public static string GetLangCode(this string target)
        => target switch
        {
            "pt-BR" or "pt-PT" => target,
            "zh-Hant" => "zh-TW",
            "zh-Hans" => "zh-CN",
            var t => t[..2],
        };
}