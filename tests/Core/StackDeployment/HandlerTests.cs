using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using Amazon.CloudFormation.Model;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;
using Amazon.ElasticLoadBalancingV2;
using Amazon.ElasticLoadBalancingV2.Model;
using Amazon.Lambda.SQSEvents;

using Cythral.CloudFormation.Aws;
using Cythral.CloudFormation.Events;
using Cythral.CloudFormation.StackDeployment;
using Cythral.CloudFormation.StackDeployment.TemplateConfig;

using NSubstitute;

using NUnit.Framework;
using NSubstitute.ClearExtensions;

using static Amazon.ElasticLoadBalancingV2.TargetHealthStateEnum;
using static System.Text.Json.JsonSerializer;

using SNSRecord = Amazon.Lambda.SNSEvents.SNSEvent.SNSRecord;
using SNSMessage = Amazon.Lambda.SNSEvents.SNSEvent.SNSMessage;
using Tag = Amazon.CloudFormation.Model.Tag;

namespace Cythral.CloudFormation.Tests.StackDeployment
{
    public class HandlerTests
    {
        private static DeployStackFacade stackDeployer = Substitute.For<DeployStackFacade>();
        private static ParseConfigFileFacade parseConfigFileFacade = Substitute.For<ParseConfigFileFacade>();
        private static IAmazonStepFunctions stepFunctionsClient = Substitute.For<IAmazonStepFunctions>();
        private static S3GetObjectFacade s3GetObjectFacade = Substitute.For<S3GetObjectFacade>();
        private static TokenGenerator tokenGenerator = Substitute.For<TokenGenerator>();
        private static RequestFactory requestFactory = Substitute.For<RequestFactory>();
        private static StepFunctionsClientFactory stepFunctionsClientFactory = Substitute.For<StepFunctionsClientFactory>();

        private const string stackName = "stackName";
        private const string location = "location";
        private const string templateFileName = "templateFileName";
        private const string roleArn = "roleArn";
        private const string template = "template";
        private const string actionMode = "actionMode";
        private const string templateConfigurationFileName = "configurationFileName";
        private const string notificationArn = "notificationArn";
        private const string clientRequestToken = "clientRequestToken";
        private const string createdToken = "createdToken";
        private string templateConfiguration = "templateConfiguration";

        private TemplateConfiguration configuration = new TemplateConfiguration
        {
            Parameters = new List<Parameter>
            {
                new Parameter { ParameterKey = "A", ParameterValue = "B" }
            },
            Tags = new List<Tag>
            {
                new Tag { Key = "A", Value = "B" }
            },
            StackPolicy = new StackPolicyBody
            {
                Value = "body"
            }
        };

        [SetUp]
        public void SetupS3GetObjectFacade()
        {
            TestUtils.SetPrivateStaticField(typeof(Handler), "s3GetObjectFacade", s3GetObjectFacade);
            s3GetObjectFacade.ClearReceivedCalls();
            s3GetObjectFacade.GetZipEntryInObject(Arg.Any<string>(), Arg.Is(templateFileName)).Returns(template);
            s3GetObjectFacade.GetZipEntryInObject(Arg.Any<string>(), Arg.Is(templateConfigurationFileName)).Returns(templateConfiguration);
        }

        [SetUp]
        public void SetupStackDeployer()
        {
            TestUtils.SetPrivateStaticField(typeof(Handler), "stackDeployer", stackDeployer);
            stackDeployer.ClearSubstitute();
        }

        [SetUp]
        public void SetupParseConfigFileFacade()
        {
            TestUtils.SetPrivateStaticField(typeof(Handler), "parseConfigFileFacade", parseConfigFileFacade);
            parseConfigFileFacade.ClearSubstitute();
            parseConfigFileFacade.Parse(Arg.Any<string>()).Returns(configuration);
        }

        [SetUp]
        public void SetupTokenGenerator()
        {
            TestUtils.SetPrivateStaticField(typeof(Handler), "tokenGenerator", tokenGenerator);
            tokenGenerator.ClearSubstitute();
            tokenGenerator.Generate(Arg.Any<SQSEvent>(), Arg.Any<Request>()).Returns(createdToken);
        }

        [SetUp]
        public void SetupEnvvars()
        {
            Environment.SetEnvironmentVariable("NOTIFICATION_ARN", notificationArn);
        }

        [SetUp]
        public void SetupRequestFactory()
        {
            TestUtils.SetPrivateStaticField(typeof(Handler), "requestFactory", requestFactory);
            requestFactory.ClearSubstitute();
        }

        [SetUp]
        public void SetupStepFunctions()
        {
            TestUtils.SetPrivateStaticField(typeof(Handler), "stepFunctionsClientFactory", stepFunctionsClientFactory);
            stepFunctionsClientFactory.ClearSubstitute();
            stepFunctionsClientFactory.Create().Returns(stepFunctionsClient);
        }

        private Request CreateRequest()
        {
            var req = new Request
            {
                ZipLocation = location,
                TemplateFileName = templateFileName,
                TemplateConfigurationFileName = templateConfigurationFileName,
                StackName = stackName,
                RoleArn = roleArn,
                Token = clientRequestToken
            };

            requestFactory.CreateFromSqsEvent(Arg.Any<SQSEvent>()).Returns(req);
            return req;
        }

        [Test]
        public void SQSEventIsParsed()
        {
            var request = CreateRequest();
            var sqs = Substitute.For<SQSEvent>();

            Assert.ThrowsAsync<Exception>(() => Handler.Handle(sqs));

            requestFactory.Received().CreateFromSqsEvent(Arg.Is(sqs));
        }

        [Test]
        public async Task TemplateIsRetrieved()
        {
            var request = CreateRequest();
            var sqs = Substitute.For<SQSEvent>();

            Assert.ThrowsAsync<Exception>(() => Handler.Handle(sqs));

            await s3GetObjectFacade.Received().GetZipEntryInObject(Arg.Is(location), Arg.Is(templateFileName));
        }

        [Test]
        public async Task TemplateConfigurationIsRetrieved()
        {
            var request = CreateRequest();
            var sqs = Substitute.For<SQSEvent>();

            Assert.ThrowsAsync<Exception>(() => Handler.Handle(sqs));

            await s3GetObjectFacade.Received().GetZipEntryInObject(Arg.Is(location), Arg.Is(templateConfigurationFileName));
        }

        [Test]
        public async Task TokenIsGenerated()
        {
            var request = CreateRequest();
            var sqs = Substitute.For<SQSEvent>();

            Assert.ThrowsAsync<Exception>(() => Handler.Handle(sqs));

            await tokenGenerator.Received().Generate(Arg.Is(sqs), Arg.Is(request));
        }

        [Test]
        public async Task TemplateConfigurationIsNotRetrievedIfNotGiven()
        {
            var request = CreateRequest();
            var sqs = Substitute.For<SQSEvent>();
            request.TemplateConfigurationFileName = null;

            Assert.ThrowsAsync<Exception>(() => Handler.Handle(sqs));

            await s3GetObjectFacade.DidNotReceive().GetZipEntryInObject(Arg.Is(location), Arg.Is((string)null));
        }

        [Test]
        public async Task TemplateConfigurationIsNotRetrievedIfNotBlank()
        {
            var request = CreateRequest();
            var sqs = Substitute.For<SQSEvent>();
            request.TemplateConfigurationFileName = "";

            Assert.ThrowsAsync<Exception>(() => Handler.Handle(sqs));

            await s3GetObjectFacade.DidNotReceive().GetZipEntryInObject(Arg.Is(location), Arg.Is(""));
        }

        [Test]
        public async Task DeployWasCalled()
        {
            var request = CreateRequest();
            var sqs = Substitute.For<SQSEvent>();

            Assert.ThrowsAsync<Exception>(() => Handler.Handle(sqs));

            await stackDeployer.Received().Deploy(
                Arg.Is<DeployStackContext>(c =>
                    c.StackName == stackName &&
                    c.Template == template &&
                    c.RoleArn == roleArn &&
                    c.NotificationArn == notificationArn &&
                    c.Parameters == configuration.Parameters &&
                    c.Tags == configuration.Tags &&
                    c.StackPolicyBody == configuration.StackPolicy.ToString() &&
                    c.ClientRequestToken == createdToken
                )
            );
        }

        [Test]
        public async Task StepFunctionsNotifiedIfDeployFailed()
        {
            var request = CreateRequest();
            var sqs = Substitute.For<SQSEvent>();
            var message = "message";

            stackDeployer.Deploy(Arg.Any<DeployStackContext>()).Returns(x => throw new Exception(message));

            await Handler.Handle(sqs);

            stepFunctionsClientFactory.Received().Create();
            await stepFunctionsClient.Received().SendTaskFailureAsync(Arg.Is<SendTaskFailureRequest>(req =>
                req.TaskToken == clientRequestToken &&
                req.Cause == message
            ));
        }
    }
}