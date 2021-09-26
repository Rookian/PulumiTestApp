using Pulumi;
using Pulumi.AzureNative.Resources;
using Pulumi.AzureNative.Sql;
using Pulumi.AzureNative.Web;
using Pulumi.AzureNative.Web.Inputs;
using ManagedServiceIdentityType = Pulumi.AzureNative.Web.ManagedServiceIdentityType;
using Pulumi.AzureNative.Sql.Inputs;
using Output = Pulumi.Output;

// ReSharper disable ObjectCreationAsStatement

namespace PulumiTestApp
{
    public class MyStack : Stack
    {
        [Output]
        public Output<string> SqlConnectionString { get; set; }

        public MyStack(StackConfig config, ICryptoService cryptoService)
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

            const string administratorLogin = "myloginforsql";
            var administratorLoginPassword = Output.Create(cryptoService.Decrypt("VrKPB8XGAwQnF/qgf22370+I0OILIo3DvqSKc4voVVjyvutPIYCvRN6TS/fQZBjtv0UnRw6tD9xGYy/+WA6AMG90R64tfNhVjKMlgoYcKPcz1Bx09YMdu2584wU9Qz6vvxwsVzZthio4mMNd90XqhttsWLkUtEZBGbJGRRzykcTACGpiq6+A1hoOvNPMXOTfJUKCZuEbjD3tE5b+bwtjqKae97OmW66L5fKUttnR3tS9jMhU3KeWPve4mnP8zQnAs0AIjuSoycPOb+ZO09vkR+glhgWyhwYm3lqBxC+75ZlpRYnD17dSCN88aNOdncZJgTAhzKV5AbpSsEjTOT1hgw=="));

            var sqlServer = new Server("myServer", new ServerArgs
            {
                AdministratorLogin = administratorLogin,
                AdministratorLoginPassword = administratorLoginPassword,
                ResourceGroupName = resourceGroup.Name,
            });

            var database = new Database("myDb", new DatabaseArgs
            {
                ResourceGroupName = resourceGroup.Name,
                ServerName = sqlServer.Name,
                Sku = new SkuArgs { Family = "Gen5", Name = "Basic", Tier = "Basic" }
            });

            
            SqlConnectionString = Output.Format($"Server=tcp:{sqlServer.Name}.database.windows.net;initial catalog={database.Name};User ID={administratorLogin};Password={administratorLoginPassword};Persist Security Info=true;");
        }
    }
}