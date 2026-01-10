using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace PlayfabFunctions;

public class EndRun_FarmingDungeon
{
    private readonly ILogger<EndRun_FarmingDungeon> _logger;

    public EndRun_FarmingDungeon(ILogger<EndRun_FarmingDungeon> logger)
    {
        _logger = logger;
    }

    [Function("EndRun_FarmingDungeon")]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");
        return new OkObjectResult("Welcome to Azure Functions!");
    }
}
