using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;

namespace WebApiApp.Controllers;

[ApiController]
[Route("/")]
public class HomeController : ControllerBase
{    /// <summary>
    /// Returns the APP_VALUE environment variable as JSON
    /// </summary>
    /// <returns>JSON object containing the APP_VALUE</returns>
    [HttpGet]
    public IActionResult Get()
    {
        var response = new Dictionary<string, string>
        {
            { "APP_VALUE", AppConfiguration.AppValue }
        };
        return Ok(response);
    }

    /// <summary>
    /// Waits 10 seconds and returns a 200 OK response
    /// </summary>
    /// <returns>200 OK response after 10 second delay</returns>
    [HttpGet("slowapi")]
    public async Task<IActionResult> SlowApi()
    {
        await Task.Delay(10000); // Wait 10 seconds
        return Ok(new { message = "OK" });
    }
}
