using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OCC.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ConfigController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public ConfigController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet("google-maps-key")]
        public IActionResult GetGoogleMapsKey()
        {
            var key = _configuration["GoogleMaps:ApiKey"];
            if (string.IsNullOrEmpty(key))
            {
                return NotFound("Google Maps API Key not configured on server.");
            }
            return Ok(new { key });
        }
    }
}
