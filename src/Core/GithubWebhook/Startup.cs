using Amazon.S3;
using Amazon.StepFunctions;

using Cythral.CloudFormation.AwsUtils.CloudFormation;
using Cythral.CloudFormation.GithubWebhook.Github;
using Cythral.CloudFormation.GithubWebhook.Pipelines;

using Lambdajection.Core;

using Microsoft.Extensions.DependencyInjection;

namespace Cythral.CloudFormation.GithubWebhook
{
    public class Startup : ILambdaStartup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.UseAwsService<IAmazonS3>();
            services.UseAwsService<IAmazonStepFunctions>();
            services.AddSingleton<GithubHttpClient>();
            services.AddSingleton<Sha256SumComputer>();
            services.AddSingleton<GithubFileFetcher>();
            services.AddSingleton<GithubStatusNotifier>();
            services.AddSingleton<RequestValidator>();
            services.AddSingleton<DeployStackFacade>();
            services.AddSingleton<PipelineDeployer>();
            services.AddSingleton<PipelineStarter>();
        }
    }
}