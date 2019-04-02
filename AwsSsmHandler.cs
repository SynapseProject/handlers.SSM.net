using Synapse.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


public class AwsSsmHandler : HandlerRuntimeBase
{
    private readonly ExecuteResult _result = new ExecuteResult()
    {
        Status = StatusType.None,
        BranchStatus = StatusType.None,
        Sequence = int.MaxValue
    };

    public override ExecuteResult Execute(HandlerStartInfo startInfo)
    {
        string exitData = "This is a test";
        _result.ExitData = exitData;
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

    public override object GetParametersInstance()
    {
        List<string> script = new List<string> { "$env:computername" };
        Dictionary<string, List<string>> commandParameters = new Dictionary<string, List<string>>();
        commandParameters.Add("commands", script);

        return new UserRequest
        {
            InstanceId = "i-12345678",
            InstanceName = "aaaaaaaa",
            CommandType = "send-command", // "get-command-invocation"
            UserId = "bbbbbbbb",
            CommandDocument = "AWS-RunPowerShellScript",
            CommandParameters = commandParameters,
            CommandComment = "cccccccc",
            Region = "eu-west-1"
        };
    }

}

public class HandlerConfig
{
    public int ClientMaxErrorRetry { get; set; }
    public int ClientTimeoutSeconds { get; set; }
    public int ClientReadWriteTimeoutSeconds { get; set; }
    public string CommandMaxConcurrency { get; set; }
    public string CommandMaxErrors { get; set; }
    public int CommandTimeoutSeconds { get; set; }
}

public class SsmCommandResponse
{
    public bool Success { get; set; }

    public string CommandId { get; set; }

    public string CommandStatus { get; set; }

    public string CommandComment { get; set; }

    public string ErrorMessage { get; set; }

    public string StandardOutput { get; set; }

    public string StandardError { get; set; }

    public UserRequest OriginalRequest { get; set; }
}

public class UserRequest
{
    public string InstanceId { get; set; }

    public string InstanceName { get; set; }

    public string CommandType { get; set; }

    public string CommandId { get; set; }

    public string UserId { get; set; }

    public string CommandDocument { get; set; }

    public Dictionary<string, List<string>> CommandParameters { get; set; }

    public string CommandComment { get; set; }

    public string Region { get; set; }
}