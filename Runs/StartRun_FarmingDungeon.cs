using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AfterHuman.Games.Function;

public class StartRun_FarmingDungeon
{
    private readonly ILogger<StartRun_FarmingDungeon> _logger;

    public StartRun_FarmingDungeon(ILogger<StartRun_FarmingDungeon> logger)
    {
        _logger = logger;
    }

    [Function("StartRun_FarmingDungeon")]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");
        return new OkObjectResult("Welcome to Azure Functions!");
    }
}