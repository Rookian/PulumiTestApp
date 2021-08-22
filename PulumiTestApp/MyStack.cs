using Pulumi;
using Pulumi.AzureNative.Resources;
using Pulumi.AzureNative.Sql;
using Pulumi.AzureNative.Web;
using Pulumi.AzureNative.Web.Inputs;
using ManagedServiceIdentityType = Pulumi.AzureNative.Web.ManagedServiceIdentityType;
// ReSharper disable ObjectCreationAsStatement

namespace PulumiTestApp
{
    public class MyStack : Stack
    {
        public MyStack(StackConfig config)
        {
            var resourceGroup = new ResourceGroup($"myapp-{config.Environment}", new ResourceGroupArgs
            {
                Location = config.Location,
                ResourceGroupName = $"myapp-{config.Environment}"
            });

            var plan = new AppServicePlan("pulitestserviceplan", new AppServicePlanArgs
            {
                Location = config.Location,
                ResourceGroupName = resourceGroup.Name,
                Kind = "Linux",
                Reserved = true,
                Sku = new SkuDescriptionArgs { Size = "B1", Tier = "Standard", Name = "B1" }
            });

            new WebApp("myApp", new WebAppArgs
            {
                Location = config.Location,
                ResourceGroupName = resourceGroup.Name,
                Identity = new ManagedServiceIdentityArgs { Type = ManagedServiceIdentityType.SystemAssigned },
                ServerFarmId = plan.Id
            });

            var sqlServer = new Server("myServer", new ServerArgs
            {
                AdministratorLogin = "myloginforsql",
                AdministratorLoginPassword = "passWord123@123",
                ResourceGroupName = resourceGroup.Name,
            });

            new Database("myDb", new DatabaseArgs
            {
                ResourceGroupName = resourceGroup.Name,
                ServerName = sqlServer.Name,
                Sku = new Pulumi.AzureNative.Sql.Inputs.SkuArgs { Family = "Gen5", Name = "Basic", Tier = "Basic" }
            });

        }
    }
}