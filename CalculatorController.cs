using Microsoft.AspNetCore.Mvc;
using NCalc; // 必须引用NCalc包
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace CalculatorBackend
{
    [ApiController]
    [Route("[controller]")]
    public class CalculatorController : ControllerBase
    {
        private readonly ILogger<CalculatorController> _logger;

        public CalculatorController(ILogger<CalculatorController> logger)
        {
            _logger = logger;
        }
        [HttpPost("calculate")]

        public IActionResult Calculate([FromBody] CalculateRequest request)
        {
            _logger.LogInformation("收到计算请求：ID={Id}, Expression={Expression}", request.Id, request.Expression);
            try
            {
                var result = SafeEvaluate(request.Expression);
                _logger.LogInformation("计算成功：ID={Id}, 表达式={Expression}, 结果={Result}", request.Id, request.Expression, result);
                return Ok(new { Id = request.Id, Result = result }); // 返回Id和结果
            }
            catch (ValidationException ex)
            {
                _logger.LogWarning(ex, "输入验证失败：ID={Id}, 表达式={Expression}", request.Id, request.Expression);
                return BadRequest(new
                {
                    errorType = "VALIDATION_ERROR",
                    message = ex.Message,
                    details = ex.Details,
                    id = request.Id  // 错误响应包含Id
                });
            }
            catch (MathOperationException ex)
            {
                _logger.LogWarning(ex, "数学运算错误：ID={Id}, 表达式={Expression}", request.Id, request.Expression);
                return BadRequest(new
                {
                    errorType = "MATH_ERROR",
                    message = ex.Message,
                    affectedFunction = ex.FunctionName,
                    id = request.Id
                });
            }
            catch (EvaluationException ex)
            {
                _logger.LogWarning(ex, "表达式解析错误：ID={Id}, 表达式={Expression}", request.Id, request.Expression);
                return BadRequest(new
                {
                    errorType = "SYNTAX_ERROR",
                    message = "表达式语法错误",
                    details = ex.Message,
                    id = request.Id
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "服务器内部错误：ID={Id}, 表达式={Expression}", request.Id, request.Expression);
                return StatusCode(500, new
                {
                    errorType = "INTERNAL_ERROR",
                    message = "服务器内部错误",
                    id = request.Id
                });
            }
        }

        public class ValidationException : Exception
        {
            public object Details { get; }
            public ValidationException(string message, object details = null) : base(message)
            {
                Details = details;
            }
        }

        public class MathOperationException : Exception
        {
            public string FunctionName { get; }
            public MathOperationException(string message, string functionName) : base(message)
            {
                FunctionName = functionName;
            }
        }

        private double SafeEvaluate(string expr)
        {
            // 输入字符白名单校验
            var allowedChars = new HashSet<char>("+-*/()^!.,0123456789sincostansqrtlogexplnpowepi");
            if (expr.ToLower().Any(c => !allowedChars.Contains(c)))
                throw new ValidationException("表达式中含有非法字符");

            // 预处理表达式
            expr = expr.ToLower().Replace("°", "").Replace(" ", "");

            // 使用NCalc解析表达式
            var expression = new Expression(expr);
            expression.EvaluateFunction += EvaluateCustomFunctions;
            expression.EvaluateParameter += EvaluateParameters; // 添加参数解析逻辑
            // 计算结果并返回
            return Math.Round(Convert.ToDouble(expression.Evaluate()), 8);
        }

        private void EvaluateParameters(string name, ParameterArgs args)
        {
            name = name.ToLower();
            switch (name)
            {
                case "e":
                    args.Result = Math.E;  // 自然对数的底数 ≈ 2.71828
                    break;
                case "pi":
                    args.Result = Math.PI; // 圆周率 ≈ 3.14159
                    break;
                default:
                    args.Result = null;    // 未知参数，按变量处理（此处不支持变量）
                    break;
            }
        }

        private void EvaluateCustomFunctions(string name, FunctionArgs args)
        {
            // 统一转换为小写函数名
            name = name.ToLower();

            // 验证参数数量
            switch (name)
            {
                case "sqrt":
                case "sin":
                case "cos":
                case "tan":
                case "ln":
                case "exp":
                    ValidateParameterCount(name, args, 1);
                    break;
                case "log":
                    ValidateParameterCount(name, args, 1, 2);
                    break;
                case "pow":
                    ValidateParameterCount(name, args, 2);
                    break;
                default:
                    throw new ArgumentException($"Error:不支持函数 {name}");
            }

            // 解析参数值
            var parameters = args.Parameters
                .Select(p => Convert.ToDouble(p.Evaluate()))
                .ToArray();

            // 执行具体计算
            switch (name)
            {
                // 三角函数（角度制）
                case "sin":
                    args.Result = Math.Sin(parameters[0] * Math.PI / 180);
                    break;
                case "cos":
                    args.Result = Math.Cos(parameters[0] * Math.PI / 180);
                    break;
                case "tan":
                    args.Result = Math.Tan(parameters[0] * Math.PI / 180);
                    break;

                // 开方和指数
                case "sqrt":
                    ValidateNonNegative(parameters[0], "sqrt");
                    args.Result = Math.Sqrt(parameters[0]);
                    break;
                case "exp":
                    args.Result = Math.Exp(parameters[0]); // e^x
                    break;
                case "pow":
                    ValidateParameterCount(name, args, 2); // 必须两个参数
                    double baseNum = Convert.ToDouble(args.Parameters[0].Evaluate());
                    double exponent = Convert.ToDouble(args.Parameters[1].Evaluate());
                    args.Result = Math.Pow(baseNum, exponent);
                    break;

                // 对数
                case "log":
                    if (parameters.Length == 1 && parameters[0] <= 0 || parameters.Length == 2 && (parameters[0] <= 0 || parameters[1] <= 0))
                        throw new MathOperationException("log 的参数必须为正数", "log");
                    if (parameters.Length == 1)
                        args.Result = Math.Log10(parameters[0]); // log10(x)
                    else
                        args.Result = Math.Log(parameters[0], parameters[1]); // log_base(x)
                    break;
                case "ln":
                    if (parameters[0] <= 0)
                        throw new MathOperationException("ln 的参数必须为正数", "ln");
                    args.Result = Math.Log(parameters[0]); // 自然对数
                    break;
                default:
                    throw new ValidationException($"不支持的函数 {name}", new { InvalidFunction = name });
            }
        }

        // 验证参数数量
        private void ValidateParameterCount(string funcName, FunctionArgs args, int min, int? max = null)
        {
            if (args.Parameters.Length < min || (max != null && args.Parameters.Length > max))
            {
                throw new MathOperationException(
                    $"函数 {funcName} 需要 {min} 个参数",
                    funcName
                );
            }
        }

        // 验证非负数
        private void ValidateNonNegative(double value, string funcName)
        {
            if (value < 0)
            {
                throw new MathOperationException(
                    $"函数 {funcName} 的参数不能为负数",
                    funcName
                );
            }
        }
    }



}