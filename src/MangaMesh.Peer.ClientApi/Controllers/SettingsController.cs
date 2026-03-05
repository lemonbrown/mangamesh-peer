using MangaMesh.Peer.Core.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MangaMesh.Peer.ClientApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SettingsController : ControllerBase
    {
        private readonly ReplicationOptions _replicationOptions;
        private readonly IWebHostEnvironment _env;

        public SettingsController(IOptionsMonitor<ReplicationOptions> replicationOptions, IWebHostEnvironment env)
        {
            _replicationOptions = replicationOptions.CurrentValue;
            _env = env;
        }

        [HttpGet]
        public IActionResult GetSettings()
        {
            return Ok(new
            {
                IsFullSeeder = _replicationOptions.IsFullSeeder
            });
        }

        [HttpPut]
        public async Task<IActionResult> UpdateSettings([FromBody] UpdateSettingsRequest request)
        {
            try
            {
                var appSettingsPath = Path.Combine(_env.ContentRootPath, "appsettings.json");
                
                JsonObject? jsonNode = null;
                if (System.IO.File.Exists(appSettingsPath))
                {
                    var jsonStr = await System.IO.File.ReadAllTextAsync(appSettingsPath);
                    jsonNode = JsonSerializer.Deserialize<JsonObject>(jsonStr);
                }
                jsonNode ??= new JsonObject();

                if (!jsonNode.ContainsKey("Replication"))
                    jsonNode["Replication"] = new JsonObject();

                var replicationObj = jsonNode["Replication"] as JsonObject;
                if (replicationObj == null)
                {
                    replicationObj = new JsonObject();
                    jsonNode["Replication"] = replicationObj;
                }

                replicationObj["IsFullSeeder"] = request.IsFullSeeder;

                var options = new JsonSerializerOptions { WriteIndented = true };
                await System.IO.File.WriteAllTextAsync(appSettingsPath, jsonNode.ToJsonString(options));

                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Failed to update settings: {ex.Message}");
            }
        }
    }

    public class UpdateSettingsRequest
    {
        public bool IsFullSeeder { get; set; }
    }
}
