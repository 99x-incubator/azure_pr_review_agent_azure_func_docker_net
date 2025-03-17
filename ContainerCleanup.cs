using System;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.ContainerInstance;
using Azure.ResourceManager.Resources;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace PRReviewAgent
{
    public class ContainerCleanupFunction
    {
        private readonly ILogger<ContainerCleanupFunction> _logger;

        public ContainerCleanupFunction(ILogger<ContainerCleanupFunction> logger)
        {
            _logger = logger;
        }

        [Function("ContainerCleanup")]
        public async Task Run([TimerTrigger("0 0 */6 * * *")] TimerInfo myTimer)
        {
            _logger.LogInformation("Starting container cleanup with Managed Identity...");

            try
            {
                var subscriptionId = Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID");
                var resourceGroupName = Environment.GetEnvironmentVariable("RESOURCE_GROUP");

                if (string.IsNullOrEmpty(subscriptionId) || string.IsNullOrEmpty(resourceGroupName))
                {
                    _logger.LogError("Missing required environment variables");
                    return;
                }

                var armClient = new ArmClient(new DefaultAzureCredential());
                var subscription = await armClient.GetDefaultSubscriptionAsync();
                var resourceGroup = await subscription.GetResourceGroups().GetAsync(resourceGroupName);

                await foreach (var containerGroup in resourceGroup.Value.GetContainerGroups().GetAllAsync())
                {
                    try
                    {
                        if (!containerGroup.Data.Name.StartsWith("pr-agent-"))
                            continue;

                        _logger.LogInformation($"Processing container group: {containerGroup.Data.Name}");

                        var shouldDelete = true;
                        var groupDetails = await containerGroup.GetAsync();

                        foreach (var container in groupDetails.Value.Data.Containers)
                        {
                            var instanceView = container.InstanceView;
                            if (instanceView == null)
                            {
                                shouldDelete = false;
                                break;
                            }

                            var currentState = instanceView.CurrentState?.State?.ToLower();
                            if (currentState == null || 
                                !(currentState.Contains("terminated") || 
                                  currentState.Contains("succeeded") || 
                                  currentState.Contains("failed")))
                            {
                                shouldDelete = false;
                                break;
                            }
                        }

                        if (shouldDelete)
                        {
                            _logger.LogInformation($"Deleting container group: {containerGroup.Data.Name}");
                            await containerGroup.DeleteAsync(Azure.WaitUntil.Completed);
                            _logger.LogInformation($"Successfully deleted {containerGroup.Data.Name}");
                        }
                        else
                        {
                            _logger.LogInformation($"Skipping active container group: {containerGroup.Data.Name}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error processing {containerGroup.Data.Name}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Cleanup failed: {ex.Message}");
            }
        }
    }
}