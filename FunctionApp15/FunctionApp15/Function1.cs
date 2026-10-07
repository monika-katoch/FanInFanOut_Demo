using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using System.Net;

namespace FunctionApp15;

public class Function1
{
    private readonly ILogger<Function1> _logger;

    public Function1(ILogger<Function1> logger)
    {
        _logger = logger;
    }
    [Function("FanInFanOut")]
    public static async Task<string[]> Run(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        // fan out
        Task<string> task1 =
            context.CallActivityAsync<string>("DoWork", "Mumbai");

        Task<string> task2 =
            context.CallActivityAsync<string>("DoWork", "Pune");

        Task<string> task3 =
            context.CallActivityAsync<string>("DoWork", "Delhi");

        // fan in
        string[] results =    
            await Task.WhenAll(task1, task2, task3);

        return results;
    }
[Function("LongProcess")]
    public static async Task<string> LongProcess(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        string result =
            await context.CallActivityAsync<string>("DoWorkDelay");

        return result;
    }


    // 2. ACTIVITY
    [Function("DoWorkDelay")]
    public static async Task<string> DoWorkDelay(
        [ActivityTrigger] string input)
    {
        // Simulate long-running work
        await Task.Delay(10000);

        return "Work Completed";
    }
    [Function("DoWork")]
    public static string DoWork(
        [ActivityTrigger] string city)
    {
        return $"{city} completed";
    }
    [Function("Start")]
    public static async Task<HttpResponseData> Start(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            "post")]
        HttpRequestData req,

        [DurableClient]
        DurableTaskClient client)
    {
        string instanceId =
            await client.ScheduleNewOrchestrationInstanceAsync( //"FanInFanOut");
                //"LongProcess");
                "StartApproval");

        return await client.CreateCheckStatusResponseAsync(
            req,
            instanceId);
    }
    // 1. ORCHESTRATOR
    [Function("ApprovalWorkflow")]
    public static async Task<string> ApprovalWorkflow(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        // Wait for human approval
        string decision =
            await context.WaitForExternalEvent<string>("ApprovalEvent");

        if (decision == "Approved")
            return "Request Approved";

        return "Request Rejected";
    }


    // 2. START WORKFLOW
    [Function("StartApproval")]
    public static async Task<HttpResponseData> StartApproval(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get")]
        HttpRequestData request,

        [DurableClient]
        DurableTaskClient client)
    {
        string instanceId =
            await client.ScheduleNewOrchestrationInstanceAsync(
                "ApprovalWorkflow");

        return await client.CreateCheckStatusResponseAsync(
            request,
            instanceId);
    }


    // 3. APPROVE
    [Function("Approve")]
    public static async Task<HttpResponseData> Approve(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "approve/{instanceId}")]
        HttpRequestData request,

        string instanceId,

        [DurableClient]
        DurableTaskClient client)
    {
        await client.RaiseEventAsync(
            instanceId,
            "ApprovalEvent",
            "Approved");

        HttpResponseData response =
            request.CreateResponse(HttpStatusCode.OK);

        await response.WriteStringAsync("Approval sent");

        return response;
    }


    // 4. REJECT
    [Function("Reject")]
    public static async Task<HttpResponseData> Reject(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "reject/{instanceId}")]
        HttpRequestData request,

        string instanceId,

        [DurableClient]
        DurableTaskClient client)
    {
        await client.RaiseEventAsync(
            instanceId,
            "ApprovalEvent",
            "Rejected");

        HttpResponseData response =
            request.CreateResponse(HttpStatusCode.OK);

        await response.WriteStringAsync("Rejection sent");

        return response;
    }

    //[Function("Function1")]
    //public IActionResult Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
    //{
    //    _logger.LogInformation("C# HTTP trigger function processed a request.");
    //    return new OkObjectResult("Welcome to Azure Functions!");
    //}
}