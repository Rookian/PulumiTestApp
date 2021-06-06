using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.ResourceManager.KeyVault.Models;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Storage;
using Azure.ResourceManager.Storage.Models;
using Microsoft.Graph;
using Pulumi.Automation;
using Pulumi.Azure.AppService;
using Pulumi.Azure.AppService.Inputs;
using Pulumi.Azure.Core;
using Environment = System.Environment;
using Permissions = Azure.ResourceManager.KeyVault.Models.Permissions;
using Sku = Azure.ResourceManager.KeyVault.Models.Sku;
using SkuName = Azure.ResourceManager.KeyVault.Models.SkuName;

namespace PulumiTestApp
{
    class Program
    {
        private const string Location = "West Europe";
        private const string SubscriptionId = "cfed8a6e-91e3-4ba3-b79d-698c8b7b4e29";
        private static readonly Guid TenantId = Guid.Parse("17e6b881-0146-48e2-8241-7b564e5e94cb");

        static async Task Main(string[] args)
        {
            var graphClient = new GraphServiceClient(new AzureCliCredential());
            var currentUser = await graphClient.Me.Request().GetAsync();

            var managementClient = new ResourcesManagementClient(SubscriptionId, new AzureCliCredential());
            var infraResourceGroup = "myapp-infra";
            await managementClient.ResourceGroups.CreateOrUpdateAsync(
                infraResourceGroup,
                new Azure.ResourceManager.Resources.Models.ResourceGroup(Location));

            var keyVaultManagementClient = new Azure.ResourceManager.KeyVault.KeyVaultManagementClient(SubscriptionId, new AzureCliCredential());
            var vault = await keyVaultManagementClient.Vaults.StartCreateOrUpdateAsync(
                infraResourceGroup, "vault", new VaultCreateOrUpdateParameters(Location, new VaultProperties(TenantId, new Sku(SkuName.Standard))));

            await keyVaultManagementClient.Vaults.UpdateAccessPolicyAsync(infraResourceGroup, "vault",
                AccessPolicyUpdateKind.Add, new VaultAccessPolicyParameters(new VaultAccessPolicyProperties(new[]
                {
                    new AccessPolicyEntry(TenantId, currentUser.Id, new Permissions
                    {
                        Keys = new List<KeyPermissions>
                        {
                            KeyPermissions.List, KeyPermissions.Decrypt, KeyPermissions.Encrypt, KeyPermissions.Get
                        }
                    })
                })));

            var storageManagementClient = new StorageManagementClient(SubscriptionId, new AzureCliCredential());
            var accountName = "pulumi-state";
            var storage = await storageManagementClient.StorageAccounts.StartCreateAsync(SubscriptionId, accountName,
                new StorageAccountCreateParameters(
                    new Azure.ResourceManager.Storage.Models.Sku(Azure.ResourceManager.Storage.Models.SkuName.StandardLRS),
                    Kind.StorageV2, Location));

            var response = await storageManagementClient.BlobContainers.GetAsync(infraResourceGroup, accountName, "pulumistate");

            await storageManagementClient.BlobContainers.CreateAsync(infraResourceGroup, accountName, "pulumistate",
                new BlobContainer
                {
                    PublicAccess = PublicAccess.None
                });



            Environment.SetEnvironmentVariable("AZURE_KEYVAULT_AUTH_VIA_CLI", true.ToString());
            Environment.SetEnvironmentVariable("AZURE_STORAGE_ACCOUNT", "pulustate");


            Environment.SetEnvironmentVariable("AZURE_STORAGE_KEY", "Y/opC/OygdBNze5TLQHMqiwBLYqPzfXEz59th2cGhIdeBRdBpNmJaI30X5dV42/8yQIR5IRvaLV8UHUJUspaLA==");

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
                    Sku = new PlanSkuArgs
                    {
                        Tier = "Standard",
                        Size = "S1",
                    }
                });

                new AppService("pulitestservice", new AppServiceArgs
                {
                    AppServicePlanId = plan.Id,
                    Location = Location,
                    ResourceGroupName = resourceGroup.Name,
                });
            });

            var projectName = "pulumi-test-project";
            var stackName = "dev";

            var secretsProvider = "azurekeyvault://pulumi-akv.vault.azure.net/keys/master-key";
            var stackArgs = new InlineProgramArgs(projectName, stackName, program)
            {
                StackSettings = new Dictionary<string, StackSettings>
                {
                    [stackName] = new StackSettings
                    {
                        SecretsProvider = secretsProvider,
                    }
                },
                SecretsProvider = secretsProvider,
                ProjectSettings = new ProjectSettings(projectName, ProjectRuntimeName.Dotnet)
                {
                    Backend = new ProjectBackend { Url = "azblob://state-container" },
                }
            };


            var stack = await LocalWorkspace.CreateOrSelectStackAsync(stackArgs);

            Console.WriteLine("successfully initialized stack");
        }
    }
}

