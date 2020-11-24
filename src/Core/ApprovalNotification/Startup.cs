using Amazon.S3;
using Amazon.SimpleNotificationService;
using Amazon.StepFunctions;

using Cythral.CloudFormation.ApprovalNotification.Links;
using Cythral.CloudFormation.AwsUtils.SimpleStorageService;

using Lambdajection.Core;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cythral.CloudFormation.ApprovalNotification
{
    public class Startup : ILambdaStartup
    {
        private readonly IConfiguration configuration;

        public Startup(IConfiguration configuration)
        {
            this.configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.UseAwsService<IAmazonSimpleNotificationService>();
            services.UseAwsService<IAmazonS3>();
            services.UseAwsService<IAmazonStepFunctions>();

            services.AddSingleton<S3GetObjectFacade>();
            services.AddSingleton<ComputeHash>(Utils.Hash);
            services.AddSingleton<ApprovalCanceler>();

            services.AddSingleton<ILinkService, DefaultLinkService>();
            services.UseBrighidIdentity<ILinkService, DefaultLinkService>(options => options
                .WithBaseAddress("https://cythr.al/")
                .WithCredentials<Config>("Lambda")
            );
        }
    }
}