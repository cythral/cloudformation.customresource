namespace Cythral.CloudFormation.S3Deployment
{
    public class Request
    {
        public string ZipLocation { get; set; }
        public string DestinationBucket { get; set; }
        public string RoleArn { get; set; }
        public string EnvironmentName { get; set; }
        public CommitInfo CommitInfo { get; set; }
        public SsoConfig SsoConfig { get; set; }
    }
}