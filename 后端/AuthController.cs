using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace CalculatorBackend
{
    [ApiController]
    [Route("[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ILogger<AuthController> _logger; // 添加日志字段
        private static Dictionary<string, string> _users = new()
        {
            { "admin", "admin123" },
            { "user", "pass123" }
        };

        public AuthController(ILogger<AuthController> logger)
        {
            _logger = logger; // 通过构造函数注入
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            _logger.LogInformation("收到登录请求：Username={Username}", request.Username); // 记录请求
            if (_users.TryGetValue(request.Username, out var pwd) && pwd == request.Password)
            {
                _logger.LogInformation("用户 {Username} 登录成功", request.Username);
                return Ok(new { Token = "dummy-token" });
            }
            _logger.LogWarning("用户 {Username} 登录失败：密码错误", request.Username);
            return Unauthorized();
        }
    }
}
