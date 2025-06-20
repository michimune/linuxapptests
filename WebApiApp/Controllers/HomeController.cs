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
            { "AppValue", AppConfiguration.AppValue }
        };
        return Ok(response);
    }
}
