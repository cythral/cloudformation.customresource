using System;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Cythral.CloudFormation.Cicd;
using Cythral.CloudFormation.Cicd.Events;
using Cythral.CloudFormation.Cicd.Entities;
using Cythral.CloudFormation.Cicd.Exceptions;
using Amazon.Lambda.ApplicationLoadBalancerEvents;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using FluentAssertions;
using NSubstitute;
using RichardSzalay.MockHttp;

using static Amazon.CloudFormation.OnFailure;
using static System.Net.HttpStatusCode;
using static System.Text.Json.JsonSerializer;

namespace Cythral.CloudFormation.Tests.Cicd {
    public class StackDeployerTest {
        [Test]
        public async Task DeployCallsCreateStackIfNotExists() {
            var stackName = "test-stack";
            var exampleTemplate = "this is a bad example template.";
            var cloudformationClient = Substitute.For<IAmazonCloudFormation>();
            
            cloudformationClient
            .DescribeStacksAsync(Arg.Is<DescribeStacksRequest>(req =>
                req.StackName == stackName
            ))
            .Returns(new DescribeStacksResponse {
                Stacks = new List<Stack>()
            }); 

            cloudformationClient
            .CreateStackAsync(Arg.Any<CreateStackRequest>())
            .Returns(new CreateStackResponse {});

            await StackDeployer.Deploy(stackName, exampleTemplate, cloudformationClient: cloudformationClient);
            await cloudformationClient
            .Received()
            .CreateStackAsync(Arg.Is<CreateStackRequest>(req =>
                req.StackName == stackName &&
                req.TemplateBody == exampleTemplate &&
                req.Capabilities.Any(capability => capability == "CAPABILITY_IAM") &&
                req.Capabilities.Any(capability => capability == "CAPABILITY_NAMED_IAM") &&
                req.OnFailure == DELETE
            ));
        }

        [Test]
        public async Task DeployCallsUpdateStackIfExists() {
            var stackName = "test-stack";
            var exampleTemplate = "this is a bad example template.";
            var cloudformationClient = Substitute.For<IAmazonCloudFormation>();
            
            cloudformationClient
            .DescribeStacksAsync(Arg.Is<DescribeStacksRequest>(req =>
                req.StackName == stackName
            ))
            .Returns(new DescribeStacksResponse {
                Stacks = new List<Stack> {
                    new Stack {
                        StackName = stackName
                    }
                }
            }); 

            cloudformationClient
            .UpdateStackAsync(Arg.Any<UpdateStackRequest>())
            .Returns(new UpdateStackResponse {});

            await StackDeployer.Deploy(stackName, exampleTemplate, cloudformationClient: cloudformationClient);
            await cloudformationClient
            .Received()
            .UpdateStackAsync(Arg.Is<UpdateStackRequest>(req =>
                req.StackName == stackName &&
                req.TemplateBody == exampleTemplate &&
                req.Capabilities.Any(capability => capability == "CAPABILITY_IAM") &&
                req.Capabilities.Any(capability => capability == "CAPABILITY_NAMED_IAM")
            ));
        }
    }
}