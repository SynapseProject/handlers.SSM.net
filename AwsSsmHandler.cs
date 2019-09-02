using Newtonsoft.Json;
using Synapse.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Amazon;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using Amazon.Runtime.Internal;

public class AwsSsmHandler : HandlerRuntimeBase
{
    private HandlerConfig _config;
    private string _progressMessage = "";
    private int _sequenceNumber = 0;
    private string _context = "Execute";
    private SsmCommandResponse _response = null;
    private UserRequest _request = null;
    private ExecuteResult _result = new ExecuteResult()
    {
        Status = StatusType.None,
        BranchStatus = StatusType.None,
        Sequence = int.MaxValue
    };

    public override ExecuteResult Execute(HandlerStartInfo startInfo)
    {
        try
        {
            UpdateProgress("Parsing incoming request...", StatusType.Running);
            _request = DeserializeOrDefault<UserRequest>(startInfo.Parameters);

            UpdateProgress("Executing request" + (startInfo.IsDryRun ? " in dry run mode..." : "..."), StatusType.Running);
            if (ValidateRequest(_request))
            {
                if (!startInfo.IsDryRun)
                {
                    _response = RunSsmCommand(_request, _config).Result;
                    UpdateProgress("Execution is completed.", StatusType.Complete, int.MaxValue);
                    if (_response == null)
                    {
                        _response = new SsmCommandResponse
                        {
                            Status = "Failed"
                        };
                    }
                    _response.Summary = _progressMessage;
                }
                else
                {
                    UpdateProgress("Dry run execution is completed.", StatusType.Complete, int.MaxValue);
                }
            }
        }
        catch (Exception ex)
        {
            string errorMessage = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
            UpdateProgress($"Execution has been aborted due to: {errorMessage}", StatusType.Failed, int.MaxValue, LogLevel.Error, ex);
            _response = new SsmCommandResponse()
            {
                Status = "Failed",
                Summary = _progressMessage
            };
        }

        _result.ExitData = _response;
        return _result;
    }

    public override object GetConfigInstance()
    {
        return new HandlerConfig
        {
            ClientMaxErrorRetry = 10,
            ClientTimeoutSeconds = 120,
            ClientReadWriteTimeoutSeconds = 120,
            CommandMaxConcurrency = "50",
            CommandMaxErrors = "200",
            CommandTimeoutSeconds = 600
        };
    }

    public override IHandlerRuntime Initialize(string values)
    {
        try
        {
            _config = DeserializeOrNew<HandlerConfig>(values) ?? new HandlerConfig();
        }
        catch (Exception ex)
        {
            OnLogMessage("Initialization", "Encountered exception while deserializing handler config.", LogLevel.Error, ex);
        }

        return this;
    }

    public override object GetParametersInstance()
    {
        List<string> script = new List<string> { "$env:computername" };
        Dictionary<string, List<string>> commandParameters = new Dictionary<string, List<string>>();
        commandParameters.Add("commands", script);

        return new UserRequest
        {
            InstanceId = "i-12345678",
            CommandType = "send-command", // "get-command-invocation"
            CommandId = "xxxxxxxx",
            CommandDocument = "AWS-RunPowerShellScript",
            CommandParameters = commandParameters,
            CommandComment = "xxxxxxxx",
            AwsRegion = "eu-west-1",
            AwsRole = "xxxxxxxx"
        };
    }

    public static bool ValidateRequest(UserRequest request)
    {
        string errorMessage = string.Empty;

        if (request == null)
        {
            errorMessage = "Request cannot be null or empty.";
        }
        else if (string.IsNullOrWhiteSpace(request.InstanceId))
        {
            errorMessage = "Instance id cannot be null or empty.";
        }
        else if (!IsValidCommandType(request.CommandType))
        {
            errorMessage = "Command type cannot be null or empty. Support types: send-command, get-command-invocation.";
        }
        else if (request.CommandType.ToLowerInvariant() == "get-command-invocation" && string.IsNullOrWhiteSpace(request.CommandId))
        {
            errorMessage = "Command id cannot be null or empty for 'get-command-invocation'.";
        }
        else if (request.CommandType.ToLowerInvariant() == "send-command" && string.IsNullOrWhiteSpace(request.CommandDocument))
        {
            errorMessage = "Command document cannot be null or empty for 'send-command'.";
        }
        else if (request.CommandType.ToLowerInvariant() == "send-command" && string.IsNullOrWhiteSpace(request.CommandComment))
        {
            errorMessage = "Command comment cannot be null or empty for 'send-command'.";
        }
        else if (!IsAwsRegion(request.AwsRegion))
        {
            errorMessage = "AWS region specified is not valid.";
        }
        else if (string.IsNullOrWhiteSpace(request.AwsRole))
        {
            errorMessage = "AWS role cannot be null or empty.";
        }

        if (!string.IsNullOrWhiteSpace(errorMessage))
        {
            throw new Exception(errorMessage);
        }
        return true;
    }

    public static bool IsValidCommandType(string commandType)
    {
        bool isValid = false;

        if (!string.IsNullOrWhiteSpace(commandType))
        {
            commandType = commandType.ToLowerInvariant();
            switch (commandType)
            {
                case "get-command-invocation":
                    isValid = true;
                    break;
                case "send-command":
                    isValid = true;
                    break;

                default:
                    break;
            }
        }

        return isValid;
    }

    public static bool IsAwsRegion(string region)
    {
        if (region == null) return false;

        return RegionEndpoint.GetBySystemName(region).DisplayName != "Unknown";
    }

    public async Task<SsmCommandResponse> RunSsmCommand(UserRequest request, HandlerConfig config)
    {
        if (request == null || config == null) return null;
        string errorMessage = string.Empty;
        SsmCommandResponse output = new SsmCommandResponse()
        {
            Status = "Failed" // If no processing is done.
        };

        AmazonSimpleSystemsManagementConfig clientConfig = new AmazonSimpleSystemsManagementConfig()
        {
            MaxErrorRetry = config.ClientMaxErrorRetry,
            Timeout = TimeSpan.FromSeconds(config.ClientTimeoutSeconds),
            ReadWriteTimeout = TimeSpan.FromSeconds(config.ClientReadWriteTimeoutSeconds),
            RegionEndpoint = RegionEndpoint.GetBySystemName(request.AwsRegion) // Or RegionEndpoint.EUWest1
        };

        try
        {
            // https://docs.aws.amazon.com/sdk-for-net/v3/developer-guide/net-dg-config-creds.html
            // Accessing Credentials and Profiles in an Application
            CredentialProfileStoreChain chain = new CredentialProfileStoreChain(config.AwsProfilesLocation);

            AWSCredentials awsCredentials;
            if (chain.TryGetAWSCredentials(request.AwsRole, out awsCredentials))
            {
                // Use awsCredentials
                AmazonSimpleSystemsManagementClient ssmClient =
                    new AmazonSimpleSystemsManagementClient(awsCredentials, clientConfig);
                if (request.CommandType == "send-command")
                {
                    List<string> instanceIds = new List<string> { request.InstanceId };
                    SendCommandRequest commandRequest = new SendCommandRequest(request.CommandDocument, instanceIds);
                    commandRequest.MaxConcurrency = config.CommandMaxConcurrency; // 50%
                    commandRequest.MaxErrors = config.CommandMaxErrors;
                    commandRequest.TimeoutSeconds = config.CommandTimeoutSeconds;
                    commandRequest.Comment = request.CommandComment;
                    commandRequest.Parameters = request.CommandParameters;
                    SendCommandResponse sendCommandResponse = await ssmClient.SendCommandAsync(commandRequest);

                    output.Status = "Complete";
                    output.CommandId = sendCommandResponse.Command.CommandId;
                    output.CommandStatus = sendCommandResponse.Command.StatusDetails;
                    output.ErrorMessage = errorMessage;
                    output.CommandComment = sendCommandResponse.Command.Comment;
                }
                else if (request.CommandType == "get-command-invocation")
                {
                    //GetCommandInvocationRequest commandRequest = new GetCommandInvocationRequest()
                    //{
                    //    CommandId = request.CommandId,
                    //    InstanceId = request.InstanceId,
                    //    PluginName = request.CommandPluginName // If there are more than one plugins, this cannot be null.
                    //};
                    //GetCommandInvocationResponse getCommandResponse =
                    //    await ssmClient.GetCommandInvocationAsync(commandRequest);

                    ListCommandInvocationsRequest commandRequest = new ListCommandInvocationsRequest()
                    {
                        CommandId = request.CommandId,
                        Details = true
                    };
                    ListCommandInvocationsResponse commandResponse = await ssmClient.ListCommandInvocationsAsync(commandRequest);

                    if (commandResponse.CommandInvocations.Count > 0)
                    {
                        output.Status = "Complete";
                        output.CommandId = commandResponse.CommandInvocations[0].CommandId;
                        output.CommandStatus = commandResponse.CommandInvocations[0].StatusDetails;
                        output.CommandComment = commandResponse.CommandInvocations[0].Comment;
                        if (commandResponse.CommandInvocations[0].StatusDetails == "Success" &&
                            commandResponse.CommandInvocations[0].CommandPlugins.Count > 0)
                        {
                            output.StandardOutput = commandResponse.CommandInvocations[0].CommandPlugins[0].Output;
                        }
                    }
                    else
                    {
                        errorMessage = "The command id and instance id specified did not match any invocation.";
                    }
                }
            }
            else
            {
                errorMessage = "AWS credentials cannot be found for the execution.";
            }
        }
        catch (AmazonSimpleSystemsManagementException ex)
        {
            switch (ex.ErrorCode)
            {
                // https://docs.aws.amazon.com/systems-manager/latest/APIReference/API_SendCommand.html
                // Error codes for "SendCommandRequest"
                case "DuplicateInstanceId":
                    errorMessage = "You cannot specify an instance ID in more than one association.";
                    break;
                case "InternalServerError":
                    errorMessage = "Internal server error.";
                    break;
                case "InvalidDocument":
                    errorMessage = "The specified document does not exist.";
                    break;
                case "InvalidDocumentVersion":
                    errorMessage = "The document version is not valid or does not exist.";
                    break;
                case "ExpiredTokenException":
                    errorMessage = "The security token included in the request is expired.";
                    break;
                case "InvalidInstanceId":
                    errorMessage = "Possible causes are the 1) server instance may not be running 2) Server instance may not exist. 3) SSM agent may not be running. 4) Account used does not have permssion to access the instance.";
                    break;
                case "InvalidNotificationConfig":
                    errorMessage = "One or more configuration items is not valid.";
                    break;
                case "InvalidOutputFolder":
                    errorMessage = "The S3 bucket does not exist.";
                    break;
                case "InvalidParameters":
                    errorMessage =
                        "You must specify values for all required parameters in the Systems Manager document.";
                    break;
                case "InvalidRole":
                    errorMessage = "The role name can't contain invalid characters.";
                    break;
                case "MaxDocumentSizeExceeded":
                    errorMessage = "The size limit of a document is 64 KB.";
                    break;
                case "UnsupportedPlatformType":
                    errorMessage = "The document does not support the platform type of the given instance ID(s).";
                    break;
                // Error codes for "GetcommandInvocation"
                case "InvalidCommandId":
                    errorMessage = "The command ID is invalid.";
                    break;
                case "InvocationDoesNotExist":
                    errorMessage =
                        "The command id and instance id specified did not match any invocation.";
                    break;
                case "ValidationException":
                    errorMessage = ex.Message;
                    break;
                default:
                    errorMessage = ex.Message;
                    break;
            }
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
        }

        if (!string.IsNullOrWhiteSpace(errorMessage))
        {

            throw new Exception(errorMessage);

        }
        return output;
    }

    private void UpdateProgress(string message, StatusType status, int sequenceNumber = 0, LogLevel logLevel = LogLevel.Info, Exception ex = null)
    {
        _progressMessage = $"{DateTime.UtcNow} {message}";
        _result.Status = status;
        _sequenceNumber = sequenceNumber == 0 ? _sequenceNumber++ : sequenceNumber;
        OnProgress(_context, _progressMessage, _result.Status, _sequenceNumber);
        OnLogMessage(_context, _progressMessage, logLevel, ex);
    }
}

public class HandlerConfig
{
    public string AwsProfilesLocation { get; set; }

    // https://docs.aws.amazon.com/sdk-for-net/v3/developer-guide/retries-timeouts.html
    public int ClientMaxErrorRetry { get; set; } = 4;

    public int ClientTimeoutSeconds { get; set; } = 100;

    public int ClientReadWriteTimeoutSeconds { get; set; } = 300;

    // https://docs.aws.amazon.com/systems-manager/latest/APIReference/API_SendCommand.html
    public string CommandMaxConcurrency { get; set; } = "50";

    public string CommandMaxErrors { get; set; } = "0";

    // If this time is reached and the command has not already started running, it will not run.
    public int CommandTimeoutSeconds { get; set; } = 30;
}

public class SsmCommandResponse
{
    public string Status { get; set; } // "Complete", "Failed"

    public string CommandId { get; set; }

    public string CommandStatus { get; set; }

    public string CommandComment { get; set; }

    public string ErrorMessage { get; set; }

    public string StandardOutput { get; set; }

    public string StandardError { get; set; }

    public string Summary { get; set; } // Handler Execution Summary
}

public class UserRequest
{
    public string InstanceId { get; set; }

    public string CommandType { get; set; } // "send-command", "get-command-invocation"

    public string CommandId { get; set; }

    public string CommandDocument { get; set; }

    public Dictionary<string, List<string>> CommandParameters { get; set; }

    public string CommandComment { get; set; }

    public string CommandPluginName { get; set; }

    public string AwsRegion { get; set; }

    public string AwsRole { get; set; }
}