using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.SQSEvents;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;

using Cythral.CloudFormation.AwsUtils;
using Cythral.CloudFormation.AwsUtils.CloudFormation;
using Cythral.CloudFormation.AwsUtils.SimpleStorageService;
using Cythral.CloudFormation.GithubUtils;
using Cythral.CloudFormation.StackDeployment.TemplateConfig;

using Lambdajection.Attributes;
using Lambdajection.Core;

using Octokit;

using static System.Text.Json.JsonSerializer;

namespace Cythral.CloudFormation.StackDeployment
{
    [Lambda(typeof(Startup))]
    public partial class Handler
    {
        private const string notificationArnKey = "NOTIFICATION_ARN";
        private readonly DeployStackFacade stackDeployer;
        private readonly S3GetObjectFacade s3GetObjectFacade;
        private readonly ParseConfigFileFacade parseConfigFileFacade;
        private readonly TokenGenerator tokenGenerator;
        private readonly RequestFactory requestFactory;
        private readonly IAmazonStepFunctions stepFunctionsClient;
        private readonly IAwsFactory<IAmazonCloudFormation> cloudformationFactory;
        private readonly PutCommitStatusFacade putCommitStatusFacade;

        public Handler(
            DeployStackFacade stackDeployer,
            S3GetObjectFacade s3GetObjectFacade,
            ParseConfigFileFacade parseConfigFileFacade,
            TokenGenerator tokenGenerator,
            RequestFactory requestFactory,
            IAmazonStepFunctions stepFunctionsClient,
            IAwsFactory<IAmazonCloudFormation> cloudformationFactory,
            PutCommitStatusFacade putCommitStatusFacade
        )
        {
            this.stackDeployer = stackDeployer;
            this.s3GetObjectFacade = s3GetObjectFacade;
            this.parseConfigFileFacade = parseConfigFileFacade;
            this.tokenGenerator = tokenGenerator;
            this.requestFactory = requestFactory;
            this.stepFunctionsClient = stepFunctionsClient;
            this.cloudformationFactory = cloudformationFactory;
            this.putCommitStatusFacade = putCommitStatusFacade;
        }

        public async Task<Response> Handle(
            SQSEvent sqsEvent,
            ILambdaContext context = null
        )
        {
            var request = requestFactory.CreateFromSqsEvent(sqsEvent);

            try
            {
                var notificationArn = Environment.GetEnvironmentVariable(notificationArnKey);
                var template = await s3GetObjectFacade.GetZipEntryInObject(request.ZipLocation, request.TemplateFileName);
                var config = await GetConfig(request);
                var token = await tokenGenerator.Generate(sqsEvent, request);

                await PutCommitStatus(request, CommitState.Pending);
                await stackDeployer.Deploy(new DeployStackContext
                {
                    StackName = request.StackName,
                    Template = template,
                    RoleArn = request.RoleArn,
                    NotificationArn = notificationArn,
                    Parameters = MergeParameters(config?.Parameters, request.ParameterOverrides),
                    Tags = config?.Tags,
                    StackPolicyBody = config?.StackPolicy?.Value,
                    ClientRequestToken = token,
                    Capabilities = request.Capabilities,
                });
            }
            catch (NoUpdatesException)
            {
                var outputs = await GetStackOutputs(request.StackName, request.RoleArn);
                var response = await stepFunctionsClient.SendTaskSuccessAsync(new SendTaskSuccessRequest
                {
                    TaskToken = request.Token,
                    Output = Serialize(outputs)
                });

                await PutCommitStatus(request, CommitState.Success);

                return new Response
                {
                    Success = true
                };
            }
            catch (Exception e)
            {
                var response = await stepFunctionsClient.SendTaskFailureAsync(new SendTaskFailureRequest
                {
                    TaskToken = request.Token,
                    Cause = e.Message
                });

                await PutCommitStatus(request, CommitState.Failure);

                return new Response
                {
                    Success = true
                };
            }

            throw new Exception();
        }

        private async Task<TemplateConfiguration> GetConfig(Request request)
        {
            var fileName = request.TemplateConfigurationFileName;

            if (fileName != null && fileName != "")
            {
                var source = await s3GetObjectFacade.GetZipEntryInObject(request.ZipLocation, fileName);
                return parseConfigFileFacade.Parse(source);
            }

            return null;
        }

        private static List<Parameter> MergeParameters(List<Parameter> parameters, Dictionary<string, string> overrides)
        {
            var result = parameters?.ToDictionary(param => param.ParameterKey, param => param.ParameterValue) ?? new Dictionary<string, string>();
            overrides = overrides ?? new Dictionary<string, string>();

            foreach (var entry in overrides)
            {
                result[entry.Key] = entry.Value;
            }

            return result.Select(entry => new Parameter { ParameterKey = entry.Key, ParameterValue = entry.Value }).ToList();
        }

        private async Task<Dictionary<string, string>> GetStackOutputs(string stackId, string roleArn)
        {
            var client = await cloudformationFactory.Create(roleArn);
            var response = await client.DescribeStacksAsync(new DescribeStacksRequest
            {
                StackName = stackId
            });

            return response.Stacks[0].Outputs.ToDictionary(entry => entry.OutputKey, entry => entry.OutputValue);
        }

        private async Task PutCommitStatus(Request request, CommitState state)
        {
            await putCommitStatusFacade.PutCommitStatus(new PutCommitStatusRequest
            {
                CommitState = state,
                ServiceName = "AWS CloudFormation",
                DetailsUrl = $"https://console.aws.amazon.com/cloudformation/home?region=us-east-1#/stacks/stackinfo?filteringText=&filteringStatus=active&viewNested=true&hideStacks=false&stackId={request.StackName}",
                ProjectName = request.StackName,
                EnvironmentName = request.EnvironmentName,
                GithubOwner = request.CommitInfo?.GithubOwner,
                GithubRepo = request.CommitInfo?.GithubRepository,
                GithubRef = request.CommitInfo?.GithubRef,
            });
        }
    }
}