using System.Text;
using System.Text.Json;
using Bee.CTOS.PreShipmentRestacking.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Phenix.Core.DependencyInjection;

namespace Bee.CTOS.PreShipmentRestacking.Domain
{
    /// <summary>
    /// 策略优化
    /// </summary>
    [Service(typeof(IStrategyOptimizingService))]
    public class StrategyOptimizingService : IStrategyOptimizingService, IDisposable
    {
        public StrategyOptimizingService(ILogger<StrategyOptimizingService> logger, IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            _httpClient = httpClientFactory.CreateClient("StrategyOptimizer");
            _httpClient.Timeout = TimeSpan.FromSeconds(60);
        }

        #region 属性

        private readonly ILogger<StrategyOptimizingService> _logger;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        #endregion

        #region 定义

        // 优化结果
        public class OptimizationResult
        {
            public bool Success { get; set; }
            public string OriginalStrategy { get; set; } = string.Empty;
            public string OptimizedStrategy { get; set; } = string.Empty;
            public string Summary { get; set; } = string.Empty;
            public List<string> Errors { get; set; } = new();
            public DateTime Timestamp { get; set; } = DateTime.UtcNow;
            public double ImprovementScore { get; set; }
            public string AnalysisReport { get; set; } = string.Empty;
        }

        /// <summary>
        /// 优化历史记录
        /// </summary>
        private class OptimizationHistory
        {
            public DateTime Timestamp { get; set; }
            public string FileName { get; set; } = string.Empty;
            public double ImprovementScore { get; set; }
            public int StrategyLength { get; set; }
        }

        #endregion

        #region 方法

        async Task<string> IStrategyOptimizingService.ExecuteStrategyOptimizingAsync(string constraints, string strategy, string pendingPart, string aiKey, string aiModel)
        {
            _logger.LogInformation("开始执行策略优化流程");

            OptimizationResult result = new OptimizationResult
            {
                OriginalStrategy = strategy
            };

            try
            {
                _logger.LogInformation("调用AI进行策略优化...");
                result.AnalysisReport = await CallAIForAnalysisAsync(constraints, strategy, pendingPart, aiKey, aiModel);

                _logger.LogInformation("调用AI生成新策略代码...");
                result.OptimizedStrategy = await CallAIForCodeGenerationAsync(result.AnalysisReport, aiKey, aiModel);

                _logger.LogInformation("验证新策略...");
                bool validationResult = await SimpleValidateAsync(result.OptimizedStrategy, constraints);

                if (validationResult)
                {
                    result.Success = true;
                    result.ImprovementScore = CalculateImprovementScore(result.AnalysisReport, strategy, result.OptimizedStrategy);
                    result.Summary = $"策略优化完成！改进评分: {result.ImprovementScore:F2}/10.0。原始策略长度: {strategy.Length} 字符，新策略长度: {result.OptimizedStrategy.Length} 字符";
                    
                    _logger.LogInformation($"✅ 策略优化成功！改进评分: {result.ImprovementScore:F2}");
                }
                else
                {
                    result.Success = false;
                    result.Errors.Add("新策略验证失败");
                    result.OptimizedStrategy = strategy; // 保持原策略
                    result.Summary = "策略优化失败，保持原策略。";

                    _logger.LogWarning("❌ 策略优化验证失败");
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"优化失败: {ex.Message}");
                result.Summary = $"优化过程发生异常: {ex.Message}";

                _logger.LogError(ex, "策略优化过程发生异常");
            }

            // 返回JSON格式的结果
            return JsonSerializer.Serialize(result, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }

        /// <summary>
        /// 调用AI进行策略优化
        /// </summary>
        private async Task<string> CallAIForAnalysisAsync(string constraints, string strategy, string pendingPart, string apiKey, string aiModel)
        {
            try
            {
                string prompt = BuildAnalysisPrompt(strategy, constraints);

                _logger.LogDebug($"调用AI进行策略优化，策略长度: {strategy.Length}，约束长度: {constraints.Length}");

                string response = await CallAIAsync(prompt, apiKey, aiModel);

                _logger.LogInformation($"AI策略优化完成，响应长度: {response.Length}");

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "调用AI进行策略优化失败");
                throw;
            }
        }

        /// <summary>
        /// 调用AI生成代码
        /// </summary>
        private async Task<string> CallAIForCodeGenerationAsync(string analysis, string apiKey, string aiModel)
        {
            try
            {
                string prompt = BuildCodeGenerationPrompt(analysis);

                _logger.LogDebug("调用AI生成代码，策略长度: {AnalysisLength}", analysis.Length);

                string response = await CallAIAsync(prompt, apiKey, aiModel);

                _logger.LogInformation("AI代码生成完成，代码长度: {CodeLength}", response.Length);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "调用AI生成代码失败");
                throw;
            }
        }

        /// <summary>
        /// 调用AI API
        /// </summary>
        private async Task<string> CallAIAsync(string prompt, string apiKey, string aiModel)
        {
            try
            {
                var requestData = new
                {
                    model = aiModel,
                    messages = new[]
                    {
                        new
                        {
                            role = "user",
                            content = prompt
                        }
                    },
                    temperature = 0.7,
                    max_tokens = 4000
                };

                string jsonContent = JsonSerializer.Serialize(requestData);
                StringContent content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

                using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(45)); // 添加超时和重试机制
                HttpResponseMessage response = await _httpClient.PostAsync(_httpClient.BaseAddress, content, cts.Token);
                response.EnsureSuccessStatusCode();

                string responseJson = await response.Content.ReadAsStringAsync(cts.Token);
                using JsonDocument doc = JsonDocument.Parse(responseJson);

                string result = doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString() ?? string.Empty;

                return result;
            }
            catch (TaskCanceledException)
            {
                throw new TimeoutException("AI API调用超时");
            }
            catch (HttpRequestException httpEx)
            {
                throw new Exception($"AI API调用失败: {httpEx.Message}", httpEx);
            }
        }

        /// <summary>
        /// 简单验证策略
        /// </summary>
        private Task<bool> SimpleValidateAsync(string strategy, string constraints)
        {
            bool isValid = true;
            List<string> errors = new List<string>();

            try
            {
                // 基本检查1: 非空
                if (string.IsNullOrWhiteSpace(strategy))
                {
                    isValid = false;
                    errors.Add("策略代码为空");
                }

                // 基本检查2: 包含必要的C#类结构
                if (!strategy.Contains("class") || (!strategy.Contains("public") && !strategy.Contains("internal")))
                {
                    isValid = false;
                    errors.Add("策略代码缺少类定义或访问修饰符");
                }

                // 基本检查3: 检查是否包含危险代码
                string[] dangerousPatterns = new[]
                {
                    "System.IO.File.Delete",
                    "System.Diagnostics.Process.Start",
                    "System.Reflection.Emit",
                    "System.Management",
                    "System.Data.SqlClient"
                };

                foreach (string? pattern in dangerousPatterns)
                {
                    if (strategy.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    {
                        isValid = false;
                        errors.Add($"策略包含潜在危险代码: {pattern}");
                    }
                }

                // 基本检查4: 检查语法错误（简单的C#语法检查）
                if (!CheckCSharpSyntax(strategy, out var syntaxErrors))
                {
                    isValid = false;
                    errors.AddRange(syntaxErrors);
                }

                // 基本检查5: 确保实现了关键接口
                if (!strategy.Contains("IStrategy") && !strategy.Contains("Execute") && !strategy.Contains("Optimize"))
                {
                    _logger.LogWarning("策略代码可能缺少关键方法，但这不是致命错误");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "策略验证过程中发生异常");
                isValid = false;
                errors.Add($"验证异常: {ex.Message}");
            }

            if (!isValid)
            {
                _logger.LogWarning("策略验证失败: {Errors}", string.Join("; ", errors));
            }
            else
            {
                _logger.LogInformation("策略验证通过");
            }

            return Task.FromResult(isValid);
        }

        /// <summary>
        /// 检查C#基本语法
        /// </summary>
        private bool CheckCSharpSyntax(string code, out List<string> errors)
        {
            errors = new List<string>();

            // 检查括号匹配
            int openBraces = code.Count(c => c == '{');
            int closeBraces = code.Count(c => c == '}');
            if (openBraces != closeBraces)
            {
                errors.Add($"大括号不匹配: 左大括号={openBraces}, 右大括号={closeBraces}");
            }

            // 检查分号（基本语句结束符）
            string[] lines = code.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (!string.IsNullOrWhiteSpace(line) &&
                    !line.StartsWith("//") &&
                    !line.StartsWith("/*") &&
                    !line.EndsWith("{") &&
                    !line.EndsWith("}") &&
                    !line.Contains("class ") &&
                    !line.Contains("interface ") &&
                    !line.Contains("namespace ") &&
                    !line.Contains("using ") &&
                    !line.EndsWith(";") &&
                    !line.EndsWith("{") &&
                    !line.EndsWith("}"))
                {
                    // 这可能是一个语法错误，但放宽检查
                    if (line.Contains("=") || line.Contains("return") || line.Contains("if ") || line.Contains("for ") || line.Contains("foreach "))
                        _logger.LogDebug("第 {LineNumber} 行可能缺少分号: {Line}", i + 1, line);
                }
            }

            return errors.Count == 0;
        }

        /// <summary>
        /// 计算改进评分
        /// </summary>
        private double CalculateImprovementScore(string analysisReport, string oldStrategy, string newStrategy)
        {
            // 这里实现改进评分的计算逻辑
            // 可以根据分析报告、新旧策略的比较来计算评分

            double score = 0.0;

            // 基于分析报告的关键词加分
            if (analysisReport.Contains("优化") || analysisReport.Contains("改进"))
                score += 2.0;
            if (analysisReport.Contains("性能") || analysisReport.Contains("效率"))
                score += 1.5;
            if (analysisReport.Contains("可读性") || analysisReport.Contains("可维护性"))
                score += 1.0;
            if (analysisReport.Contains("错误处理") || analysisReport.Contains("健壮性"))
                score += 1.5;
            if (analysisReport.Contains("扩展性") || analysisReport.Contains("配置化"))
                score += 1.0;

            // 基于代码质量的加分
            int oldLines = oldStrategy.Split('\n').Length;
            int newLines = newStrategy.Split('\n').Length;

            // 适当的代码增长是可以接受的
            if (newLines > oldLines * 0.8 && newLines < oldLines * 2)
                score += 0.5;

            // 检查新代码是否包含更多注释
            double oldCommentRatio = CountLinesStartingWith(oldStrategy, "//") / (double)Math.Max(1, oldLines);
            double newCommentRatio = CountLinesStartingWith(newStrategy, "//") / (double)Math.Max(1, newLines);

            if (newCommentRatio > oldCommentRatio)
                score += 0.5;

            // 确保分数在0-10之间
            return Math.Max(0, Math.Min(10, score));
        }

        private int CountLinesStartingWith(string text, string prefix)
        {
            return text.Split('\n').Count(line => line.Trim().StartsWith(prefix));
        }

        /// <summary>
        /// 构建分析提示词
        /// </summary>
        private string BuildAnalysisPrompt(string strategy, string constraints)
        {
            return $"""
你是一个集装箱翻箱策略优化专家，同时也是资深的C#架构师。请分析以下策略代码，并提出改进建议。

## 📋 约束条件
{constraints}

## 💻 当前策略代码
```csharp
{strategy}
```

## 🔍 分析要求
请从以下角度进行分析：
1. **性能优化**：时间复杂度、空间复杂度、算法效率
2. **安全性**：是否符合所有约束条件，有无安全漏洞
3. **可维护性**：代码结构、注释、命名规范
4. **可扩展性**：是否容易添加新功能，是否符合设计模式
5. **健壮性**：错误处理、边界条件、异常情况
6. **可测试性**：是否易于编写单元测试

## 📝 输出要求
请提供：
1. **问题列表**：详细列出发现的所有问题
2. **改进建议**：针对每个问题的具体优化方案
3. **新策略设计**：改进后的算法思路和伪代码
4. **预期收益**：优化后的性能提升预期

## 🎯 重点关注
- 择排策略的优化
- 清排策略的效率
- 代码的可读性和可维护性
- 错误处理和边界情况

请用中文回答，保持专业性和实用性。
""";
        }

        /// <summary>
        /// 构建代码生成提示词
        /// </summary>
        private string BuildCodeGenerationPrompt(string analysis)
        {
            return $"""
你是一个专业的C#开发工程师，擅长编写高质量的生产代码。请根据以下分析结果，生成完整的C#代码实现。

## 📊 策略分析结果
{analysis}

## 💻 代码要求
1. **完整性**：生成完整的C#类实现，包含所有必要的方法和属性
2. **可读性**：包含详细的XML文档注释和必要的代码注释
3. **健壮性**：完善的错误处理机制（try-catch、输入验证等）
4. **性能**：优化算法效率，合理使用数据结构和缓存
5. **可测试性**：代码结构便于单元测试
6. **规范性**：遵循C#编码规范和命名约定

## 🎯 核心要求
- 必须实现完整的翻箱策略逻辑
- 必须包含择排策略和清排策略
- 必须处理所有约束条件
- 必须包含日志记录
- 必须支持依赖注入

## 📁 代码结构
请按以下结构组织代码：
1. 命名空间定义
2. 主策略类
3. 数据模型类
4. 配置类
5. 异常类

只输出C#代码，不要包含其他解释。确保代码可以直接编译运行。
""";
        }

        public void Dispose()
        {
            _httpClient?.Dispose();

            GC.SuppressFinalize(this);
        }

        #endregion
    }
}