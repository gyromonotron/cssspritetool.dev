using Amazon.CDK;
using Amazon.CDK.AWS.CloudFront.Origins;
using Amazon.CDK.AWS.CloudFront;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.S3;
using Constructs;
using System.Collections.Generic;
using static Amazon.CDK.AWS.CloudFront.CfnOriginAccessControl;
using static Amazon.CDK.AWS.CloudFront.CfnDistribution;
using Function = Amazon.CDK.AWS.Lambda.Function;
using FunctionProps = Amazon.CDK.AWS.Lambda.FunctionProps;

namespace Cdk
{
    public class CssSpriteToolstack : Stack
    {
        internal CssSpriteToolstack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            #region Context Variables

            var bucket_name = this.Node.TryGetContext("BucketName") as string ?? "cssspriter";
            var result_folder_path = this.Node.TryGetContext("ResultFolderPath") as string ?? "result/";
            var allowed_extensions = this.Node.TryGetContext("AllowedExtensions") as string ?? ".bmp, .jpg, .jpeg, .jpe, .jif, .jfif, .jfi, .png, .webp, .gif";
            var allowed_total_files = this.Node.TryGetContext("AllowedTotalFiles") as int? ?? 50;
            var allowed_file_size = this.Node.TryGetContext("AllowedFileSize") as int? ?? 5242880;
            var lambdas_architecture = this.Node.TryGetContext("LambdasArchitecture") as string ?? "X86_64";

            #endregion

            #region S3 Bucket

            var s3SiteBucket = new Bucket(this, "SiteBucket", new BucketProps
            {
                BucketName = bucket_name,
                RemovalPolicy = RemovalPolicy.DESTROY,
                BlockPublicAccess = BlockPublicAccess.BLOCK_ALL,
                LifecycleRules =
                [
                    new LifecycleRule
                    {
                        Prefix = "r/",
                        Enabled = true,
                        Expiration = Duration.Days(1)
                    }
                ],
                Cors =
                [
                    new CorsRule
                    {
                        AllowedMethods = [HttpMethods.GET, HttpMethods.PUT, HttpMethods.POST],
                        AllowedOrigins = ["*"],
                        AllowedHeaders = ["*"],
                        MaxAge = 3000
                    }
                ],
            });

            var getPostUrlLambdaRole = new Role(this, "GetPostUrlLambdaRole", new RoleProps
            {
                AssumedBy = new ServicePrincipal("lambda.amazonaws.com"),
                ManagedPolicies =
                [
                    ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSLambdaBasicExecutionRole")
                ],
                InlinePolicies = new Dictionary<string, PolicyDocument>
                {
                    ["S3Policy"] = new PolicyDocument(new PolicyDocumentProps
                    {
                        Statements =
                    [
                        new PolicyStatement(new PolicyStatementProps
                            {
                                Actions = ["s3:PutObject"],
                                Resources = [s3SiteBucket.ArnForObjects("*"), s3SiteBucket.BucketArn]
                            })
                        ]
                    })
                }
            });

            #endregion

            #region Lambdas

            var lambdaArchitecture = lambdas_architecture == "X86_64" ? Architecture.X86_64 : Architecture.ARM_64;
            var getPostUrlLambda = new Function(this, "GetPostUrlLambda", new FunctionProps
            {
                FunctionName = "CssSpriteTool-GetPostUrlLambda",
                Code = Code.FromAsset("../api/get-url-lambda/get-url-lambda"),
                Architecture = lambdaArchitecture,
                Handler = "app.lambda_handler",
                Runtime = Runtime.PYTHON_3_12,
                Timeout = Duration.Seconds(5),
                MemorySize = 256,
                Role = getPostUrlLambdaRole,
                Environment = new Dictionary<string, string>
                {
                    ["BUCKET_NAME"] = s3SiteBucket.BucketName,
                    ["RESULT_FOLDER_PATH"] = result_folder_path,
                    ["ALLOWED_EXTENSIONS"] = allowed_extensions,
                    ["ALLOWED_FILE_SIZE"] = allowed_file_size.ToString()
                }
            });

            var getPostUrlLambda_FunctionUrl = new FunctionUrl(this, "GetPostUrlLambdaFunctionUrl", new FunctionUrlProps
            {
                Function = getPostUrlLambda,
                AuthType = FunctionUrlAuthType.AWS_IAM,
                InvokeMode = InvokeMode.BUFFERED,
                Cors = new FunctionUrlCorsOptions
                {
                    AllowedOrigins = ["*"],
                    AllowedMethods = [HttpMethod.GET],
                    AllowedHeaders = ["*"]
                }
            });

            var spriteGenerateLambdaRole = new Role(this, "SpriteGenerateLambdaRole", new RoleProps
            {
                AssumedBy = new ServicePrincipal("lambda.amazonaws.com"),
                ManagedPolicies =
                [
                    ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSLambdaBasicExecutionRole")
                ],
                InlinePolicies = new Dictionary<string, PolicyDocument>
                {
                    ["S3Policy"] = new PolicyDocument(new PolicyDocumentProps
                    {
                        Statements =
                    [
                        new PolicyStatement(new PolicyStatementProps
                            {
                                Actions = ["s3:PutObject", "s3:GetObject"],
                                Resources = [s3SiteBucket.ArnForObjects("*"), s3SiteBucket.BucketArn]
                            })
                        ]
                    })
                }
            });

            var spriteGenerateLambda = new Function(this, "SpriteGenerateLambda", new FunctionProps
            {
                FunctionName = "CssSpriteTool-SpriteGenerateLambda",
                Code = Code.FromAsset("../api/upload-lambda/SpriteGenerateFunction", new Amazon.CDK.AWS.S3.Assets.AssetOptions
                {
                    Bundling = new BundlingOptions()
                    {
                        Image = Runtime.DOTNET_8.BundlingImage,
                        User = "root",
                        OutputType = BundlingOutput.ARCHIVED,
                        Command = [
                            "/bin/sh",
                            "-c",
                            " dotnet tool install -g Amazon.Lambda.Tools"+
                            " && dotnet tool update -g Amazon.Lambda.Tools"+
                            " && dotnet build"+
                            " && dotnet lambda package --output-package /asset-output/function.zip"
                        ]
                    }
                }),
                Architecture = lambdaArchitecture,
                Handler = "SpriteGenerateFunction",
                Runtime = Runtime.DOTNET_8,
                Timeout = Duration.Seconds(60),
                MemorySize = 1024,
                Role = spriteGenerateLambdaRole,
                Environment = new Dictionary<string, string>
                {
                    ["ANNOTATIONS_HANDLER"] = "PostFunctionHandler",
                    ["BucketName"] = s3SiteBucket.BucketName,
                    ["ResultFolderPath"] = result_folder_path,
                    ["AllowedExtensions"] = allowed_extensions,
                    ["AllowedTotalFiles"] = allowed_total_files.ToString()
                }
            });

            var spriteGenerateLambda_FunctionUrl = new FunctionUrl(this, "SpriteGenerateLambdaFunctionUrl", new FunctionUrlProps
            {
                Function = spriteGenerateLambda,
                AuthType = FunctionUrlAuthType.AWS_IAM,
                InvokeMode = InvokeMode.BUFFERED,
                Cors = new FunctionUrlCorsOptions
                {
                    AllowedOrigins = ["*"],
                    AllowedMethods = [HttpMethod.POST],
                    AllowedHeaders = ["*"]
                }
            });

            #endregion

            #region CloudFront

            var cfnS3OriginAccessControl = new CfnOriginAccessControl(this, "S3OriginAccessControl", new CfnOriginAccessControlProps
            {
                OriginAccessControlConfig = new OriginAccessControlConfigProperty
                {
                    Name = "CssSpriteTool-S3-OriginAccessControl",
                    OriginAccessControlOriginType = "s3",
                    SigningBehavior = "always",
                    SigningProtocol = "sigv4"
                }
            });

            var cfnLambdaOriginAccessControl = new CfnOriginAccessControl(this, "LambdaOriginAccessControl", new CfnOriginAccessControlProps
            {
                OriginAccessControlConfig = new OriginAccessControlConfigProperty
                {
                    Name = "CssSpriteTool-Lambda-OriginAccessControl",
                    OriginAccessControlOriginType = "lambda",
                    SigningBehavior = "always",
                    SigningProtocol = "sigv4",
                }
            });

            var cfnDistribution = new Distribution(this, "SiteDistribution", new DistributionProps
            {
                DefaultRootObject = "index.html",
                PriceClass = PriceClass.PRICE_CLASS_100,
                DefaultBehavior = new BehaviorOptions
                {
                    Origin = new S3OacOrigin(s3SiteBucket, new S3OriginProps
                    {
                        OriginId = "Site",
                        OriginAccessIdentity = null,
                        ConnectionAttempts = 3,
                        ConnectionTimeout = Duration.Seconds(10),
                        OriginPath = "/site",
                    }),
                    CachePolicy = CachePolicy.CACHING_OPTIMIZED,
                    ViewerProtocolPolicy = ViewerProtocolPolicy.REDIRECT_TO_HTTPS,
                    AllowedMethods = AllowedMethods.ALLOW_GET_HEAD_OPTIONS,
                    CachedMethods = CachedMethods.CACHE_GET_HEAD_OPTIONS,
                    OriginRequestPolicy = OriginRequestPolicy.ALL_VIEWER,
                    Compress = true,
                },
                AdditionalBehaviors = new Dictionary<string, IBehaviorOptions>
                {
                    ["/r/*"] = new BehaviorOptions
                    {
                        Origin = new S3OacOrigin(s3SiteBucket, new S3OriginProps
                        {
                            OriginId = "Result",
                            OriginAccessIdentity = null,
                            ConnectionAttempts = 3,
                            ConnectionTimeout = Duration.Seconds(10),
                            OriginPath = "/result",
                        }),
                        AllowedMethods = AllowedMethods.ALLOW_GET_HEAD,
                        CachePolicy = CachePolicy.CACHING_OPTIMIZED,
                        ViewerProtocolPolicy = ViewerProtocolPolicy.HTTPS_ONLY
                    },
                    ["/api/url"] = new BehaviorOptions
                    {
                        Origin = new FunctionUrlOrigin(getPostUrlLambda_FunctionUrl, new FunctionUrlOriginProps
                        {
                            OriginId = "GetPostUrl-Lambda",
                            ReadTimeout = Duration.Seconds(10)
                        }),
                        AllowedMethods = AllowedMethods.ALLOW_GET_HEAD,
                        CachePolicy = CachePolicy.CACHING_DISABLED,
                        OriginRequestPolicy = OriginRequestPolicy.ALL_VIEWER_EXCEPT_HOST_HEADER,
                        ViewerProtocolPolicy = ViewerProtocolPolicy.HTTPS_ONLY
                    },
                    ["/api/process"] = new BehaviorOptions
                    {
                        Origin = new FunctionUrlOrigin(spriteGenerateLambda_FunctionUrl, new FunctionUrlOriginProps
                        {
                            OriginId = "SpriteGenerate-Lambda",
                            ReadTimeout = Duration.Seconds(60),
                        }),
                        AllowedMethods = AllowedMethods.ALLOW_ALL,
                        CachePolicy = CachePolicy.CACHING_DISABLED,
                        OriginRequestPolicy = OriginRequestPolicy.ALL_VIEWER_EXCEPT_HOST_HEADER,
                        ViewerProtocolPolicy = ViewerProtocolPolicy.HTTPS_ONLY
                    }
                }
            });

            s3SiteBucket.AddToResourcePolicy(new PolicyStatement(new PolicyStatementProps
            {
                Actions = ["s3:GetObject"],
                Principals = [new ServicePrincipal("cloudfront.amazonaws.com")],
                Effect = Effect.ALLOW,
                Resources = [s3SiteBucket.ArnForObjects("*")],
                Conditions = new Dictionary<string, object>
                {
                    ["StringEquals"] = new Dictionary<string, object>
                    {
                        ["AWS:SourceArn"] = $"arn:aws:cloudfront::{this.Account}:distribution/{cfnDistribution.DistributionId}"
                    }
                }
            }));

            // workaround using the L1 construct to attach the OriginAccessControl to the CloudFront Distribution
            var l1CfnDistribution = cfnDistribution.Node.DefaultChild as CfnDistribution;
            l1CfnDistribution.AddPropertyOverride("DistributionConfig.Origins.0.OriginAccessControlId", cfnS3OriginAccessControl.AttrId);
            l1CfnDistribution.AddPropertyOverride("DistributionConfig.Origins.1.OriginAccessControlId", cfnS3OriginAccessControl.AttrId);
            l1CfnDistribution.AddPropertyOverride("DistributionConfig.Origins.2.OriginAccessControlId", cfnLambdaOriginAccessControl.AttrId);
            l1CfnDistribution.AddPropertyOverride("DistributionConfig.Origins.3.OriginAccessControlId", cfnLambdaOriginAccessControl.AttrId);

            #endregion

            #region Lambda Permissions

            getPostUrlLambda.AddPermission("AllowCloudFront", new Permission
            {
                Principal = new ServicePrincipal("cloudfront.amazonaws.com"),
                Action = "lambda:InvokeFunctionUrl",
                SourceArn = $"arn:aws:cloudfront::{this.Account}:distribution/{cfnDistribution.DistributionId}",
                SourceAccount = this.Account
            });

            spriteGenerateLambda.AddPermission("AllowCloudFront", new Permission
            {
                Principal = new ServicePrincipal("cloudfront.amazonaws.com"),
                Action = "lambda:InvokeFunctionUrl",
                SourceArn = $"arn:aws:cloudfront::{this.Account}:distribution/{cfnDistribution.DistributionId}",
                SourceAccount = this.Account
            });

            #endregion
        }
    }
    public class S3OacOrigin(IBucket bucket, IOriginProps props = null) : OriginBase(bucket.BucketRegionalDomainName, props)
    {
        // workaround to avoid the "OriginAccessIdentity" property to be rendered in the CloudFormation template
        protected override IS3OriginConfigProperty RenderS3OriginConfig()
        {
            return new S3OriginConfigProperty
            {
                OriginAccessIdentity = string.Empty
            };
        }
    }
}
