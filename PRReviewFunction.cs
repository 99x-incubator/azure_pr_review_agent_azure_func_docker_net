using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
//using Microsoft.Azure.Management.ContainerInstance;
//using Microsoft.Azure.Management.ContainerInstance.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.ResourceManager.ContainerInstance;
using Azure.ResourceManager.ContainerInstance.Models;
using Azure.ResourceManager;
using Azure;

namespace PRReviewAgent
{
    public class PRReviewFunction
    {
        private readonly ILogger<PRReviewFunction> _logger;

        public PRReviewFunction(ILogger<PRReviewFunction> logger)
        {
            _logger = logger;
        }

        [Function("PRReviewTrigger")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processing request.");

            try
            {
                // Parse incoming request
                var payload = await JsonSerializer.DeserializeAsync<PullRequestPayload>(req.Body);
                var prResource = payload?.Resource ?? new Resource();

                // Extract PR details
                var prTitle = prResource.Title ?? string.Empty;
                var prId = prResource.PullRequestId.ToString();
                var repository = prResource.Repository ?? new Repository();
                var repoName = repository.Name ?? string.Empty;
                var repoUrl = repository.RemoteUrl ?? string.Empty;

                _logger.LogInformation($"Processing PR #{prId}: {prTitle}");
                _logger.LogInformation($"Repository: {repoName} ({repoUrl})");

                // Parse organization and project from URL
                var (org, project) = ParseRepoUrl(repoUrl);
                _logger.LogInformation($"Parsed organization: {org}, project: {project}");

                // Validate environment variables
                var requiredEnvVars = new List<string>
                {
                    "AZURE_PAT", "AZURE_SUBSCRIPTION_ID",
                    "ACR_NAME", "ACR_USERNAME", "ACR_PASSWORD",
                    "RESOURCE_GROUP", "AZURE_OPENAI_API_KEY"
                };

                var missingVars = new List<string>();
                foreach (var varName in requiredEnvVars)
                {
                    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(varName)))
                        missingVars.Add(varName);
                }

                if (missingVars.Count > 0)
                {
                    _logger.LogError($"Missing environment variables: {string.Join(", ", missingVars)}");
                    return new BadRequestObjectResult($"Missing required environment variables: {string.Join(", ", missingVars)}");
                }              

                // Initialize Azure resources client
                var credential = new DefaultAzureCredential();
                var armClient = new ArmClient(credential);
                var subscription = await armClient.GetDefaultSubscriptionAsync();
                
                var resourceGroupName = Environment.GetEnvironmentVariable("RESOURCE_GROUP");
                var acrName = Environment.GetEnvironmentVariable("ACR_NAME");
                var location = AzureLocation.EastUS;

                // Get resource group
                var resourceGroup = await subscription.GetResourceGroups().GetAsync(resourceGroupName);

                // Create container group collection
                var containerGroupCollection = resourceGroup.Value.GetContainerGroups();

                // Create container definition
                var container = new ContainerInstanceContainer(
                    name: "pr-agent-container",
                    image: $"{acrName}.azurecr.io/azure-pr-agent",
                    resources: new ContainerResourceRequirements(
                        new ContainerResourceRequestsContent(
                            cpu: 1.0,
                            memoryInGB: 4.0
                        )
                    )
                )
                {
                    EnvironmentVariables =
                    {
                        new ContainerEnvironmentVariable("AZURE_PAT") {Value = Environment.GetEnvironmentVariable("AZURE_PAT") },
                        new ContainerEnvironmentVariable("AZURE_ORG") {Value = org },
                        new ContainerEnvironmentVariable("AZURE_PROJECT") {Value = project },
                        new ContainerEnvironmentVariable("AZURE_REPO") {Value = repoName },
                        new ContainerEnvironmentVariable("AZURE_PR_ID") {Value = prId },
                        new ContainerEnvironmentVariable("AZURE_OPENAI_API_KEY") {Value = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY") },
                        new ContainerEnvironmentVariable("AZURE_OPENAI_API_INSTANCE_NAME") {Value = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_INSTANCE_NAME") },
                        new ContainerEnvironmentVariable("AZURE_OPENAI_API_DEPLOYMENT_NAME") {Value = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_DEPLOYMENT_NAME") },
                        new ContainerEnvironmentVariable("AZURE_OPENAI_API_VERSION") {Value = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_VERSION") },
                        new ContainerEnvironmentVariable("INSTRUCTION_SOURCE") {Value = Environment.GetEnvironmentVariable("INSTRUCTION_SOURCE") },
                        new ContainerEnvironmentVariable("CREATE_NEW_PR") {Value = Environment.GetEnvironmentVariable("CREATE_NEW_PR") ?? "false" }
                    }
                };

                // Create container group data
                var containerGroupData = new ContainerGroupData(
                    location: AzureLocation.EastUS,
                    containers: new[] { container },
                    osType: ContainerInstanceOperatingSystemType.Linux
                )
                {
                    RestartPolicy = ContainerGroupRestartPolicy.Never,
                    ImageRegistryCredentials =
                    {
                        new ContainerGroupImageRegistryCredential(
                            server: $"{acrName}.azurecr.io")
                        {
                            Username = Environment.GetEnvironmentVariable("ACR_USERNAME"),
                            Password = Environment.GetEnvironmentVariable("ACR_PASSWORD")
                        }
                    }
                };

                // Deploy container instance
                var containerGroupName = $"pr-agent-{prId}";
                _logger.LogInformation($"Creating container group: {containerGroupName}");
                
                //var resourceGroup = await armClient.GetDefaultSubscription().GetResourceGroups().GetAsync(resourceGroupName);
                var containerGroups = resourceGroup.Value.GetContainerGroups();

                var operation = await containerGroups.CreateOrUpdateAsync(
                    Azure.WaitUntil.Completed,
                    containerGroupName,
                    containerGroupData
                );

                return new AcceptedResult($"Container instance started for PR #{prId}", null);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing request: {ex.Message}", ex);
                return new BadRequestObjectResult($"Error occurred: {ex.Message}");
            }
        }

        private (string org, string project) ParseRepoUrl(string repoUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(repoUrl))
                    return ("", "");

                if (repoUrl.Contains("dev.azure.com"))
                {
                    var parts = repoUrl.Split('/');
                    if (parts.Length >= 6)
                    {
                        var org = Uri.UnescapeDataString(parts[3]);
                        var project = Uri.UnescapeDataString(parts[4]);
                        return (org, project);
                    }
                }
                else
                {
                    var domainPart = repoUrl.Split('/')[2];
                    var org = domainPart.Split('.')[0];
                    var pathParts = repoUrl.Split('/')[3..];
                    if (pathParts.Length > 0)
                    {
                        var project = pathParts[0];
                        return (org, project);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error parsing repo URL: {ex.Message}");
            }

            return ("", "");
        }
    }

    // Model classes for JSON deserialization
    public class PullRequestPayload
    {
        [JsonPropertyName("resource")]
        public Resource Resource { get; set; }
    }

    public class Resource
    {
        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("pullRequestId")]
        public int PullRequestId { get; set; }

        [JsonPropertyName("repository")]
        public Repository Repository { get; set; }
    }

    public class Repository
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("remoteUrl")]
        public string RemoteUrl { get; set; }
    }
}