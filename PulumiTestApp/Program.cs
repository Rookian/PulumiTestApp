using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Pulumi.Automation;
using Environment = System.Environment;

namespace PulumiTestApp
{
    class Program
    {
        private const string Location = "West Europe";
        private const string SubscriptionId = "cfed8a6e-91e3-4ba3-b79d-698c8b7b4e29";
        private static readonly Guid TenantId = Guid.Parse("17e6b881-0146-48e2-8241-7b564e5e94cb");
        private const string ProjectName = "pulumi-test-project";

        static async Task Main()
        {
            var serviceProvider = new ServiceCollection();

            var stackConfig = new StackConfig
            {
                Location = Location,
                Environment = "dev"
            };

            serviceProvider.AddSingleton(stackConfig);
            serviceProvider.AddSingleton<MyStack>();


            var pulumiBackendConfig = new PulumiBackendConfiguration
            {
                SubscriptionId = SubscriptionId,
                Location = Location,
                TenantId = TenantId,
                VaultName = $"myappvaulta1b2-{stackConfig.Environment}",
                ResourceGroupName = $"myapp-infra-{stackConfig.Environment}",
                AccountName = $"myapp1pulumistate{stackConfig.Environment}",
                ContainerName = "pulumistate",
                KeyName = "pulumi"
            };

            var prepareResult = await PulumiBackendProvisioning.Run(pulumiBackendConfig);

            serviceProvider.AddSingleton<ICryptoService, CryptoService>(_ => new CryptoService(prepareResult.KeyVaultKey.Id));

            Environment.SetEnvironmentVariable("AZURE_KEYVAULT_AUTH_VIA_CLI", true.ToString());
            Environment.SetEnvironmentVariable("AZURE_STORAGE_ACCOUNT", pulumiBackendConfig.AccountName);
            Environment.SetEnvironmentVariable("AZURE_STORAGE_KEY", prepareResult.StorageKey);

            var provider = serviceProvider.BuildServiceProvider();
            var program = PulumiFn.Create<MyStack>(provider);

            var secretsProvider = $"azurekeyvault://{pulumiBackendConfig.VaultName}.vault.azure.net/keys/{pulumiBackendConfig.KeyName}";
            var stackArgs = new InlineProgramArgs(ProjectName, stackConfig.Environment, program)
            {
                StackSettings = new Dictionary<string, StackSettings>
                {
                    [stackConfig.Environment] = new() { SecretsProvider = secretsProvider, }
                },
                SecretsProvider = secretsProvider,
                ProjectSettings = new ProjectSettings(ProjectName, ProjectRuntimeName.Dotnet)
                {
                    Backend = new ProjectBackend { Url = $"azblob://{pulumiBackendConfig.ContainerName}" },
                }
            };

            var stack = await LocalWorkspace.CreateOrSelectStackAsync(stackArgs);
            Console.WriteLine("successfully initialized stack");

            Console.WriteLine("installing plugins...");
            await stack.Workspace.InstallPluginAsync("azure", "v4.19.0");
            await stack.Workspace.InstallPluginAsync("azure-native", "v1.31.0");
            Console.WriteLine("plugins installed");

            Console.WriteLine("refreshing stack...");
            await stack.RefreshAsync(new RefreshOptions { OnStandardOutput = Console.WriteLine });
            Console.WriteLine("refresh complete");

            Console.WriteLine("updating stack...");

            var result = await stack.UpAsync(new UpOptions { OnStandardOutput = Console.WriteLine });
            var sqlConnectionString = result.Outputs[nameof(MyStack.SqlConnectionString)];
            
            if (result.Summary.ResourceChanges != null)
            {
                Console.WriteLine("update summary:");
                foreach (var change in result.Summary.ResourceChanges)
                    Console.WriteLine($"    {change.Key}: {change.Value}");
            }
        }
    }

    public class StackConfig
    {
        public string Location { get; set; }
        public string Environment { get; set; }
    }
}

