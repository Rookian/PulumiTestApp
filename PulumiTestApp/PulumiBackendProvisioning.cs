using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.ResourceManager.KeyVault.Models;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Storage;
using Azure.ResourceManager.Storage.Models;
using Azure.Security.KeyVault.Keys;
using Microsoft.Graph;
using KeyType = Azure.Security.KeyVault.Keys.KeyType;
using Permissions = Azure.ResourceManager.KeyVault.Models.Permissions;
using Sku = Azure.ResourceManager.KeyVault.Models.Sku;
using SkuName = Azure.ResourceManager.KeyVault.Models.SkuName;

namespace PulumiTestApp
{
    public class PulumiBackendProvisioning
    {
        public static async Task<Result> Run(PulumiBackendConfiguration configuration)
        {
            var graphClient = new GraphServiceClient(new AzureCliCredential());
            var currentUser = await graphClient.Me.Request().GetAsync();

            var managementClient = new ResourcesManagementClient(configuration.SubscriptionId, new AzureCliCredential());
            await managementClient.ResourceGroups.CreateOrUpdateAsync(
                configuration.ResourceGroupName,
                new Azure.ResourceManager.Resources.Models.ResourceGroup(configuration.Location));

            var keyVaultManagementClient =
                new Azure.ResourceManager.KeyVault.KeyVaultManagementClient(configuration.SubscriptionId, new AzureCliCredential());

            var vault = await keyVaultManagementClient.Vaults.StartCreateOrUpdateAsync(
                configuration.ResourceGroupName, configuration.VaultName,
                new VaultCreateOrUpdateParameters(configuration.Location, new VaultProperties(configuration.TenantId, new Sku(SkuName.Standard))
                {
                    AccessPolicies = new List<AccessPolicyEntry>
                    {
                        new(configuration.TenantId, currentUser.Id, new Permissions
                        {
                            Keys = new List<KeyPermissions>
                            {
                                KeyPermissions.List, KeyPermissions.Decrypt, KeyPermissions.Encrypt, KeyPermissions.Get, KeyPermissions.Create
                            }
                        })
                    }
                }));

            var vaultResponse = await vault.WaitForCompletionAsync();

            await CreateKeyIfNotExist(configuration.KeyName, vaultResponse.Value.Properties.VaultUri);

            var storageManagementClient = new StorageManagementClient(configuration.SubscriptionId, new AzureCliCredential());
            var storageOperation = await storageManagementClient.StorageAccounts.StartCreateAsync(configuration.ResourceGroupName, configuration.AccountName,
                new StorageAccountCreateParameters(
                    new Azure.ResourceManager.Storage.Models.Sku(Azure.ResourceManager.Storage.Models.SkuName.StandardLRS),
                    Kind.StorageV2, configuration.Location));

            await storageOperation.WaitForCompletionAsync();
            await storageManagementClient.BlobContainers.CreateAsync(configuration.ResourceGroupName, configuration.AccountName, configuration.ContainerName,
                new BlobContainer
                {
                    PublicAccess = PublicAccess.None
                });


            var keys = await storageManagementClient.StorageAccounts.ListKeysAsync(configuration.ResourceGroupName, configuration.AccountName);
            return new Result(keys.Value.Keys.First().Value);
        }

        private static async Task CreateKeyIfNotExist(string keyName, string vaultUri)
        {
            var keyClient = new KeyClient(new Uri(vaultUri), new AzureCliCredential());

            try
            {
                await keyClient.GetKeyAsync(keyName);
            }
            catch (Exception)
            {
                await keyClient.CreateKeyAsync(keyName, KeyType.Rsa);
            }
        }
    }

    public record PulumiBackendConfiguration
    {
        public string SubscriptionId { get; init; }
        public string Location { get; init; }
        public Guid TenantId { get; init; }
        public string VaultName { get; init; }
        public string ResourceGroupName { get; init; }
        public string ContainerName { get; init; }
        public string AccountName { get; init; }
        public string KeyName { get; init; }
    }

    public record Result(
        string StorageKey
    );
}