using System.Collections.Generic;
namespace Cythral.CloudFormation.StackDeployment
{
    public class Request
    {
        public string ZipLocation { get; set; }
        public string TemplateFileName { get; set; }
        public string TemplateConfigurationFileName { get; set; }
        public List<string> Capabilities { get; set; }
        public Dictionary<string, string> ParameterOverrides { get; set; }
        public string StackName { get; set; }
        public string RoleArn { get; set; }
        public string Token { get; set; }
        public string EnvironmentName { get; set; }
        public CommitInfo CommitInfo { get; set; }
        public SsoConfig SsoConfig { get; set; }
    }




}