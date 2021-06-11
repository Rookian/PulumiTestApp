﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Pulumi.Automation;
using Pulumi.Azure.AppService;
using Pulumi.Azure.AppService.Inputs;
using Pulumi.Azure.Core;
using Environment = System.Environment;

namespace PulumiTestApp
{
    class Program
    {
        private const string Location = "West Europe";
        private const string SubscriptionId = "cfed8a6e-91e3-4ba3-b79d-698c8b7b4e29";
        private static readonly Guid TenantId = Guid.Parse("17e6b881-0146-48e2-8241-7b564e5e94cb");

        static async Task Main(string[] args)
        {
            var configuration = new PulumiResourceConfiguration(
                SubscriptionId, Location, TenantId, "myappvaulta1b2",
                "myapp-infra", "pulumistate", "myapp1pulumistate", "pulumi");

            var prepareResult = await PulumiPreProvisioning.PreparePulumi(configuration);

            Environment.SetEnvironmentVariable("AZURE_KEYVAULT_AUTH_VIA_CLI", true.ToString());
            Environment.SetEnvironmentVariable("AZURE_STORAGE_ACCOUNT", configuration.AccountName);
            Environment.SetEnvironmentVariable("AZURE_STORAGE_KEY", prepareResult.StorageKey);

            var program = PulumiFn.Create(() =>
            {
                var resourceGroup = new ResourceGroup("myapp", new ResourceGroupArgs
                {
                    Location = Location,
                });

                var plan = new Plan("pulitestserviceplan", new PlanArgs
                {
                    Location = Location,
                    ResourceGroupName = resourceGroup.Name,
                    Sku = new PlanSkuArgs { Tier = "Standard", Size = "S1" }
                });

                var appService = new AppService("pulitestservice", new AppServiceArgs
                {
                    AppServicePlanId = plan.Id,
                    Location = Location,
                    ResourceGroupName = resourceGroup.Name,
                });

            });

            var projectName = "pulumi-test-project";
            var stackName = "dev";

            var secretsProvider = $"azurekeyvault://{configuration.VaultName}.vault.azure.net/keys/{configuration.KeyName}";
            var stackArgs = new InlineProgramArgs(projectName, stackName, program)
            {
                StackSettings = new Dictionary<string, StackSettings>
                {
                    [stackName] = new() { SecretsProvider = secretsProvider, }
                },
                SecretsProvider = secretsProvider,
                ProjectSettings = new ProjectSettings(projectName, ProjectRuntimeName.Dotnet)
                {
                    Backend = new ProjectBackend { Url = $"azblob://{configuration.ContainerName}" },
                }
            };

            var stack = await LocalWorkspace.CreateOrSelectStackAsync(stackArgs);
            await stack.Workspace.InstallPluginAsync("azure", "v4.6.0");

            await stack.RefreshAsync(new RefreshOptions { OnStandardOutput = Console.WriteLine });
            Console.WriteLine("successfully initialized stack");

            var result = await stack.UpAsync(new UpOptions { OnStandardOutput = Console.WriteLine });

            if (result.Summary.ResourceChanges != null)
            {
                Console.WriteLine("update summary:");
                foreach (var change in result.Summary.ResourceChanges)
                    Console.WriteLine($"    {change.Key}: {change.Value}");
            }
        }
    }
}

