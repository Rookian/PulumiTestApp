namespace PulumiTestApp
{
    public record StackConfig(string Location, string Environment)
    {
        public DeploymentAccount DeploymentAccount => new();
    };

    public record DeploymentAccount
    {
        public string ClientId => "bbf847c3-5110-4204-ad81-d81c309e0338";
        public string ObjectId => "a60745a7-184b-418e-9a1e-76f1e09ceb4b";
        public string Name => "DeploymentAccount";
    }
}