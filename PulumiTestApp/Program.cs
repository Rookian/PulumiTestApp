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
        
        static async Task Main(string[] args)
        {
            var serviceProvider = new ServiceCollection();

            var myConfig = new MyConfig { Location = Location };

            serviceProvider.AddSingleton(myConfig);
            serviceProvider.AddSingleton<MyStack>();

            var stackName = "dev";

            var configuration = new PulumiBackendConfiguration
            {
                SubscriptionId = SubscriptionId,
                Location = Location,
                TenantId = TenantId,
                VaultName = $"myappvaulta1b2-{stackName}",
                ResourceGroupName = $"myapp-infra-{stackName}",
                AccountName = $"myapp1pulumistate{stackName}",
                ContainerName = "pulumistate",
                KeyName = "pulumi"
            };

            var prepareResult = await PulumiPreProvisioning.PreparePulumi(configuration);

            Environment.SetEnvironmentVariable("AZURE_KEYVAULT_AUTH_VIA_CLI", true.ToString());
            Environment.SetEnvironmentVariable("AZURE_STORAGE_ACCOUNT", configuration.AccountName);
            Environment.SetEnvironmentVariable("AZURE_STORAGE_KEY", prepareResult.StorageKey);

            var provider = serviceProvider.BuildServiceProvider();
            var program = PulumiFn.Create<MyStack>(provider);

            var secretsProvider = $"azurekeyvault://{configuration.VaultName}.vault.azure.net/keys/{configuration.KeyName}";
            var stackArgs = new InlineProgramArgs(ProjectName, stackName, program)
            {
                StackSettings = new Dictionary<string, StackSettings>
                {
                    [stackName] = new() { SecretsProvider = secretsProvider, }
                },
                SecretsProvider = secretsProvider,
                ProjectSettings = new ProjectSettings(ProjectName, ProjectRuntimeName.Dotnet)
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

    public class MyConfig
    {
        public string Location { get; set; }
    }
}

